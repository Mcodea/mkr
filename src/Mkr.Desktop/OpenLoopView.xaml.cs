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
        RateLabel.Content = Loc.T("cs.vitesse_max_de_la_sortie", model.MvUnit);
        Units.Text = Loc.T("cs.etude_fopdt_ecarts_autour_du", model.Label, model.MvUnit, model.PvUnit);
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
            Message.Text = Loc.T("cs.saisissez_les_six_valeurs_numeriques");
            return;
        }

        double? rate = null;
        if (!string.IsNullOrWhiteSpace(Rate.Text))
        {
            if (!Read(Rate, out var maximumRate) || maximumRate <= 0)
            {
                Message.Text = Loc.T("cs.la_vitesse_maximale_de_la");
                return;
            }
            rate = maximumRate;
        }

        ScenarioForm.IsEnabled = false;
        Message.Text = Loc.T("cs.simulation_en_cours");
        var token = _lifetime.Token;
        try
        {
            var (result, version) = await _workspace.SimulateOpenLoopAsync(_model,
                new OpenLoopScenario(sample, duration, step, at, lower, upper, rate), token);
            token.ThrowIfCancellationRequested();
            _shown = result;
            Message.Text = result.Warnings.Contains("response_beyond_horizon")
                ? Loc.T("cs.le_retard_repousse_le_debut")
                : "";
            Report.Text = Loc.T("cs.simulation_exploratoire_methode_moteur_ecart", result.AlgorithmVersion, version, result.Points[^1].Pv, _model.PvUnit, result.SteadyPv, _model.PvUnit, result.SaturationSeconds, result.RateLimitedSeconds, _model.PvUnit);
            ExportButton.IsEnabled = true;
            DrawPlot();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Trace.TraceError("open_loop.simulation {0}", error.GetType().Name);
            Message.Text = error is CsvInputException input ? input.Code switch
            {
                "sim_grid" => Loc.T("cs.utilisez_une_duree_et_un"),
                "sim_domain" => Loc.T("cs.verifiez_que_le_pas_est"),
                _ => Loc.T("cs.verifiez_les_valeurs_et_les")
            } : Loc.T("cs.simulation_indisponible_consultez_le_journal");
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
        Chart.XLabel = Loc.T("cs.temps_depuis_le_debut_du");
        Chart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.ecart_de_mesure"), _model.PvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.simulee"), "MkrSignal1", seconds, result.Points.Select(p => p.Pv).ToArray()),
                new ChartSeries(Loc.T("cs.niveau_initial"), "MkrSignal2", new[] { seconds[0], seconds[^1] }, new[] { 0.0, 0.0 })
            })
        });
    }

    private void Export(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation)
            return;
        var dialog = new SaveFileDialog { FileName = "mkr-boucle-ouverte.csv", Filter = Loc.T("cs.csv_csv_csv") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            var csv = new StringBuilder("time_s,pv_delta,requested_mv_delta,applied_mv_delta\r\n");
            foreach (var point in simulation.Points)
                csv.Append(FormattableString.Invariant(
                    $"{point.Seconds:G17},{point.Pv:G17},{point.RequestedMv:G17},{point.AppliedMv:G17}\r\n"));
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(false));
            Message.Text = Loc.T("cs.series_simulees_exportees");
        }
        catch (Exception error)
        {
            Trace.TraceError("open_loop.export {0}", error.GetType().Name);
            Message.Text = Loc.T("cs.export_csv_impossible");
        }
    }
}
