using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class StepSearchWindow : Window
{
    private sealed record PairRow(StepPair Pair, string First, string Second, string Direction, string Ratio, string Notes);

    private readonly string _mvUnit;
    private readonly string _pvUnit;
    private readonly StepSearch _search;

    /// <summary>Both windows of the chosen pair, to use as estimation and validation (same recording).</summary>
    internal StepPair? SelectedPair { get; private set; }

    internal StepSearchWindow(StepSearch search, string recordingName, string mvUnit, string pvUnit)
    {
        _search = search;
        _mvUnit = mvUnit;
        _pvUnit = pvUnit;
        InitializeComponent();
        var byId = search.Candidates.ToDictionary(c => c.Id);
        Summary.Text = Loc.T("cs.lignes_echelon_s_exploitable_s", recordingName, search.SourceRows, search.Candidates.Length, search.Rejected.Length);
        string Describe(StepCandidate c, StepWindow w) =>
            Loc.T("cs.n_lignes", c.Id, w.RowStart, w.RowEnd, c.MvBefore, c.MvAfter, _mvUnit);
        Pairs.ItemsSource = search.Pairs.Select(p => new PairRow(p,
            Describe(byId[p.FirstId], p.FirstWindow), Describe(byId[p.SecondId], p.SecondWindow),
            p.OppositeDirection ? Loc.T("cs.opposes") : Loc.T("cs.meme_sens"), p.AmplitudeRatio.ToString("0.00", CultureInfo.CurrentCulture),
            string.Join(" · ", p.Notes.Select(NoteText).Concat(
                !p.FirstSettled || !p.SecondSettled ? [] : ["réponses stabilisées"])))).ToList();
        Steps.ItemsSource = search.Candidates.Select(c => new
        {
            c.Id, Start = c.StartUtc.UtcDateTime.ToString(Loc.T("cs.yyyy_mm_dd_hh_mm"), CultureInfo.InvariantCulture),
            Mv = $"{c.MvBefore:G5} → {c.MvAfter:G5} {_mvUnit}", Rows = $"{c.RowStart}–{c.RowEnd}",
            Samples = $"{c.PreSamples} / {c.PostSamples}", Horizon = $"{c.HorizonSeconds:G4} s",
            Response = c.Settled ? Loc.T("cs.stabilisee", c.PvResponse, _pvUnit)
                : Loc.T("cs.non_stabilisee_essai_trop_court", c.PvResponse, _pvUnit)
        }).ToList();
        Rejected.ItemsSource = search.Rejected.Select(r => new { Row = r.StepRow, Reason = RejectionText(r.Reason) }).ToList();
        PairHelp.Text = search.Pairs.Length == 0
            ? Loc.T("cs.aucune_paire_exploitable_il_faut")
            : Loc.T("cs.les_paires_sont_classees_sens");
    }

    private static string NoteText(string note) => note switch
    {
        "shared_plateau_split" => Loc.T("cs.plateau_intermediaire_partage_et_coupe"),
        "same_direction" => Loc.T("cs.meme_sens_preferez_deux_sens"),
        "response_not_settled" => Loc.T("cs.reponse_non_stabilisee"),
        _ => note
    };

    private static string RejectionText(string reason) => reason switch
    {
        "ramp_or_multi_sample_change" => Loc.T("cs.rampe_ou_changement_de_la"),
        "baseline_too_short_or_unstable" => Loc.T("cs.moins_de_4_mesures_stables"),
        "response_window_too_short" => Loc.T("cs.moins_de_12_mesures_avant"),
        "search_limit" => Loc.T("cs.limite_de_recherche_atteinte_reduisez"),
        "fit_step" => Loc.T("cs.sortie_de_commande_non_constante"),
        "fit_baseline" => Loc.T("cs.mesure_non_stable_avant_lechelon"),
        "fit_response" => Loc.T("cs.reponse_de_la_mesure_trop"),
        _ => reason
    };

    private void PairChanged(object sender, SelectionChangedEventArgs args) =>
        UsePair.IsEnabled = Pairs.SelectedItem is PairRow;

    private void UseSelectedPair(object sender, RoutedEventArgs args)
    {
        if (Pairs.SelectedItem is not PairRow row) return;
        SelectedPair = row.Pair;
        DialogResult = true;
    }
}
