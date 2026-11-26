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
        Chart.CursorTextChanged += text => CursorText.Text = text.Length == 0 ? Loc.T("cs.survolez_le_graphique_pour_lire") : text;
        Kp.Text = tuning.Result.Imc.Kp.ToString("G17", CultureInfo.CurrentCulture);
        Ti.Text = tuning.Result.Imc.TiSeconds.ToString("G17", CultureInfo.CurrentCulture);
        ContextInfo.Text =
            Loc.T("cs.modele_fopdt_gain_k_s", model.Id.ToString()[..8], model.Result.Gain, model.PvUnit, model.MvUnit, model.Result.TauSeconds, model.Result.DelaySeconds, scenario.SampleSeconds, scenario.DurationSeconds);
        Status.Text = Loc.T("cs.saisissez_kp_ti_td_et");
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
            Status.Text = Loc.T("cs.parametres_rejetes_kp_doit_avoir");
            return;
        }
        InputPanel.IsEnabled = false;
        ResultsPanel.Visibility = PlotPanel.Visibility = Visibility.Collapsed;
        Status.Text = Loc.T("cs.calcul_scientifique_du_meme_scenario");
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
                    Loc.T("cs.comparaison_interdite_incoherence_de_versions"));

            _pi = reference.Imc;
            _pid = pid;
            Metrics.ItemsSource = new[]
            {
                new {
                    Method = Loc.T("cs.pi_lambda"),
                    _pi.Iae, _pi.Ise, _pi.FinalError,
                    Saturation = _pi.SaturationSeconds,
                    RateLimited = _pi.RateLimitedSeconds,
                    _pi.FrozenSteps
                },
                new {
                    Method = Loc.T("cs.pid_manuel"),
                    pid.Iae, pid.Ise, pid.FinalError,
                    Saturation = pid.SaturationSeconds,
                    RateLimited = pid.RateLimitedSeconds,
                    pid.FrozenSteps
                }
            };
            ResultMeta.Text =
                Loc.T("cs.moteur_pid_kp_ti_s", pidVersion, pid.AlgorithmVersion, kp, ti, td, tf);
            Legend.Text =
                Loc.T("cs.courbes_pi_lambda_de_reference", _model.PvUnit, _model.MvUnit, _model.MvUnit);
            ResultsPanel.Visibility = PlotPanel.Visibility = Visibility.Visible;
            Draw();
            Status.Text = pid.Warnings.Contains("coarse_derivative_filter")
                ? Loc.T("cs.avertissement_le_pas_de_calcul")
                : Loc.T("cs.comparaison_hors_ligne_calculee_aucune");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Trace.TraceError("pid.prototype {0}", exception.GetType().Name);
            Status.Text = exception is CsvInputException input
                ? Loc.T("cs.simulation_refusee_verifiez_les_parametres", input.Code)
                : Loc.T("cs.simulation_indisponible_ou_resultats_incompatibles");
        }
        finally { InputPanel.IsEnabled = true; }
    }

    private void Draw()
    {
        if (_pid is null || _pi is null || _pid.Points.Length != _pi.Points.Length || _pid.Points.Length < 3 ||
            _pid.Points.Where((p, index) => p.Seconds != _pi.Points[index].Seconds).Any())
        {
            Chart.SetTracks(Array.Empty<ChartTrack>());
            return;
        }
        var pi = _pi.Points;
        var pid = _pid.Points;
        var seconds = pi.Select(p => p.Seconds).ToArray();
        var span = new[] { seconds[0], seconds[^1] };
        var mvPi = pi.Where(p => p.Mv.HasValue).ToArray();
        var mvPid = pid.Where(p => p.Mv.HasValue).ToArray();
        var derivative = pid.Where(p => p.Derivative.HasValue).ToArray();
        Chart.XLabel = Loc.T("cs.temps_depuis_l_origine_du");
        Chart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.mesure_et_consigne"), _model.PvUnit, new[]
            {
                new ChartSeries("Consigne", "MkrSignal3", span, new[] { _scenario.Setpoint, _scenario.Setpoint }),
                new ChartSeries(Loc.T("cs.pi_lambda_reference"), "MkrSignal1", seconds, pi.Select(p => p.Pv).ToArray()),
                new ChartSeries(Loc.T("cs.pid_manuel"), "MkrSignal2", pid.Select(p => p.Seconds).ToArray(), pid.Select(p => p.Pv).ToArray())
            }) { Weight = 2 },
            new ChartTrack(Loc.T("cs.sortie_de_commande_appliquee"), _model.MvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.pi_lambda"), "MkrSignal1", mvPi.Select(p => p.Seconds).ToArray(), mvPi.Select(p => p.Mv!.Value).ToArray()),
                new ChartSeries(Loc.T("cs.pid_manuel"), "MkrSignal2", mvPid.Select(p => p.Seconds).ToArray(), mvPid.Select(p => p.Mv!.Value).ToArray()),
                new ChartSeries(Loc.T("cs.limite_basse"), "MkrPlotAxis", span, new[] { _scenario.MinimumMv, _scenario.MinimumMv }),
                new ChartSeries(Loc.T("cs.limite_haute"), "MkrPlotAxis", span, new[] { _scenario.MaximumMv, _scenario.MaximumMv })
            }),
            new ChartTrack(Loc.T("cs.action_derivee_du_pid"), _model.MvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.derivee_filtree"), "MkrSignal4", derivative.Select(p => p.Seconds).ToArray(), derivative.Select(p => p.Derivative!.Value).ToArray())
            })
        });
    }

    private void CloseDialog(object sender, RoutedEventArgs args) => Close();
}
