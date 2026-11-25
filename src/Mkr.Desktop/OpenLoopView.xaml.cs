using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class OpenLoopView : UserControl, IStepView
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly CancellationTokenSource _lifetime = new();

    public void Cancel() { _lifetime.Cancel(); _lifetime.Dispose(); }
    private OpenLoopResult? _shown;

    internal OpenLoopView(IdentificationWorkspace workspace, IdentificationStudy model)
    {
        _workspace = workspace;
        _model = model;
        InitializeComponent();
        RateLabel.Content = $"Vitesse max. de la sortie ({model.MvUnit}/s, facultative)";
        Units.Text = $"Étude FOPDT {model.Label}\n" +
            $"Écarts autour du point initial. Écarts de la sortie de commande en {model.MvUnit}, de la mesure en {model.PvUnit}.";
    }

    private async void Run(object sender, RoutedEventArgs args)
    {
        _shown = null;
        ExportButton.IsEnabled = false;
        Report.Text = "";
        DrawPlot();
        if (!Read(Sample, out var sample) || !Read(Duration, out var duration) || !Read(StepMv, out var step) ||
            !Read(StepAt, out var at) || !Read(Lower, out var lower) || !Read(Upper, out var upper))
        {
            Message.Text = "Saisissez les six valeurs numériques du scénario.";
            return;
        }

        double? rate = null;
        if (!string.IsNullOrWhiteSpace(Rate.Text))
        {
            if (!Read(Rate, out var maximumRate) || maximumRate <= 0)
            {
                Message.Text = "La vitesse maximale de la sortie de commande doit être strictement positive.";
                return;
            }
            rate = maximumRate;
        }

        ScenarioForm.IsEnabled = false;
        Message.Text = "Simulation en cours…";
        var token = _lifetime.Token;
        try
        {
            var (result, version) = await _workspace.SimulateOpenLoopAsync(_model,
                new OpenLoopScenario(sample, duration, step, at, lower, upper, rate), token);
            token.ThrowIfCancellationRequested();
            _shown = result;
            Message.Text = result.Warnings.Contains("response_beyond_horizon")
                ? "Le retard repousse le début de la réponse jusqu’à la fin du scénario ou au-delà."
                : "";
            Report.Text = $"Simulation exploratoire · méthode {result.AlgorithmVersion} · moteur {version}\n" +
                $"Écart final de la mesure {result.Points[^1].Pv:G5} {_model.PvUnit} · " +
                $"Écart asymptotique de la mesure {result.SteadyPv:G5} {_model.PvUnit} · saturation {result.SaturationSeconds:G5} s · " +
                $"vitesse limitée {result.RateLimitedSeconds:G5} s\n" +
                $"Écart de mesure ({_model.PvUnit}) : réponse simulée et niveau initial (voir la légende du graphique)";
            ExportButton.IsEnabled = true;
            DrawPlot();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Trace.TraceError("open_loop.simulation {0}", error.GetType().Name);
            Message.Text = error is CsvInputException input ? input.Code switch
            {
                "sim_grid" => "Utilisez une durée et un instant d’échelon multiples du pas, avec 2 à 5 000 intervalles.",
                "sim_domain" => "Vérifiez que le pas est positif, l’échelon non nul et antérieur à la fin, et les limites de la sortie de commande de part et d’autre de zéro.",
                _ => "Vérifiez les valeurs et les unités du scénario."
            } : "Simulation indisponible. Consultez le journal MKR.";
        }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private static bool Read(TextBox input, out double value) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);

    private void DrawPlot()
    {
        if (Chart is null)
            return;
        if (_shown is not { Points.Length: >= 2 } result)
        {
            Chart.SetTracks(Array.Empty<ChartTrack>());
            return;
        }
        var seconds = result.Points.Select(p => p.Seconds).ToArray();
        Chart.XLabel = "Temps depuis le début du scénario (s)";
        Chart.SetTracks(new[]
        {
            new ChartTrack("Écart de mesure", _model.PvUnit, new[]
            {
                new ChartSeries("Simulée", "MkrSignal1", seconds, result.Points.Select(p => p.Pv).ToArray()),
                new ChartSeries("Niveau initial", "MkrSignal2", new[] { seconds[0], seconds[^1] }, new[] { 0.0, 0.0 })
            })
        });
    }

    private void Export(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation)
            return;
        var dialog = new SaveFileDialog { FileName = "mkr-boucle-ouverte.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            var csv = new StringBuilder("time_s,pv_delta,requested_mv_delta,applied_mv_delta\r\n");
            foreach (var point in simulation.Points)
                csv.Append(FormattableString.Invariant(
                    $"{point.Seconds:G17},{point.Pv:G17},{point.RequestedMv:G17},{point.AppliedMv:G17}\r\n"));
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(false));
            Message.Text = "Séries simulées exportées.";
        }
        catch (Exception error)
        {
            Trace.TraceError("open_loop.export {0}", error.GetType().Name);
            Message.Text = "Export CSV impossible.";
        }
    }
}
