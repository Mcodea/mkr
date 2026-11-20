using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class SimulationWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly PiStudy _tuning;
    private readonly CancellationTokenSource _lifetime = new();
    private PiSimulationStudy? _shown;
    private double? _zoomStart;
    private double? _zoomEnd;
    private double? _cursorTime;
    private double? _dragX;

    internal SimulationWindow(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        _workspace = workspace;
        _model = model;
        _tuning = tuning;
        InitializeComponent();
        RateLabel.Content = $"Vitesse max. ΔMV ({model.MvUnit}/s, facultative)";
        Units.Text = $"Cible PI : {tuning.Result.ResponseSeconds:G5} s. Déviations autour du point initial : " +
            $"consigne et PV en {model.PvUnit}, saturation et perturbation en {model.MvUnit}.";
        Saved.ItemsSource = _workspace.Simulations(_tuning.Id);
        if (Saved.Items.Count > 0)
            Saved.SelectedIndex = 0;
        Closed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private async void Run(object sender, RoutedEventArgs args)
    {
        if (!Read(Sample, out var sample) || !Read(Duration, out var duration) || !Read(Setpoint, out var setpoint) ||
            !Read(Lower, out var lower) || !Read(Upper, out var upper) || !Read(Disturbance, out var disturbance) ||
            !Read(DisturbanceAt, out var at))
        {
            Message.Text = "Saisissez les sept valeurs numériques du scénario.";
            return;
        }
        double? rate = null;
        if (!string.IsNullOrWhiteSpace(Rate.Text))
        {
            if (!Read(Rate, out var maximumRate) || maximumRate <= 0)
            {
                Message.Text = "La vitesse maximale de MV doit être strictement positive.";
                return;
            }
            rate = maximumRate;
        }

        ScenarioForm.IsEnabled = false;
        Message.Text = "Simulation en cours…";
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.SimulateAsync(_model, _tuning,
                new PiScenario(sample, duration, setpoint, lower, upper, disturbance, at, rate), token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = "Simulation enregistrée.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private void ShowSaved(object sender, SelectionChangedEventArgs args)
    {
        _shown = Saved.SelectedItem as PiSimulationStudy;
        _zoomStart = _zoomEnd = _cursorTime = _dragX = null;
        ResetZoomButton.IsEnabled = false;
        ZoomStatus.Text = "Horizon complet";
        CursorReadout.Text = "Cliquez sur le graphique pour inspecter un échantillon enregistré.";
        ExportButton.IsEnabled = _shown is not null;
        if (_shown is not { } simulation)
        {
            Report.Text = Legend.Text = "";
            DrawPlot();
            return;
        }
        var input = simulation.Scenario;
        Sample.Text = input.SampleSeconds.ToString("G17");
        Duration.Text = input.DurationSeconds.ToString("G17");
        Setpoint.Text = input.Setpoint.ToString("G17");
        Lower.Text = input.MinimumMv.ToString("G17");
        Upper.Text = input.MaximumMv.ToString("G17");
        Disturbance.Text = input.Disturbance.ToString("G17");
        DisturbanceAt.Text = input.DisturbanceAtSeconds.ToString("G17");
        Rate.Text = input.MaximumMvRate?.ToString("G17") ?? "";
        var result = simulation.Result;
        var warnings = result.Warnings.Select(code => code switch
        {
            "coarse_sample" => "Pas supérieur au dixième de la constante de temps. Contrôlez la sensibilité au pas.",
            "delay_beyond_horizon" => "Le retard atteint ou dépasse l’horizon du scénario.",
            _ => "Simulation exploratoire sur modèle nominal."
        });
        Report.Text = $"Méthode {result.AlgorithmVersion} · moteur {simulation.EngineVersion}\n" +
            $"IMC/Lambda : {Metrics(result.Imc)}\nSIMC : {Metrics(result.Simc)}\n" +
            $"IMC : {ResponseWindow(result.Imc.SetpointResponse)}" +
            $"SIMC : {ResponseWindow(result.Simc.SetpointResponse)}" +
            $"{string.Join(" ", warnings)}";
        Legend.Text = $"PV/SP ({_model.PvUnit}) : bleu = IMC/Lambda, orange = SIMC, vert pointillé = consigne. " +
            $"Commandes ({_model.MvUnit}) : pointillés = CV calculée, vert = MV appliquée, gris = limites ; violet = instant de perturbation.";
        DrawPlot();
    }

    // Figures and the exported series are generated from the same persisted simulation runs.
    // A historical study is displayed without re-running a control algorithm.
    private string Metrics(SimulationRun run) =>
        $"IAE {run.Iae:G5} {_model.PvUnit}·s · ISE {run.Ise:G5} {_model.PvUnit}²·s · " +
        $"saturation {run.SaturationSeconds:G5} s · vitesse limitée {run.RateLimitedSeconds:G5} s · " +
        $"intégration gelée {run.FrozenSteps} pas · " +
        $"erreur finale {run.FinalError:G5} {_model.PvUnit}" +
        (run.SetpointResponse is { } response
            ? $" · dépassement {(response.OvershootPv is { } peak ? $"{peak:G5} {_model.PvUnit}" : "—")}" +
              $" · temps de réponse ±5 % {(response.SettlingSeconds is { } time ? $"{time:G5} s" : "—")}"
            : "");

    private static string ResponseWindow(SetpointResponse? response) => response is null
        ? ""
        : $"Fenêtre de réponse à la consigne [0, {response.WindowEndSeconds:G5}] s · — : valeur indisponible.\n";

    private void ResizePlot(object sender, SizeChangedEventArgs args) => DrawPlot();

    private void DrawPlot()
    {
        if (Plot is null)
            return;
        PiComparisonPlot.Draw(Plot, _shown, _model.PvUnit, _model.MvUnit,
                              _zoomStart, _zoomEnd, _cursorTime);
    }

    private (double Start, double End)? VisibleWindow()
    {
        if (_shown?.Result.Imc.Points is not { Length: >= 3 } points)
            return null;
        return (_zoomStart ?? points[0].Seconds, _zoomEnd ?? points[^1].Seconds);
    }

    private void StartPlotSelection(object sender, MouseButtonEventArgs args)
    {
        if (VisibleWindow() is not { } range)
            return;
        var x = args.GetPosition(Plot).X;
        if (!PiComparisonPlot.TryTimeAt(x, Plot.ActualWidth, range.Start, range.End, out _))
            return;
        _dragX = x;
        Plot.CaptureMouse();
    }

    private void EndPlotSelection(object sender, MouseButtonEventArgs args)
    {
        if (_dragX is not { } x0)
            return;
        _dragX = null;
        Plot.ReleaseMouseCapture();
        if (VisibleWindow() is not { } range || _shown is not { } study)
            return;
        var x1 = args.GetPosition(Plot).X;
        if (!PiComparisonPlot.TryTimeAt(x0, Plot.ActualWidth, range.Start, range.End, out var a) ||
            !PiComparisonPlot.TryTimeAt(x1, Plot.ActualWidth, range.Start, range.End, out var b))
            return;

        var points = study.Result.Imc.Points;
        if (Math.Abs(x1 - x0) < 10)
        {
            var selected = points
                .Where(p => p.Seconds >= range.Start && p.Seconds <= range.End)
                .OrderBy(p => Math.Abs(p.Seconds - b))
                .FirstOrDefault();
            if (selected is not null)
            {
                _cursorTime = selected.Seconds;
                var index = Array.FindIndex(points, p => p.Seconds == selected.Seconds);
                var other = study.Result.Simc.Points[index];
                string Value(double? value) => value is { } v ? v.ToString("G6", CultureInfo.CurrentCulture) : "—";
                CursorReadout.Text = $"t = {selected.Seconds:G6} s · ΔSP = {study.Scenario.Setpoint:G6} {_model.PvUnit} · " +
                    $"PV IMC = {selected.Pv:G6} {_model.PvUnit}, SIMC = {other.Pv:G6} {_model.PvUnit} · " +
                    $"CV IMC = {Value(selected.Cv)}, MV IMC = {Value(selected.Mv)} · " +
                    $"CV SIMC = {Value(other.Cv)}, MV SIMC = {Value(other.Mv)} {_model.MvUnit}";
                DrawPlot();
            }
            return;
        }
        var lower = Math.Min(a, b);
        var upper = Math.Max(a, b);
        var selectedPoints = points.Where(p => p.Seconds >= lower && p.Seconds <= upper).ToArray();
        if (selectedPoints.Length < 3)
        {
            Message.Text = "Zoom refusé : sélectionnez au moins trois points simulés.";
            return;
        }
        _zoomStart = selectedPoints[0].Seconds;
        _zoomEnd = selectedPoints[^1].Seconds;
        _cursorTime = null;
        ResetZoomButton.IsEnabled = true;
        ZoomStatus.Text = $"Zoom [{_zoomStart:G6} ; {_zoomEnd:G6}] s · {selectedPoints.Length} points";
        CursorReadout.Text = "Cliquez sur le graphique pour inspecter un point simulé du zoom.";
        DrawPlot();
    }

    private void ResetZoom(object sender, RoutedEventArgs args)
    {
        _zoomStart = _zoomEnd = _cursorTime = _dragX = null;
        Plot.ReleaseMouseCapture();
        ResetZoomButton.IsEnabled = false;
        ZoomStatus.Text = "Horizon complet";
        CursorReadout.Text = "Cliquez sur le graphique pour inspecter un échantillon enregistré.";
        DrawPlot();
    }

    private void Export(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation)
            return;
        var dialog = new SaveFileDialog { FileName = "mkr-simulation-pi.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            var csv = new StringBuilder("time_s,sp_delta,imc_pv,imc_cv,imc_mv,simc_pv,simc_cv,simc_mv,disturbance_mv\r\n");
            foreach (var (imc, simc) in simulation.Result.Imc.Points.Zip(simulation.Result.Simc.Points))
                csv.Append(FormattableString.Invariant(
                    $"{imc.Seconds:G17},{simulation.Scenario.Setpoint:G17},{imc.Pv:G17},{imc.Cv:G17},{imc.Mv:G17},{simc.Pv:G17},{simc.Cv:G17},{simc.Mv:G17},{imc.Disturbance:G17}\r\n"));
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(false));
            Message.Text = "Séries simulées exportées.";
        }
        catch (Exception error) { ShowError(error); }
    }

    private static bool Read(TextBox input, out double value) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);

    private void ShowError(Exception error)
    {
        Trace.TraceError("pi.simulation {0}", error.GetType().Name);
        Message.Text = error is CsvInputException input ? input.Code switch
        {
            "sim_grid" => "Utilisez une durée et un instant de perturbation multiples du pas, pour 2 à 5 000 intervalles.",
            "sim_domain" => "Vérifiez le modèle, le pas positif, les limites de MV autour de zéro et l’instant de perturbation.",
            "sim_range" => "La dynamique calculée dépasse la plage numérique. Vérifiez le pas et le réglage.",
            _ => "Vérifiez les entrées et les unités du scénario."
        } : "Simulation indisponible. Vérifiez le stockage et le journal MKR.";
    }
}
