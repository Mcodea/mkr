using System.Diagnostics;
using System.Globalization;
using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Manual offline scientific experiment. Deliberately no persistence or PLC actions.</summary>
public partial class PidPrototypeWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly PiStudy _tuning;
    private readonly PiScenario _scenario;
    private readonly CancellationTokenSource _lifetime = new();
    private PidPrototypeResult? _pid;
    private SimulationRun? _pi;

    internal PidPrototypeWindow(IdentificationWorkspace workspace, IdentificationStudy model,
        PiStudy tuning, PiScenario scenario)
    {
        _workspace = workspace;
        _model = model;
        _tuning = tuning;
        _scenario = scenario;
        InitializeComponent();
        Kp.Text = tuning.Result.Imc.Kp.ToString("G17", CultureInfo.CurrentCulture);
        Ti.Text = tuning.Result.Imc.TiSeconds.ToString("G17", CultureInfo.CurrentCulture);
        ContextInfo.Text =
            $"Modèle FOPDT {model.Id.ToString()[..8]} · gain K {model.Result.Gain:G6} " +
            $"{model.PvUnit}/{model.MvUnit} · τ {model.Result.TauSeconds:G6} s · " +
            $"θ {model.Result.DelaySeconds:G6} s · h {scenario.SampleSeconds:G6} s · " +
            $"horizon {scenario.DurationSeconds:G6} s. " +
            "La référence est le réglage PI IMC enregistré, recalculé sur le même scénario.";
        Status.Text = "Saisissez Kp, Ti, Td et Tf puis lancez l'essai hors ligne.";
        Closed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private static bool Read(System.Windows.Controls.TextBox box, out double result) =>
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out result)
        && double.IsFinite(result);

    private async void Run(object sender, RoutedEventArgs args)
    {
        if (!Read(Kp, out var kp) || !Read(Ti, out var ti) ||
            !Read(Td, out var td) || !Read(Tf, out var tf) ||
            kp == 0 || ti <= 0 || td < 0 || tf < 0 || (td > 0 && tf <= 0) ||
            Math.Sign(kp) != Math.Sign(_model.Result.Gain))
        {
            Status.Text = "Paramètres rejetés : Kp doit avoir le signe du procédé, Ti > 0, Td ≥ 0, " +
                "Tf ≥ 0 et Tf > 0 si Td > 0.";
            return;
        }
        InputPanel.IsEnabled = false;
        ResultsPanel.Visibility = PlotPanel.Visibility = Visibility.Collapsed;
        Status.Text = "Calcul scientifique du même scénario PI / PID…";
        var token = _lifetime.Token;
        try
        {
            var (reference, referenceVersion) = await _workspace.PreviewPiAsync(
                _model, _tuning, _scenario, token);
            token.ThrowIfCancellationRequested();
            var (pid, pidVersion) = await _workspace.SimulatePidPrototypeAsync(
                _model, _tuning, new PidPrototypeCoefficients(kp, ti, td, tf),
                _scenario, token);
            token.ThrowIfCancellationRequested();
            if (referenceVersion != pidVersion ||
                reference.Imc.Points.Length != pid.Points.Length ||
                reference.Imc.Points.Where((p, index) =>
                    p.Seconds != pid.Points[index].Seconds).Any())
                throw new InvalidOperationException(
                    "Comparaison interdite : incohérence de versions moteur ou de pas temporel.");

            _pi = reference.Imc;
            _pid = pid;
            Metrics.ItemsSource = new[]
            {
                new {
                    Method = "PI IMC",
                    _pi.Iae, _pi.Ise, _pi.FinalError,
                    Saturation = _pi.SaturationSeconds,
                    RateLimited = _pi.RateLimitedSeconds,
                    _pi.FrozenSteps
                },
                new {
                    Method = "PID manuel",
                    pid.Iae, pid.Ise, pid.FinalError,
                    Saturation = pid.SaturationSeconds,
                    RateLimited = pid.RateLimitedSeconds,
                    pid.FrozenSteps
                }
            };
            ResultMeta.Text =
                $"Moteur {pidVersion} · PID {pid.AlgorithmVersion} · " +
                $"Kp {kp:G6}, Ti {ti:G6} s, Td {td:G6} s, Tf {tf:G6} s. " +
                "Résultats calculés sur le même procédé et le même scénario ; " +
                "aucun réglage n'est sauvegardé ni transféré.";
            Legend.Text =
                $"Bleu : PI IMC de référence ; orange : PID manuel ; vert pointillé : consigne. " +
                $"Piste 1 : mesure ({_model.PvUnit}) · piste 2 : sortie de commande appliquée ({_model.MvUnit}) · " +
                $"piste 3 : dérivée filtrée PID ({_model.MvUnit}).";
            ResultsPanel.Visibility = PlotPanel.Visibility = Visibility.Visible;
            Draw();
            Status.Text = pid.Warnings.Contains("coarse_derivative_filter")
                ? "Avertissement : le pas de calcul est supérieur à Tf. Réduisez le pas " +
                  "et vérifiez la convergence numérique avant toute interprétation."
                : "Comparaison hors ligne calculée. Aucune qualification d'un bloc constructeur.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Trace.TraceError("pid.prototype {0}", exception.GetType().Name);
            Status.Text = exception is CsvInputException input
                ? $"Simulation refusée ({input.Code}). Vérifiez les paramètres et le scénario."
                : "Simulation indisponible ou résultats incompatibles. " +
                  "Consultez le journal MKR et vérifiez les hypothèses.";
        }
        finally { InputPanel.IsEnabled = true; }
    }

    private void ResizeGraph(object sender, SizeChangedEventArgs args) => Draw();

    private void Draw() => PidPrototypePlot.Draw(
        Graph, _pid, _pi, _scenario, _model.PvUnit, _model.MvUnit);

    private void CloseDialog(object sender, RoutedEventArgs args) => Close();
}
