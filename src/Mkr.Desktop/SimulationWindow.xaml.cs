using System.Diagnostics;
using System.Globalization;
using System.IO;
using Mkr.Infrastructure;
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

    internal SimulationWindow(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        _workspace = workspace;
        _model = model;
        _tuning = tuning;
        InitializeComponent();
        Chart.CursorTextChanged += text => CursorReadout.Text = text.Length == 0 ? "Survolez le graphique pour lire les valeurs au curseur." : text;
        Chart.ViewChanged += () => ZoomStatus.Text = $"Vue [{Chart.View.From:G6} ; {Chart.View.To:G6}] s";
        RateLabel.Content = $"Vitesse max. de la sortie ({model.MvUnit}/s, facultative)";
        Units.Text = $"Cible PI : {tuning.Result.ResponseSeconds:G5} s. Déviations autour du point initial : " +
            $"consigne et mesure en {model.PvUnit}, saturation et perturbation en {model.MvUnit}.";
        Saved.ItemsSource = _workspace.Simulations(_tuning.Id);
        if (Saved.Items.Count > 0)
            Saved.SelectedIndex = 0;
        Closed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private bool TryReadScenario(out PiScenario scenario)
    {
        scenario = null!;
        if (!Read(Sample, out var sample) || !Read(Duration, out var duration) ||
            !Read(Setpoint, out var setpoint) || !Read(Lower, out var lower) ||
            !Read(Upper, out var upper) || !Read(Disturbance, out var disturbance) ||
            !Read(DisturbanceAt, out var at))
        {
            Message.Text = "Saisissez les sept valeurs numériques du scénario.";
            return false;
        }
        double? rate = null;
        if (!string.IsNullOrWhiteSpace(Rate.Text))
        {
            if (!Read(Rate, out var maximumRate) || maximumRate <= 0)
            {
                Message.Text = "La vitesse maximale de la sortie de commande doit être strictement positive.";
                return false;
            }
            rate = maximumRate;
        }
        scenario = new PiScenario(sample, duration, setpoint, lower, upper, disturbance, at, rate);
        return true;
    }

    private async void RunTransfer(object sender, RoutedEventArgs args)
    {
        if (!TryReadScenario(out var scenario)) return;
        if (!Read(ManualMv, out var manual) || !Read(SwitchTime, out var at) ||
            at <= 0 || at >= scenario.DurationSeconds)
        {
            Message.Text = "Renseignez la commande manuelle et un instant de bascule compris entre le début et la fin du scénario.";
            return;
        }
        var token = _lifetime.Token;
        bool simc = TransferTune.SelectedIndex == 1;
        ScenarioForm.IsEnabled = false;
        Message.Text = "Simulation du transfert en cours…";
        try
        {
            var transfer = await _workspace.TransferAsync(_model, _tuning, simc, scenario, manual, at, token);
            token.ThrowIfCancellationRequested();
            var first = Array.FindIndex(transfer.Points, p => p.Mode == "automatic");
            var before = transfer.Points[first - 1];
            var after = transfer.Points[first];
            string method = simc ? "SIMC" : "Lambda (Dahlin)";
            string warnings = string.Join(", ", transfer.Warnings);
            Message.Text = $"Essai hors ligne {method} à {after.Seconds:G6} s · sortie manuelle demandée (écart) {manual:G6} {_model.MvUnit}\n" +
                $"Sortie de commande appliquée avant → après : {before.Mv:G6} → {after.Mv:G6} {_model.MvUnit} · saut de commande : {transfer.TransferBumpMv:G6} {_model.MvUnit}\n" +
                $"Écart de mesure à la bascule : {after.Pv:G6} {_model.PvUnit} · saturation : {transfer.SaturationSeconds:G6} s · limitation de vitesse : {transfer.RateLimitedSeconds:G6} s\n" +
                $"Avertissements moteur : {warnings}";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private void OpenPidPrototype(object sender, RoutedEventArgs args)
    {
        if (!TryReadScenario(out var scenario))
            return;
        try
        {
            new PidPrototypeWindow(_workspace, _model, _tuning, scenario)
            { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            ShowError(error);
        }
    }

    private async void Run(object sender, RoutedEventArgs args)
    {
        if (!TryReadScenario(out var scenario))
            return;
        ScenarioForm.IsEnabled = false;
        Message.Text = "Simulation en cours…";
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.SimulateAsync(_model, _tuning, scenario, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = "Simulation enregistrée.";
            ScenarioFields.IsExpanded = false; // laisse la place au graphique
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private async void RunRobustness(object sender, RoutedEventArgs args)
    {
        if (!TryReadScenario(out var scenario))
            return;
        if (!Read(Variation, out var variation) || variation <= 0 || variation > 50)
        {
            Message.Text = "Indiquez une variation de sensibilité strictement supérieure à 0 et inférieure ou égale à 50 %.";
            return;
        }
        ScenarioForm.IsEnabled = false;
        Message.Text = "Calcul de sept scénarios de sensibilité en cours…";
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.AnalyzeRobustnessAsync(
                _model, _tuning, scenario, variation, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = "Analyse de sensibilité enregistrée avec sa simulation nominale.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private async void RunCombined(object sender, RoutedEventArgs args)
    {
        if (!TryReadScenario(out var scenario))
            return;
        if (!Read(Variation, out var variation) || variation <= 0 || variation > 50)
        {
            Message.Text = "Indiquez une variation supérieure à 0 et inférieure ou égale à 50 %.";
            return;
        }
        ScenarioForm.IsEnabled = false;
        Message.Text = "Calcul de la matrice de 27 procédés en cours…";
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.AnalyzeCombinedAsync(
                _model, _tuning, scenario, variation, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = "Matrice combinée enregistrée avec l'enveloppe de la mesure et sa simulation nominale.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private void ShowSaved(object sender, SelectionChangedEventArgs args)
    {
        _shown = Saved.SelectedItem as PiSimulationStudy;
        ZoomStatus.Text = "Horizon complet";
        CursorReadout.Text = "Survolez le graphique pour lire les valeurs au curseur.";
        SheetButton.IsEnabled = _shown is not null;
        ExportButton.IsEnabled = _shown is not null;
        RobustnessPanel.Visibility = _shown?.Robustness is null
            ? Visibility.Collapsed : Visibility.Visible;
        if (_shown?.Robustness is { } sensitivity)
        {
            RobustnessGrid.ItemsSource = sensitivity.Cases.Select(caseItem => new
            {
                Name = caseItem.Key switch
                {
                    "nominal" => "Nominal",
                    "gain_minus" => "Gain −",
                    "gain_plus" => "Gain +",
                    "tau_minus" => "Constante τ −",
                    "tau_plus" => "Constante τ +",
                    "delay_minus" => "Retard θ −",
                    "delay_plus" => "Retard θ +",
                    _ => caseItem.Key
                },
                caseItem.Gain,
                Tau = caseItem.TauSeconds,
                Delay = caseItem.DelaySeconds,
                ImcIae = caseItem.Imc.Iae,
                SimcIae = caseItem.Simc.Iae,
                ImcSaturation = caseItem.Imc.SaturationSeconds,
                SimcSaturation = caseItem.Simc.SaturationSeconds
            }).ToArray();
            Variation.Text = sensitivity.VariationPercent.ToString("G5", CultureInfo.CurrentCulture);
            var worstImc = sensitivity.Cases.Max(c => c.Imc.Iae);
            var worstSimc = sensitivity.Cases.Max(c => c.Simc.Iae);
            var isCombined = sensitivity.Envelope is not null;
            var mode = isCombined
                ? "Grille combinée K × τ × θ (27 procédés)"
                : "Variation individuelle de K, τ ou θ (7 procédés)";
            RobustnessSummary.Text =
                $"{mode}, ±{sensitivity.VariationPercent:G5} %, coefficients PI fixes. " +
                $"IAE maximale parmi les {sensitivity.Cases.Length} cas : " +
                $"IMC {worstImc:G5} {_model.PvUnit}·s ; SIMC {worstSimc:G5} {_model.PvUnit}·s. " +
                (isCombined ? "Les enveloppes de la mesure indiquent seulement les extrema simulés, pas un intervalle de confiance. " : "") +
                $"Méthode {sensitivity.AlgorithmVersion} · modèle nominal conservé.";
        }
        else
        {
            RobustnessGrid.ItemsSource = null;
            RobustnessSummary.Text = "";
        }
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
        Report.Text = $"Simulation enregistrée {simulation.Id.ToString()[..8]} · durée {input.DurationSeconds:G6} s · " +
            $"Variation de consigne {input.Setpoint:G6} {_model.PvUnit} · pas {input.SampleSeconds:G6} s\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {simulation.EngineVersion}\n" +
            $"Lambda : {Metrics(result.Imc)}\nSIMC : {Metrics(result.Simc)}\n" +
            $"IMC : {ResponseWindow(result.Imc.SetpointResponse)}" +
            $"SIMC : {ResponseWindow(result.Simc.SetpointResponse)}" +
            $"{string.Join(" ", warnings)}";
        Legend.Text = $"Mesure et consigne ({_model.PvUnit}) : bleu = Lambda, orange = SIMC, vert pointillé = consigne. " +
            $"Commandes ({_model.MvUnit}) : pointillés = commande calculée, vert = commande appliquée, gris = limites ; violet = instant de perturbation.";
        if (simulation.Robustness?.Envelope is not null)
            Legend.Text += " Sur la piste de la mesure, zones translucides = enveloppes min/max de 27 simulations, sans garantie de stabilité.";
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

    private void DrawPlot()
    {
        if (Chart is null)
            return;
        if (_shown is not { } study || study.Result.Imc.Points.Length < 3 || study.Result.Simc.Points.Length != study.Result.Imc.Points.Length)
        {
            Chart.SetTracks(Array.Empty<ChartTrack>());
            return;
        }
        var imc = study.Result.Imc.Points;
        var simc = study.Result.Simc.Points;
        var seconds = imc.Select(p => p.Seconds).ToArray();
        var setpoint = study.Scenario.Setpoint;
        var pvTrack = new ChartTrack("Variation de consigne et mesure", _model.PvUnit, new[]
        {
            new ChartSeries("Consigne", "MkrSignal3", new[] { seconds[0], seconds[^1] }, new[] { setpoint, setpoint }),
            new ChartSeries("Mesure Lambda (Dahlin)", "MkrSignal1", seconds, imc.Select(p => p.Pv).ToArray()),
            new ChartSeries("Mesure SIMC", "MkrSignal2", simc.Select(p => p.Seconds).ToArray(), simc.Select(p => p.Pv).ToArray())
        }) { Weight = 2 };
        if (study.Robustness?.Envelope is { } envelope && envelope.Seconds.Length == seconds.Length &&
            envelope.Imc.MinPv.Length == seconds.Length && envelope.Imc.MaxPv.Length == seconds.Length &&
            envelope.Simc.MinPv.Length == seconds.Length && envelope.Simc.MaxPv.Length == seconds.Length)
        {
            pvTrack.Bands.Add(new ChartBand("Enveloppe IMC", "MkrSignal1", envelope.Seconds, envelope.Imc.MinPv, envelope.Imc.MaxPv));
            pvTrack.Bands.Add(new ChartBand("Enveloppe SIMC", "MkrSignal2", envelope.Seconds, envelope.Simc.MinPv, envelope.Simc.MaxPv));
        }

        ChartTrack Commands(string title, string colorKey, SimulationPoint[] points)
        {
            var withCommand = points.Where(p => p.Cv.HasValue && p.Mv.HasValue).ToArray();
            var t = withCommand.Select(p => p.Seconds).ToArray();
            var limits = new[] { study.Scenario.MinimumMv, study.Scenario.MaximumMv };
            return new ChartTrack($"{title} · sortie de commande (écart)", _model.MvUnit, new[]
            {
                new ChartSeries("Appliquée", "MkrSignal3", t, withCommand.Select(p => p.Mv!.Value).ToArray()),
                new ChartSeries("Calculée", colorKey, t, withCommand.Select(p => p.Cv!.Value).ToArray()),
                new ChartSeries("Limite basse", "MkrPlotAxis", new[] { seconds[0], seconds[^1] }, new[] { limits[0], limits[0] }),
                new ChartSeries("Limite haute", "MkrPlotAxis", new[] { seconds[0], seconds[^1] }, new[] { limits[1], limits[1] })
            });
        }

        Chart.XLabel = "Temps depuis le début du scénario (s)";
        Chart.SetTracks(new[] { pvTrack, Commands("Lambda (Dahlin)", "MkrSignal1", imc), Commands("SIMC", "MkrSignal2", simc) });
        Chart.SetEvents(study.Scenario.Disturbance != 0 ? new[] { (study.Scenario.DisturbanceAtSeconds, "Perturbation") } : Array.Empty<(double, string)>());
    }

    private void ResetZoom(object sender, RoutedEventArgs args)
    {
        Chart.ResetView();
        ZoomStatus.Text = "Horizon complet";
    }

    private void ExportSheet(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation) return;
        var dialog = new SaveFileDialog { Title = "Exporter la fiche enregistrée", FileName = $"MKR-{simulation.Id:N}.pdf", Filter = "PDF (*.pdf)|*.pdf" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _workspace.ExportSheet(dialog.FileName, _model, _tuning, simulation);
            Message.Text = "Fiche de mise au point exportée.";
        }
        catch (Exception error)
        {
            Trace.TraceError("report.export {0}", error.GetType().Name);
            Message.Text = "Export PDF impossible. Vérifiez le dossier de destination et le journal MKR.";
        }
    }

    private void Export(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation)
            return;
        var dialog = new SaveFileDialog { Title = "Exporter les séries enregistrées", FileName = $"MKR-{simulation.Id:N}.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            SimulationCsv.Export(dialog.FileName, simulation);
            Message.Text = "Séries simulées exportées.";
        }
        catch (InvalidDataException error) { Message.Text = error.Message; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError("csv.export {0}", error.GetType().Name);
            Message.Text = "Écriture du CSV impossible. Vérifiez le dossier et si le fichier est ouvert.";
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
            "sim_domain" => "Vérifiez le modèle, le pas positif, les limites de la sortie de commande autour de zéro et l’instant de perturbation.",
            "sim_range" => "La dynamique calculée dépasse la plage numérique. Vérifiez le pas et le réglage.",
            "robust_domain" => "Variation de sensibilité invalide : indiquez 0–50 %, avec un modèle FOPDT valide.",
            "robust_input" => "Les paramètres de l'analyse de robustesse sont incomplets ou incompatibles.",
            "combined_domain" => "La variation combinée doit rester entre 0 et 50 %, avec un modèle FOPDT valide.",
            "combined_input" => "Contrat de calcul combiné invalide.",
            "combined_grid" => "Les essais combinés n'ont pas tous le même axe temporel.",
            _ => "Vérifiez les entrées et les unités du scénario."
        } : "Simulation indisponible. Vérifiez le stockage et le journal MKR.";
    }
}
