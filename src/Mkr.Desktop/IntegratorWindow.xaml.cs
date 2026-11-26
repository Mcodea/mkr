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
        ModelLine.Text = $"Modèle : pente k′ = {model.Result.Gain:G6} {model.PvUnit}/s par {model.MvUnit}, retard θ = {model.Result.DelaySeconds:G6} s.";
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
                ? "Réponse plus rapide que le retard : dépassement important, voir les réserves."
                : "Réglage théorique calculé.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
    }

    private string Describe(IntegratorPi pi)
    {
        var text = new StringBuilder();
        text.AppendLine($"PI intégrateur · règle {pi.Rule} · τc = {pi.ResponseSeconds:G6} s");
        text.AppendLine($"Kp = {pi.Kp:G6} {_model.MvUnit}/{_model.PvUnit}   Ti = {pi.TiSeconds:G6} s   Ki parallèle = {pi.ParallelKi:G6}");
        var m = pi.Margins;
        text.AppendLine($"Marge de gain : {(m.GainMargin is { } g ? g.ToString("G4", CultureInfo.CurrentCulture) : "—")}   " +
                        $"marge de phase : {(m.PhaseMarginDegrees is { } p ? p.ToString("G4", CultureInfo.CurrentCulture) + " °" : "—")}   " +
                        $"sensibilité maximale : {m.PeakSensitivity:G4}");
        text.AppendLine();
        text.AppendLine("Réserves :");
        foreach (var warning in pi.Warnings) text.AppendLine("  · " + Warning(warning));
        return text.ToString();
    }

    private static string Warning(string code) => code switch
    {
        "exploratory_model" => "modèle exploratoire : à confirmer sur l'installation.",
        "actuator_limits_unassessed" => "limites et vitesse de l'actionneur non évaluées.",
        "integrating_process_simc" => "règle SIMC des procédés intégrateurs (Skogestad 2003) ; marges de la boucle nominale seulement.",
        "tight_response" => "τc inférieur au retard : le dépassement croît nettement (environ 69 % pour τc = θ/5 en simulation).",
        "low_phase_margin" => "marge de phase inférieure à 30°.",
        "low_gain_margin" => "marge de gain inférieure à 2 (6 dB).",
        "high_sensitivity" => "sensibilité maximale supérieure à 2.",
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
                ? conversion.Describe("SIMC intégrateur", _model.MvUnit, _model.PvUnit)
                : "Conversion refusée : " + result.RefusalText;
            new BlockValuesWindow("ORIAS — Valeurs du bloc PIDFF (non qualifiées)", text) { Owner = this }.ShowDialog();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
    }
}
