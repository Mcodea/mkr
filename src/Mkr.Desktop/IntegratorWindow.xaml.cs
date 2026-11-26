using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>PI of an accepted integrator model (SIMC integrating rule) with the nominal-loop margins; PIDFF values on request.</summary>
public partial class IntegratorWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly CancellationTokenSource _lifetime = new();
    private IntegratorPi? _tuning;

    internal IntegratorWindow(IdentificationWorkspace workspace, IdentificationStudy model)
    {
        InitializeComponent();
        _workspace = workspace;
        _model = model;
        ModelLine.Text = Loc.T("cs.modele_pente_k_s_par", model.Result.Gain, model.PvUnit, model.MvUnit, model.Result.DelaySeconds);
        Closed += (_, _) => _lifetime.Cancel();
    }

    private static bool TryPositive(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value) && value > 0;

    private void InputChanged(object sender, TextChangedEventArgs args)
    {
        if (Compute is null) return;
        Compute.IsEnabled = TryPositive(ResponseSeconds.Text, out _);
        BlockValues.IsEnabled = false;
        _tuning = null;
        Result.Text = "";
    }

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        if (!TryPositive(ResponseSeconds.Text, out var response)) return;
        var token = _lifetime.Token;
        try
        {
            var tuning = await _workspace.CalculateIntegratorPiAsync(_model, response, token);
            token.ThrowIfCancellationRequested();
            _tuning = tuning;
            Result.Text = Describe(tuning);
            BlockValues.IsEnabled = true;
            Status.Text = tuning.Warnings.Contains("tight_response")
                ? Loc.T("cs.reponse_plus_rapide_que_le")
                : Loc.T("cs.reglage_theorique_calcule");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
    }

    private string Describe(IntegratorPi pi)
    {
        var text = new StringBuilder();
        text.AppendLine(Loc.T("cs.pi_integrateur_regle_c_s", pi.Rule, pi.ResponseSeconds));
        text.AppendLine(Loc.T("cs.kp_ti_s_ki_parallele_2", pi.Kp, _model.MvUnit, _model.PvUnit, pi.TiSeconds, pi.ParallelKi));
        var m = pi.Margins;
        text.AppendLine(Loc.T("margins.gain_colon") + $" {(m.GainMargin is { } g ? g.ToString("G4", CultureInfo.CurrentCulture) : "—")}   " +
                        Loc.T("margins.phase_colon") + $" {(m.PhaseMarginDegrees is { } p ? p.ToString("G4", CultureInfo.CurrentCulture) + " °" : "—")}   " +
                        Loc.T("margins.peak_colon") + $" {m.PeakSensitivity:G4}");
        text.AppendLine();
        text.AppendLine(Loc.T("cs.reserves"));
        foreach (var warning in pi.Warnings) text.AppendLine("  · " + Warning(warning));
        return text.ToString();
    }

    private static string Warning(string code) => code switch
    {
        "exploratory_model" => Loc.T("cs.modele_exploratoire_a_confirmer_sur"),
        "actuator_limits_unassessed" => Loc.T("cs.limites_et_vitesse_de_l"),
        "integrating_process_simc" => Loc.T("cs.regle_simc_des_procedes_integrateurs_2"),
        "tight_response" => Loc.T("cs.c_inferieur_au_retard_le"),
        "low_phase_margin" => Loc.T("cs.marge_de_phase_inferieure_a"),
        "low_gain_margin" => Loc.T("cs.marge_de_gain_inferieure_a"),
        "high_sensitivity" => Loc.T("cs.sensibilite_maximale_superieure_a_2"),
        _ => code
    };

    private async void ShowBlock(object sender, RoutedEventArgs args)
    {
        if (_tuning is not { } tuning) return;
        var token = _lifetime.Token;
        try
        {
            var result = await _workspace.ConvertIntegratorToPidffAsync(_model, tuning, token);
            token.ThrowIfCancellationRequested();
            var text = result.Conversion is { } conversion
                ? conversion.Describe(Loc.T("cs.simc_integrateur"), _model.MvUnit, _model.PvUnit)
                : Loc.T("cs.conversion_refusee") + result.RefusalText;
            new BlockValuesWindow(Loc.T("cs.orias_valeurs_du_bloc_pidff"), text) { Owner = this }.ShowDialog();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
    }
}
