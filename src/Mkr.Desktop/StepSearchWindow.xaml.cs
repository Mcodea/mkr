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
        Summary.Text = $"{recordingName} · {search.SourceRows} lignes · {search.Candidates.Length} échelon(s) exploitable(s), " +
            $"{search.Rejected.Length} écarté(s). Chaque fenêtre listée est acceptée par l’estimateur avec les tolérances déclarées. " +
            "Rien n’est lissé, comblé ni modifié : MKR ne fait que repérer des lignes de l’enregistrement d’origine.";
        string Describe(StepCandidate c, StepWindow w) =>
            $"n°{c.Id} · lignes {w.RowStart}–{w.RowEnd} · {c.MvBefore:G5} → {c.MvAfter:G5} {_mvUnit}";
        Pairs.ItemsSource = search.Pairs.Select(p => new PairRow(p,
            Describe(byId[p.FirstId], p.FirstWindow), Describe(byId[p.SecondId], p.SecondWindow),
            p.OppositeDirection ? "opposés" : "même sens", p.AmplitudeRatio.ToString("0.00", CultureInfo.CurrentCulture),
            string.Join(" · ", p.Notes.Select(NoteText).Concat(
                !p.FirstSettled || !p.SecondSettled ? [] : ["réponses stabilisées"])))).ToList();
        Steps.ItemsSource = search.Candidates.Select(c => new
        {
            c.Id, Start = c.StartUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Mv = $"{c.MvBefore:G5} → {c.MvAfter:G5} {_mvUnit}", Rows = $"{c.RowStart}–{c.RowEnd}",
            Samples = $"{c.PreSamples} / {c.PostSamples}", Horizon = $"{c.HorizonSeconds:G4} s",
            Response = c.Settled ? $"{c.PvResponse:G4} {_pvUnit}, stabilisée"
                : $"{c.PvResponse:G4} {_pvUnit}, non stabilisée : essai trop court ou procédé intégrateur"
        }).ToList();
        Rejected.ItemsSource = search.Rejected.Select(r => new { Row = r.StepRow, Reason = RejectionText(r.Reason) }).ToList();
        PairHelp.Text = search.Pairs.Length == 0
            ? "Aucune paire exploitable. Il faut deux échelons propres de la sortie de commande, séparés par un plateau, d’amplitudes comparables " +
              "(rapport entre 0,5 et 2). Refaites un essai en boucle ouverte si besoin."
            : "Les paires sont classées : sens opposés et réponses stabilisées d’abord, puis amplitudes proches. " +
              "Un essai aller-retour partage un plateau : MKR le coupe pour que les deux plages ne partagent aucune ligne.";
    }

    private static string NoteText(string note) => note switch
    {
        "shared_plateau_split" => "plateau intermédiaire partagé et coupé",
        "same_direction" => "même sens (préférez deux sens opposés)",
        "response_not_settled" => "réponse non stabilisée",
        _ => note
    };

    private static string RejectionText(string reason) => reason switch
    {
        "ramp_or_multi_sample_change" => "Rampe ou changement de la sortie de commande sur plusieurs mesures : pas un échelon.",
        "baseline_too_short_or_unstable" => "Moins de 4 mesures stables avant l’échelon (mesure en mouvement, trou de données ou échelon précédent).",
        "response_window_too_short" => "Moins de 12 mesures avant le prochain changement de la sortie de commande ou de données manquantes.",
        "search_limit" => "Limite de recherche atteinte : réduisez la période de l’enregistrement.",
        "fit_step" => "Sortie de commande non constante de part et d’autre de l’échelon, ou amplitude trop faible devant sa tolérance.",
        "fit_baseline" => "Mesure non stable avant l’échelon dans sa tolérance.",
        "fit_response" => "Réponse de la mesure trop faible devant sa tolérance.",
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
