using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Guided tuning of an accepted model: robustness objective in, tuning or an explained refusal out.</summary>
public partial class GuidedTuningView : UserControl, IStepView
{
    private readonly IdentificationWorkspace _workspace;
    private readonly string _mvUnit, _pvUnit, _studyReference;
    private readonly double _noiseUpperBound;
    private readonly Func<string, double?, CancellationToken, Task<GuidedTuning>> _recommend;
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Raised by « Fermer »; the dialog host closes itself, the inline host has no use for the button (<see cref="ShowClose"/>).</summary>
    public event EventHandler? CloseRequested;
    public bool ShowClose { set => CloseButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    public void Cancel() => _lifetime.Cancel();
    private void CloseView(object sender, RoutedEventArgs args) => CloseRequested?.Invoke(this, EventArgs.Empty);

    public sealed class ComparisonRow
    {
        public string Name { get; init; } = "";
        public string Kp { get; init; } = "";
        public string Ti { get; init; } = "";
        public string Td { get; init; } = "";
        public string Ms { get; init; } = "";
        public string Iae { get; init; } = "";
        public string Overshoot { get; init; } = "";
        public string LoadIae { get; init; } = "";
        public string Noise { get; init; } = "";
    }

    private GuidedTuning? _last;

    public sealed class Row
    {
        public string Response { get; init; } = "";
        public string Kp { get; init; } = "";
        public string Ti { get; init; } = "";
        public string Td { get; init; } = "";
        public string Ms { get; init; } = "";
        public string Verdict { get; init; } = "";
    }

    internal GuidedTuningView(IdentificationWorkspace workspace, IdentificationStudy model)
        : this(workspace, Loc.T("cs.tuning_study_reference", model.Id, model.TrainingId, model.ValidationId, model.Result.AlgorithmVersion), model.MvUnit, model.PvUnit, model.Result.Training.Rmse,
            (model.Family == ModelFamily.Integrateur
                ? Loc.T("cs.modele_pente_k_s_par", model.Result.Gain, model.PvUnit, model.MvUnit, model.Result.DelaySeconds)
                : Loc.T("cs.modele_k", model.Family, model.Result.Gain, model.PvUnit, model.MvUnit) +
                  (model.Result.TauSeconds is { } tau ? $" · τ = {tau:G5} s" : "") + (model.Result.SecondTauSeconds is { } fast ? $" · τ₂ = {fast:G5} s" : "")) +
            Loc.T("cs.prudent_s", model.Result.TuningDelaySeconds),
            (objective, variation, token) => workspace.RecommendTuningAsync(model, objective, variation, token))
    {
    }

    /// <summary>From an accepted ARX / OE study (ADR 0008): the first-order equivalent of the retained (1,1) model, delay quantised to the sampling grid.</summary>
    internal GuidedTuningView(IdentificationWorkspace workspace, ArxStudy study)
        : this(workspace, Loc.T("cs.tuning_study_reference", study.Id, study.TrainingId, study.ValidationId, study.Result.AlgorithmVersion), study.MvUnit, study.PvUnit, 0,
            study.Result.Selected is { FirstOrder: { } f, Nk: var nk }
                ? Loc.T("cs.modele_arx_oe_1_1", f.Gain, study.PvUnit, study.MvUnit, f.TauSeconds, nk * study.Result.SamplePeriodSeconds, study.Result.SamplePeriodSeconds)
                : Loc.T("cs.modele_arx_oe_sans_equivalent"),
            (objective, variation, token) => workspace.RecommendTuningFromArxAsync(study, objective, variation, token))
    {
    }

    private GuidedTuningView(IdentificationWorkspace workspace, string studyReference, string mvUnit, string pvUnit, double noiseUpperBound, string modelLine,
        Func<string, double?, CancellationToken, Task<GuidedTuning>> recommend)
    {
        InitializeComponent();
        _workspace = workspace;
        _studyReference = studyReference;
        _mvUnit = mvUnit;
        _pvUnit = pvUnit;
        _noiseUpperBound = noiseUpperBound;
        _recommend = recommend;
        ModelLine.Text = modelLine;
        Chart.CursorTextChanged += text => CursorLine.Text = text.Length == 0 ? Loc.T("cs.survolez_un_graphique_pour_lire") : text;
        ObjectiveChanged(this, null!);
        Objective.SelectionChanged += (_, _) => ClearResult();
        Variation.TextChanged += (_, _) => ClearResult();
    }

    private string ObjectiveTag => (Objective.SelectedItem as ComboBoxItem)?.Tag as string ?? DesignObjectives.Balanced;

    private void ObjectiveChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ObjectiveHelp is null) return;
        ObjectiveHelp.Text = ObjectiveTag switch
        {
            DesignObjectives.Robust => Loc.T("cs.marges_larges_reponse_plus_lente"),
            DesignObjectives.Performance => Loc.T("cs.reponse_la_plus_rapide_admise"),
            _ => Loc.T("cs.compromis_courant_entre_rapidite_et")
        };
    }

    private void ClearResult()
    {
        _last = null;
        CopyButton.IsEnabled = false;
        Verdict.Visibility = ParamPanel.Visibility = ComparePanel.Visibility = AltPanel.Visibility = Visibility.Collapsed;
        CurveBar.Visibility = Chart.Visibility = Visibility.Collapsed;
        ChartEmpty.Visibility = Visibility.Visible;
        Alternatives.ItemsSource = null;
        Result.Clear();
        Notes.Text = CursorLine.Text = Status.Text = string.Empty;
    }

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        ClearResult();
        var token = _lifetime.Token;
        double? variation = null;
        if (!string.IsNullOrWhiteSpace(Variation.Text))
        {
            if (!double.TryParse(Variation.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) || !(percent > 0 && percent <= 50))
            {
                Status.Text = Loc.T("cs.la_variation_attendue_est_un");
                return;
            }
            variation = percent;
        }
        Compute.IsEnabled = Objective.IsEnabled = Variation.IsEnabled = false;
        Status.Text = Loc.T("cs.calcul_en_cours_recherche_du");
        try
        {
            var tuning = await _recommend(ObjectiveTag, variation, token);
            token.ThrowIfCancellationRequested();
            Show(tuning);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
        finally { Compute.IsEnabled = Objective.IsEnabled = Variation.IsEnabled = true; }
    }

    private static string F(double value) => value.ToString("G5", CultureInfo.CurrentCulture);
    private static string F3(double value) => value.ToString("G3", CultureInfo.CurrentCulture);

    private void Show(GuidedTuning tuning)
    {
        Verdict.Visibility = Visibility.Visible;
        CopyButton.IsEnabled = true;
        Verdict.Background = (Brush)FindResource(tuning.IsRecommended ? "MkrSuccess" : "MkrDanger");
        VerdictText.Text = tuning.IsRecommended ? Loc.T("cs.reglage_recommande_theorique") : Loc.T("cs.reglage_refuse_les_donnees_ne");
        var text = new StringBuilder();
        var mv = _mvUnit;
        var pv = _pvUnit;
        text.AppendLine(VerdictText.Text);
        text.AppendLine(_studyReference);
        text.AppendLine(ModelLine.Text);
        text.AppendLine(Loc.T("cs.tuning_algorithm_version", tuning.AlgorithmVersion));
        text.AppendLine(Loc.T("cs.objectif", Objective.Text));
        if (tuning.Recommendation is { } rec)
        {
            var c = rec.Controller;
            text.AppendLine(Loc.T("cs.structure_forme_ideale_erreur_consigne", c.Structure));
            text.AppendLine(Loc.T("cs.kp_ti_s", F(c.Kp), mv, pv, F(c.TiSeconds)) + (c.TdSeconds > 0 ? Loc.T("cs.td_s_tf_s_filtre", F(c.TdSeconds), F(c.FilterSeconds), F(tuning.DerivativeFilterRatio ?? 0)) : ""));
            text.AppendLine(Loc.T("cs.constante_de_temps_de_boucle", F(rec.ResponseSeconds), F(tuning.EffectiveDelaySeconds)));
            text.AppendLine(Loc.T("cs.sensibilite_maximale_au_pire_cas", F(rec.WorstPeakSensitivity), F(tuning.MaximumPeakSensitivity), rec.WorstCase));
            text.AppendLine(tuning.UncertaintyQuantified
                ? Loc.T("cs.incertitude_prise_en_compte") + (tuning.UncertaintyCoverageSigma is { } sigma ? Loc.T("cs.erreurs_types_sur_le_gain", sigma) : Loc.T("cs.aucune_erreur_type_disponible")) +
                  (tuning.ProcessVariationPercent is { } v ? Loc.T("cs.variation_du_procede_declaree", v) : ".")
                : Loc.T("cs.incertitude_des_parametres_non_quantifiee"));
        }
        else
        {
            text.AppendLine(Loc.T("cs.raisons"));
            foreach (var reason in tuning.Reasons ?? []) text.AppendLine("  · " + Reason(reason));
            text.AppendLine();
            text.AppendLine(Loc.T("cs.ce_qu_il_faut_faire"));
            foreach (var action in tuning.Actions ?? []) text.AppendLine("  · " + action);
            if (tuning.ClosestTuning is { } near)
                text.AppendLine(Loc.T("cs.le_plus_proche_essaye_c", F(near.ResponseSeconds), F(near.WorstPeakSensitivity), F(tuning.MaximumPeakSensitivity)));
        }
        if (tuning.Optimized is { } opt)
        {
            text.AppendLine();
            text.AppendLine(Loc.T("cs.reglage_optimise_pour_le_rejet"));
            text.AppendLine(Loc.T("cs.kp_ti_s_ki", F(opt.Controller.Kp), mv, pv, F(opt.Controller.TiSeconds), F(opt.IntegralGain)));
            text.AppendLine(Loc.T("cs.gain_integral_par_rapport_au", opt.IntegralGainRatio));
            text.AppendLine(Loc.T("cs.sensibilite_au_pire_cas_depassement", F(opt.WorstPeakSensitivity), opt.NominalOvershootPercent is { } overshootPercent ? F3(overshootPercent) : "—"));
            text.AppendLine(Loc.T("cs.compromis_integrale_plus_vive_donc"));
        }
        if (tuning.OptimizedPid is { } pid)
        {
            text.AppendLine();
            text.AppendLine(Loc.T("cs.pid_optimise_sous_la_meme"));
            text.AppendLine(Loc.T("cs.kp_ti_s_td_s", F(pid.Controller.Kp), mv, pv, F(pid.Controller.TiSeconds), F(pid.Controller.TdSeconds), F(pid.Controller.FilterSeconds), F(pid.IntegralGain)));
            text.AppendLine(Loc.T("cs.sensibilite_au_pire_cas", F(pid.WorstPeakSensitivity)) +
                (pid.IntegralGainRatioToOptimalPi is { } ratio ? Loc.T("cs.gain_integral_par_rapport_au_2", ratio) : ""));
            if (pid.PidNotBetterThanPi == true)
                text.AppendLine(Loc.T("cs.sur_ce_modele_le_pid"));
            else if (pid.PidNotBetterThanPi == false)
                text.AppendLine(Loc.T("cs.gain_obtenu_au_prix_d"));
        }
        if (tuning.Performance is { } perf)
        {
            text.AppendLine();
            text.AppendLine(Loc.T("cs.performances_nominales_simulees_echelon_unitaire"));
            void Line(string name, LoopPerformance? l)
            {
                if (l is null) return;
                text.AppendLine(Loc.T("cs.consigne_iae_depassement_etablissement_a", name, F(l.SetpointStep.Iae), l.SetpointStep.OvershootPercent is { } overshoot ? F(overshoot) : "—", l.SetpointStep.SettlingSeconds is { } settling ? F(settling) : "—", F(l.LoadDisturbanceStep.Iae), F(l.HighFrequencyGain)));
                if (l.SetpointStep.PeakMv is { } peak && l.SetpointStep.InitialMvStep is { } kick)
                    text.AppendLine(Loc.T("cs.commande_a_un_echelon_de", pv, F(kick), mv, F(peak), mv));
                if (l.NoiseToControlGain is { } noise)
                    text.AppendLine(Loc.T("cs.bruit_un_bruit_de_mesure", F(noise)) +
                        (_noiseUpperBound > 0 ? Loc.T("cs.avec_erreur_d_ajustement_borne", F(_noiseUpperBound), pv, F(noise * _noiseUpperBound), mv) : ""));
            }
            Line(Loc.T("cs.recommande"), perf.Recommended);
            Line(Loc.T("cs.pi_optimise"), perf.OptimizedPi);
            Line(Loc.T("cs.pid_optimise"), perf.OptimizedPid);
            if (new[] { perf.Recommended, perf.OptimizedPi, perf.OptimizedPid }.Any(p => p is not null && p.SetpointStep.SettlingSeconds is null))
                text.AppendLine(Loc.T("cs.indicateur_non_disponible_la_reponse"));
        }
        if (tuning.Warnings is { Length: > 0 })
        {
            text.AppendLine();
            text.AppendLine(Loc.T("cs.reserves"));
            foreach (var warning in tuning.Warnings) text.AppendLine("  · " + Warning(warning));
        }
        text.AppendLine(Loc.T("cs.resultat_theorique_limites_et_vitesse"));
        Result.Text = text.ToString();
        _last = tuning;
        Present(tuning);
        var rows = (tuning.Alternatives ?? []).Select(a => new Row
        {
            Response = F(a.ResponseSeconds), Kp = F(a.Controller.Kp), Ti = F(a.Controller.TiSeconds), Td = a.Controller.TdSeconds > 0 ? F(a.Controller.TdSeconds) : "—",
            Ms = F(a.WorstPeakSensitivity),
            Verdict = !a.StableOnAllCorners ? "instable" : a.WorstPeakSensitivity <= tuning.MaximumPeakSensitivity + 1e-9 ? Loc.T("cs.respecte_l_objectif") : Loc.T("cs.trop_sensible")
        }).ToList();
        Alternatives.ItemsSource = rows;
        AltPanel.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = tuning.IsRecommended ? Loc.T("cs.reglage_calcule") : Loc.T("cs.aucun_reglage_recommande");
    }

    private UIElement Card(string caption, string value, string? note = null)
    {
        var grid = new Grid { Margin = new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new StackPanel { Margin = new Thickness(10, 5, 8, 5) };
        var captionText = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap };
        captionText.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        label.Children.Add(captionText);
        if (note is not null) label.Children.Add(new TextBlock { Text = note, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MkrMuted") });
        grid.Children.Add(label);
        var val = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 5, 10, 5), VerticalAlignment = VerticalAlignment.Top };
        val.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        return new Border { Child = grid, BorderBrush = (Brush)FindResource("MkrBorder"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private TextBlock Cell(string text, int row, int column, bool header = false, bool emphasis = false)
    {
        var cell = new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8, 5, 8, 5), VerticalAlignment = VerticalAlignment.Center,
            FontWeight = header || column == 0 ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right
        };
        cell.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        if (header) cell.Background = (Brush)FindResource("MkrSurfaceAlt");
        else if (emphasis) cell.Background = (Brush)FindResource("MkrAccentPale");
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        return cell;
    }

    /// <summary>Structured view of the result: key values as cards, the three tunings side by side, and their simulated responses.</summary>
    private void Present(GuidedTuning tuning)
    {
        Cards.Children.Clear();
        var mv = _mvUnit;
        var pv = _pvUnit;
        var has = tuning.Recommendation is not null;
        ParamPanel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        if (tuning.Recommendation is { } rec)
        {
            var c = rec.Controller;
            Cards.Children.Add(Card("Structure", c.Structure, Loc.T("cs.forme_ideale")));
            Cards.Children.Add(Card(Loc.T("cs.gain_kp"), F(c.Kp), $"{mv}/{pv}"));
            Cards.Children.Add(Card(Loc.T("cs.temps_integral_ti"), F(c.TiSeconds) + " s"));
            if (c.TdSeconds > 0) Cards.Children.Add(Card(Loc.T("cs.temps_derive_td"), F(c.TdSeconds) + " s", Loc.T("cs.filtre_tf_s", F(c.FilterSeconds))));
            Cards.Children.Add(Card(Loc.T("cs.sensibilite_maximale_ms"), F(rec.WorstPeakSensitivity), Loc.T("cs.limite_pire_cas", F(tuning.MaximumPeakSensitivity), rec.WorstCase)));
            Cards.Children.Add(Card(Loc.T("cs.boucle_fermee_visee"), F(rec.ResponseSeconds) + " s", Loc.T("cs.retard_effectif_s", F(tuning.EffectiveDelaySeconds))));
        }

        var rows = new List<ComparisonRow>();
        void AddRow(string name, TuningController? c, double? ms, LoopPerformance? perf)
        {
            if (c is null) return;
            rows.Add(new ComparisonRow
            {
                Name = name, Kp = F3(c.Kp), Ti = F3(c.TiSeconds), Td = c.TdSeconds > 0 ? F3(c.TdSeconds) : "—", Ms = ms is { } m ? F3(m) : "—",
                Iae = perf is null ? "—" : F3(perf.SetpointStep.Iae), Overshoot = perf?.SetpointStep.OvershootPercent is { } o ? F3(o) + " %" : "—",
                LoadIae = perf is null ? "—" : F3(perf.LoadDisturbanceStep.Iae), Noise = perf?.NoiseToControlGain is { } n ? F3(n) : "—"
            });
        }
        AddRow(Loc.T("cs.recommande"), tuning.Recommendation?.Controller, tuning.Recommendation?.WorstPeakSensitivity, tuning.Performance?.Recommended);
        AddRow(Loc.T("cs.pi_optimise_rejet_de_perturbation"), tuning.Optimized?.Controller, tuning.Optimized?.WorstPeakSensitivity, tuning.Performance?.OptimizedPi);
        AddRow(Loc.T("cs.pid_optimise"), tuning.OptimizedPid?.Controller, tuning.OptimizedPid?.WorstPeakSensitivity, tuning.Performance?.OptimizedPid);
        CompareGrid.Children.Clear();
        CompareGrid.RowDefinitions.Clear();
        CompareGrid.ColumnDefinitions.Clear();
        ComparePanel.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count > 0)
        {
            CompareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            foreach (var _ in rows) CompareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lineSpecs = new (string Label, Func<ComparisonRow, string> Get)[]
            {
                ("Kp", r => r.Kp), ("Ti (s)", r => r.Ti), ("Td (s)", r => r.Td), (Loc.T("cs.ms_pire_cas"), r => r.Ms),
                (Loc.T("cs.depassement"), r => r.Overshoot), (Loc.T("cs.iae_consigne"), r => r.Iae), (Loc.T("cs.iae_charge"), r => r.LoadIae), (Loc.T("cs.bruit_commande"), r => r.Noise)
            };
            for (var i = 0; i <= lineSpecs.Length; i++) CompareGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            CompareGrid.Children.Add(Cell(Loc.T("cs.critere"), 0, 0, header: true));
            for (var column = 0; column < rows.Count; column++)
                CompareGrid.Children.Add(Cell(ShortColumnName(rows[column].Name), 0, column + 1, header: true));
            for (var line = 0; line < lineSpecs.Length; line++)
            {
                CompareGrid.Children.Add(Cell(lineSpecs[line].Label, line + 1, 0));
                for (var column = 0; column < rows.Count; column++)
                    CompareGrid.Children.Add(Cell(lineSpecs[line].Get(rows[column]), line + 1, column + 1, emphasis: column == 0));
            }
        }

        var lines = new List<string>();
        if (tuning.OptimizedPid is { PidNotBetterThanPi: true }) lines.Add(Loc.T("cs.sur_ce_modele_le_pid_2"));
        else if (tuning.OptimizedPid is { PidNotBetterThanPi: false }) lines.Add(Loc.T("cs.le_pid_rejette_mieux_les"));
        if (tuning.Warnings is { Length: > 0 }) lines.AddRange(tuning.Warnings.Select(w => Warning(w)));
        lines.Add(Loc.T("cs.resultat_theorique_limites_et_vitesse_2"));
        Notes.Text = string.Join("\n", lines.Select(l => "· " + l));
        Notes.Visibility = Visibility.Visible;
        DrawCurves();
    }

    private void ZoomIn(object sender, RoutedEventArgs args) => Chart.ZoomBy(0.7);

    private void ZoomOut(object sender, RoutedEventArgs args) => Chart.ZoomBy(1 / 0.7);

    private void ResetView(object sender, RoutedEventArgs args) => Chart.ResetView();

    private void ExportImage(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("cs.image_png_png_png"), FileName = "reglage-guide.png" };
        if (dialog.ShowDialog() != true) return;
        try { Chart.ExportPng(dialog.FileName); Status.Text = Loc.T("cs.image_exportee"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { Status.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
    }

    private void ExportData(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("cs.csv_csv_csv"), FileName = "reglage-guide.csv" };
        if (dialog.ShowDialog() != true) return;
        try { Chart.ExportCsv(dialog.FileName); Status.Text = Loc.T("cs.donnees_exportees"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { Status.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
    }

    private void CurveChanged(object sender, RoutedEventArgs args) { if (IsLoaded && _last is not null) DrawCurves(); }

    private void DrawCurves()
    {
        var performance = _last?.Performance;
        var entries = new[] { (Loc.T("cs.recommande"), performance?.Recommended, "MkrSignal1"), (Loc.T("cs.pi_optimise"), performance?.OptimizedPi, "MkrSignal2"), (Loc.T("cs.pid_optimise"), performance?.OptimizedPid, "MkrSignal3") }
            .Where(e => e.Item2?.Curves is not null).ToArray();
        CurveBar.Visibility = Chart.Visibility = entries.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ChartEmpty.Visibility = entries.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        if (entries.Length == 0) return;
        var load = ShowLoad.IsChecked == true;
        ResponseCurve Pick(LoopPerformance l) => load ? l.Curves!.Load : l.Curves!.Setpoint;
        Chart.XLabel = Loc.T("cs.temps_s");
        var measure = entries.Select(e => new ChartSeries(e.Item1, e.Item3, Pick(e.Item2!).Seconds, Pick(e.Item2!).Pv)).ToList();
        var command = entries.Select(e => new ChartSeries(e.Item1, e.Item3, Pick(e.Item2!).Seconds, Pick(e.Item2!).Mv)).ToList();
        Chart.SetTracks(new[]
        {
            new ChartTrack(load ? Loc.T("cs.apercu_de_la_reponse_perturbation") : Loc.T("cs.apercu_de_la_reponse_echelon"), _pvUnit, measure) { Weight = 2, YLabel = Loc.T("cs.mesure", _pvUnit) },
            new ChartTrack(Loc.T("cs.commande_sortie_du_regulateur"), _mvUnit, command) { YLabel = $"MV ({_mvUnit})" }
        });
    }

    private static string Reason(string code) => code switch
    {
        "parameter_uncertainty_excludes_the_model" => Loc.T("cs.l_incertitude_des_parametres_est"),
        "no_tuning_meets_the_objective_on_the_uncertainty_set" => Loc.T("cs.aucun_reglage_ne_respecte_l"),
        _ => code
    };

    private static string Warning(string code) => code switch
    {
        "uncertainty_not_quantified" => Loc.T("cs.incertitude_des_parametres_non_quantifiee_2"),
        "closed_loop_slower_than_the_process" => Loc.T("cs.la_boucle_fermee_visee_est"),
        "pid_second_pole_exceeds_delay" => Loc.T("cs.second_pole_superieur_au_retard"),
        "performance_is_nominal_without_actuator_limits" => Loc.T("cs.performances_calculees_sur_le_procede"),
        "second_pole_uncertainty_not_propagated" => Loc.T("cs.l_incertitude_du_second_pole"),
        "half_rule_second_pole_not_above_delay" => Loc.T("cs.second_pole_non_superieur_au"),
        "first_order_model_pi_is_the_rule" => Loc.T("cs.modele_du_premier_ordre_le"),
        "integrating_process_simc" => Loc.T("cs.regle_simc_des_procedes_integrateurs"),
        _ => code
    };

    private void Copy(object sender, RoutedEventArgs args)
    {
        try { Clipboard.SetText(Result.Text); Status.Text = Loc.T("cs.resultat_copie"); }
        catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException) { Status.Text = Loc.T("cs.copie_impossible_le_presse_papiers"); }
    }

    /// <summary>Shorter column titles of the comparison table (the full names stay in the other lists).</summary>
    private static string ShortColumnName(string name) =>
        name == Loc.T("cs.pi_optimise_rejet_de_perturbation") ? Loc.T("cs.pi_optimise")
        : name == Loc.T("cs.recommande") ? Loc.T("tbl.recommended_short") : name;
}
