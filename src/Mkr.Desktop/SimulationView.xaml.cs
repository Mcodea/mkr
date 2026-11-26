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

public partial class SimulationView : UserControl, IStepView
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly PiStudy _tuning;
    private readonly CancellationTokenSource _lifetime = new();

    public void Cancel() { _lifetime.Cancel(); _lifetime.Dispose(); }
    private PiSimulationStudy? _shown;

    internal SimulationView(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        _workspace = workspace;
        _model = model;
        _tuning = tuning;
        InitializeComponent();
        Chart.CursorTextChanged += text => CursorReadout.Text = text.Length == 0 ? Loc.T("cs.survolez_le_graphique_pour_lire") : text;
        Chart.ViewChanged += () => ZoomStatus.Text = Loc.T("cs.vue_s", Chart.View.From, Chart.View.To);
        RateLabel.Content = Loc.T("cs.vitesse_max_de_la_sortie", model.MvUnit);
        Units.Text = Loc.T("cs.cible_pi_s_deviations_autour", tuning.Result.ResponseSeconds, model.PvUnit, model.MvUnit);
        Saved.ItemsSource = _workspace.Simulations(_tuning.Id);
        if (Saved.Items.Count > 0)
            Saved.SelectedIndex = 0;
    }

    private bool TryReadScenario(out PiScenario scenario)
    {
        scenario = null!;
        if (!Read(Sample, out var sample) || !Read(Duration, out var duration) ||
            !Read(Setpoint, out var setpoint) || !Read(Lower, out var lower) ||
            !Read(Upper, out var upper) || !Read(Disturbance, out var disturbance) ||
            !Read(DisturbanceAt, out var at))
        {
            Message.Text = Loc.T("cs.saisissez_les_sept_valeurs_numeriques");
            return false;
        }
        double? rate = null;
        if (!string.IsNullOrWhiteSpace(Rate.Text))
        {
            if (!Read(Rate, out var maximumRate) || maximumRate <= 0)
            {
                Message.Text = Loc.T("cs.la_vitesse_maximale_de_la");
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
            Message.Text = Loc.T("cs.renseignez_la_commande_manuelle_et");
            return;
        }
        var token = _lifetime.Token;
        bool simc = TransferTune.SelectedIndex == 1;
        ScenarioForm.IsEnabled = false;
        Message.Text = Loc.T("cs.simulation_du_transfert_en_cours");
        try
        {
            var transfer = await _workspace.TransferAsync(_model, _tuning, simc, scenario, manual, at, token);
            token.ThrowIfCancellationRequested();
            var first = Array.FindIndex(transfer.Points, p => p.Mode == "automatic");
            var before = transfer.Points[first - 1];
            var after = transfer.Points[first];
            string method = simc ? "SIMC" : Loc.T("cs.lambda_dahlin");
            string warnings = string.Join(", ", transfer.Warnings);
            Message.Text = Loc.T("cs.essai_hors_ligne_a_s", method, after.Seconds, manual, _model.MvUnit, before.Mv, after.Mv, _model.MvUnit, transfer.TransferBumpMv, _model.MvUnit, after.Pv, _model.PvUnit, transfer.SaturationSeconds, transfer.RateLimitedSeconds, warnings);
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
            { Owner = Window.GetWindow(this) }.ShowDialog();
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
        Message.Text = Loc.T("cs.simulation_en_cours");
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.SimulateAsync(_model, _tuning, scenario, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = Loc.T("cs.simulation_enregistree");
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
            Message.Text = Loc.T("cs.indiquez_une_variation_de_sensibilite");
            return;
        }
        ScenarioForm.IsEnabled = false;
        Message.Text = Loc.T("cs.calcul_de_sept_scenarios_de");
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.AnalyzeRobustnessAsync(
                _model, _tuning, scenario, variation, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = Loc.T("cs.analyse_de_sensibilite_enregistree_avec");
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
            Message.Text = Loc.T("cs.indiquez_une_variation_superieure_a");
            return;
        }
        ScenarioForm.IsEnabled = false;
        Message.Text = Loc.T("cs.calcul_de_la_matrice_de");
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.AnalyzeCombinedAsync(
                _model, _tuning, scenario, variation, token);
            var list = _workspace.Simulations(_tuning.Id);
            Saved.ItemsSource = list;
            Saved.SelectedItem = list.First(item => item.Id == study.Id);
            Message.Text = Loc.T("cs.matrice_combinee_enregistree_avec_l");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { ScenarioForm.IsEnabled = true; }
    }

    private void ShowSaved(object sender, SelectionChangedEventArgs args)
    {
        _shown = Saved.SelectedItem as PiSimulationStudy;
        ZoomStatus.Text = Loc.T("cs.horizon_complet");
        CursorReadout.Text = Loc.T("cs.survolez_le_graphique_pour_lire");
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
                    "gain_minus" => Loc.T("cs.gain"),
                    "gain_plus" => Loc.T("cs.gain_2"),
                    "tau_minus" => Loc.T("cs.constante_2"),
                    "tau_plus" => Loc.T("cs.constante_3"),
                    "delay_minus" => Loc.T("cs.retard_2"),
                    "delay_plus" => Loc.T("cs.retard_3"),
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
                ? Loc.T("cs.grille_combinee_k_27_procedes")
                : Loc.T("cs.variation_individuelle_de_k_ou");
            RobustnessSummary.Text =
                Loc.T("cs.coefficients_pi_fixes_iae_maximale", mode, sensitivity.VariationPercent, sensitivity.Cases.Length, worstImc, _model.PvUnit, worstSimc, _model.PvUnit) +
                (isCombined ? Loc.T("cs.les_enveloppes_de_la_mesure") : "") +
                Loc.T("cs.methode_modele_nominal_conserve", sensitivity.AlgorithmVersion);
        }
        else
        {
            RobustnessGrid.ItemsSource = null;
            RobustnessSummary.Text = "";
        }
        if (_shown is not { } simulation)
        {
            Report.Text = Legend.Text = "";
            IndicatorGrid.Children.Clear();
            IndicatorNote.Visibility = Visibility.Collapsed;
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
            "coarse_sample" => Loc.T("cs.pas_superieur_au_dixieme_de"),
            "delay_beyond_horizon" => Loc.T("cs.le_retard_atteint_ou_depasse"),
            _ => Loc.T("cs.simulation_exploratoire_sur_modele_nominal")
        });
        Report.Text = $"Simulation enregistrée {simulation.Id.ToString()[..8]} · durée {input.DurationSeconds:G6} s · " +
            $"Variation de consigne {input.Setpoint:G6} {_model.PvUnit} · pas {input.SampleSeconds:G6} s\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {simulation.EngineVersion}\n" +
            $"Lambda : {Metrics(result.Imc)}\nSIMC : {Metrics(result.Simc)}\n" +
            $"IMC : {ResponseWindow(result.Imc.SetpointResponse)}" +
            $"SIMC : {ResponseWindow(result.Simc.SetpointResponse)}" +
            $"{string.Join(" ", warnings)}";
        BuildIndicators(result);
        Legend.Text = Loc.T("cs.mesure_et_consigne_bleu_lambda", _model.PvUnit, _model.MvUnit);
        if (simulation.Robustness?.Envelope is not null)
            Legend.Text += Loc.T("cs.sur_la_piste_de_la");
        DrawPlot();
    }

    // Figures and the exported series are generated from the same persisted simulation runs.
    // A historical study is displayed without re-running a control algorithm.
    private string Metrics(SimulationRun run) =>
        Loc.T("cs.iae_s_ise_2s_saturation", run.Iae, _model.PvUnit, run.Ise, _model.PvUnit, run.SaturationSeconds, run.RateLimitedSeconds, run.FrozenSteps, run.FinalError, _model.PvUnit) +
        (run.SetpointResponse is { } response
            ? $" · dépassement {(response.OvershootPv is { } peak ? $"{peak:G5} {_model.PvUnit}" : "—")}" +
              $" · temps de réponse ±5 % {(response.SettlingSeconds is { } time ? $"{time:G5} s" : "—")}"
            : "");

    private static string ResponseWindow(SetpointResponse? response) => response is null
        ? ""
        : Loc.T("cs.fenetre_de_reponse_a_la", response.WindowEndSeconds);

    private System.Windows.Controls.TextBlock IndicatorCell(string text, int row, int column, bool header = false)
    {
        var cell = new System.Windows.Controls.TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
            FontWeight = header || column == 0 ? FontWeights.SemiBold : FontWeights.Normal, TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right
        };
        cell.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MkrText");
        if (header) cell.SetResourceReference(System.Windows.Controls.TextBlock.BackgroundProperty, "MkrSurfaceAlt");
        System.Windows.Controls.Grid.SetRow(cell, row);
        System.Windows.Controls.Grid.SetColumn(cell, column);
        return cell;
    }

    /// <summary>The persisted indicators of the two compared methods as a table (units in the headers; « — » = not available, explained below).</summary>
    private void BuildIndicators(PiSimulationResult result)
    {
        IndicatorGrid.Children.Clear();
        IndicatorGrid.RowDefinitions.Clear();
        IndicatorGrid.ColumnDefinitions.Clear();
        IndicatorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        for (var i = 0; i < 4; i++) IndicatorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++) IndicatorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var headers = new[] { Loc.T("cs.methode"), Loc.T("cs.iae_s", _model.PvUnit), Loc.T("cs.depass", _model.PvUnit), Loc.T("cs.rep_5_s"), Loc.T("cs.satur_s") };
        for (var c = 0; c < headers.Length; c++) IndicatorGrid.Children.Add(IndicatorCell(headers[c], 0, c, header: true));
        var unavailable = false;
        string Num(double? value) { if (value is null) { unavailable = true; return "—"; } return value.Value.ToString("G4", CultureInfo.CurrentCulture); }
        void Row(int row, string name, SimulationRun run)
        {
            IndicatorGrid.Children.Add(IndicatorCell(name, row, 0));
            IndicatorGrid.Children.Add(IndicatorCell(Num(run.Iae), row, 1));
            IndicatorGrid.Children.Add(IndicatorCell(Num(run.SetpointResponse?.OvershootPv), row, 2));
            IndicatorGrid.Children.Add(IndicatorCell(Num(run.SetpointResponse?.SettlingSeconds), row, 3));
            IndicatorGrid.Children.Add(IndicatorCell(Num(run.SaturationSeconds), row, 4));
        }
        Row(1, Loc.T("cs.lambda_dahlin"), result.Imc);
        Row(2, "SIMC", result.Simc);
        IndicatorNote.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        IndicatorNote.Text = unavailable ? Loc.T("cs.indicateur_non_disponible_la_reponse") : "";
    }

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
        var pvTrack = new ChartTrack(Loc.T("cs.simulation_variation_de_consigne_et"), _model.PvUnit, new[]
        {
            new ChartSeries(Terms.Setpoint, "MkrSignal3", new[] { seconds[0], seconds[^1] }, new[] { setpoint, setpoint }),
            new ChartSeries(Loc.T("cs.mesure_lambda_dahlin"), "MkrSignal1", seconds, imc.Select(p => p.Pv).ToArray()),
            new ChartSeries(Loc.T("cs.mesure_simc"), "MkrSignal2", simc.Select(p => p.Seconds).ToArray(), simc.Select(p => p.Pv).ToArray())
        }) { Weight = 2, YLabel = Loc.T("cs.mesure", _model.PvUnit) };
        if (study.Robustness?.Envelope is { } envelope && envelope.Seconds.Length == seconds.Length &&
            envelope.Imc.MinPv.Length == seconds.Length && envelope.Imc.MaxPv.Length == seconds.Length &&
            envelope.Simc.MinPv.Length == seconds.Length && envelope.Simc.MaxPv.Length == seconds.Length)
        {
            pvTrack.Bands.Add(new ChartBand(Loc.T("cs.enveloppe_imc"), "MkrSignal1", envelope.Seconds, envelope.Imc.MinPv, envelope.Imc.MaxPv));
            pvTrack.Bands.Add(new ChartBand(Loc.T("cs.enveloppe_simc"), "MkrSignal2", envelope.Seconds, envelope.Simc.MinPv, envelope.Simc.MaxPv));
        }

        ChartTrack Commands(string title, string colorKey, SimulationPoint[] points)
        {
            var withCommand = points.Where(p => p.Cv.HasValue && p.Mv.HasValue).ToArray();
            var t = withCommand.Select(p => p.Seconds).ToArray();
            var limits = new[] { study.Scenario.MinimumMv, study.Scenario.MaximumMv };
            return new ChartTrack(Loc.T("cs.sortie_de_commande_ecart", title), _model.MvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.appliquee"), "MkrSignal3", t, withCommand.Select(p => p.Mv!.Value).ToArray()),
                new ChartSeries(Loc.T("cs.calculee"), colorKey, t, withCommand.Select(p => p.Cv!.Value).ToArray()),
                new ChartSeries(Loc.T("cs.limite_basse"), "MkrPlotAxis", new[] { seconds[0], seconds[^1] }, new[] { limits[0], limits[0] }),
                new ChartSeries(Loc.T("cs.limite_haute"), "MkrPlotAxis", new[] { seconds[0], seconds[^1] }, new[] { limits[1], limits[1] })
            }) { YLabel = $"MV ({_model.MvUnit})" };
        }

        Chart.XLabel = Loc.T("cs.temps_depuis_le_debut_du");
        Chart.SetTracks(new[] { pvTrack, Commands(Loc.T("cs.lambda_dahlin"), "MkrSignal1", imc), Commands("SIMC", "MkrSignal2", simc) });
        Chart.SetEvents(study.Scenario.Disturbance != 0 ? new[] { (study.Scenario.DisturbanceAtSeconds, "Perturbation") } : Array.Empty<(double, string)>());
    }

    private void ZoomIn(object sender, RoutedEventArgs args) => Chart.ZoomBy(0.7);

    private void ZoomOut(object sender, RoutedEventArgs args) => Chart.ZoomBy(1 / 0.7);

    private void ResetZoom(object sender, RoutedEventArgs args)
    {
        Chart.ResetView();
        ZoomStatus.Text = Loc.T("cs.horizon_complet");
    }

    private void ExportSheet(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation) return;
        var dialog = new SaveFileDialog { Title = Loc.T("cs.exporter_la_fiche_enregistree"), FileName = $"MKR-{simulation.Id:N}.pdf", Filter = Loc.T("cs.pdf_pdf_pdf") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            _workspace.ExportSheet(dialog.FileName, _model, _tuning, simulation);
            Message.Text = Loc.T("cs.fiche_de_mise_au_point");
        }
        catch (Exception error)
        {
            Trace.TraceError("report.export {0}", error.GetType().Name);
            Message.Text = Loc.T("cs.export_pdf_impossible_verifiez_le");
        }
    }

    private void Export(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } simulation)
            return;
        var dialog = new SaveFileDialog { Title = Loc.T("cs.exporter_les_series_enregistrees"), FileName = $"MKR-{simulation.Id:N}.csv", Filter = Loc.T("cs.csv_csv_csv") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            SimulationCsv.Export(dialog.FileName, simulation);
            Message.Text = Loc.T("cs.series_simulees_exportees");
        }
        catch (InvalidDataException error) { Message.Text = error.Message; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError("csv.export {0}", error.GetType().Name);
            Message.Text = Loc.T("cs.ecriture_du_csv_impossible_verifiez");
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
            "sim_grid" => Loc.T("cs.utilisez_une_duree_et_un_2"),
            "sim_domain" => Loc.T("cs.verifiez_le_modele_le_pas"),
            "sim_range" => Loc.T("cs.la_dynamique_calculee_depasse_la"),
            "robust_domain" => Loc.T("cs.variation_de_sensibilite_invalide_indiquez"),
            "robust_input" => Loc.T("cs.les_parametres_de_l_analyse"),
            "combined_domain" => Loc.T("cs.la_variation_combinee_doit_rester"),
            "combined_input" => Loc.T("cs.contrat_de_calcul_combine_invalide"),
            "combined_grid" => Loc.T("cs.les_essais_combines_n_ont"),
            _ => Loc.T("cs.verifiez_les_entrees_et_les")
        } : Loc.T("cs.simulation_indisponible_verifiez_le_stockage");
    }
}
