using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class IdentificationView : UserControl, IStepView
{
    private readonly IdentificationWorkspace _workspace;
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Raised when a study or a tuning has been saved, so that a hosting workspace can refresh its own table and journey.</summary>
    public event EventHandler? StudiesChanged;
    public void Cancel() { _lifetime.Cancel(); _lifetime.Dispose(); }
    private IdentificationStudy? _shown;
    private sealed record WindowChoice(Guid ImportId, StepColumns Columns, StepWindow Window);
    private WindowChoice? _trainingSelection;
    private WindowChoice? _validationSelection;
    private readonly IReadOnlyList<ImportSummary> _sources;

    internal IdentificationView(IdentificationWorkspace workspace, IdentificationStudy? initial = null)
    {
        _workspace = workspace;
        InitializeComponent();
        var charts = new[] { Chart, CommandChart, ResidualChart };
        foreach (var chart in charts)
        {
            var self = chart;
            self.RangeSelected += (from, to) => { _window = from < to ? (from, to) : (to, from); DrawPlot(); };
            // the three charts share one time axis and one cursor
            self.ViewChanged += () =>
            {
                if (_syncing) return;
                _syncing = true;
                foreach (var other in charts.Where(other => !ReferenceEquals(other, self))) other.SetView(self.View.From, self.View.To);
                _syncing = false;
            };
            self.CursorMoved += x => { foreach (var other in charts.Where(other => !ReferenceEquals(other, self))) other.SetExternalCursor(x); };
            self.CursorTextChanged += text =>
            {
                CursorText.Text = text;
                CursorText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;     // the cursor values replace the legend line while hovering
                PlotLegend.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        SizeChanged += (_, e) => { if (e.PreviousSize.Height == 0 || (e.PreviousSize.Height >= 620) != (e.NewSize.Height >= 620)) ModelPanel.IsExpanded = true; };
        FamilyChoice.ItemsSource = Enum.GetValues<ModelFamily>();
        FamilyChoice.SelectedItem = ModelFamily.FOPDT;
        _sources = _workspace.Sources();
        TrainingSource.ItemsSource = ValidationSource.ItemsSource = _sources;
        var studies = _workspace.Studies();
        StudyList.ItemsSource = studies;
        // The active study is shown at once: the one selected in the workspace, else the most recent accepted one, else the most recent.
        var first = initial is null ? null : studies.FirstOrDefault(item => item.Id == initial.Id);
        first ??= studies.FirstOrDefault(item => item.IsAcceptedForTuning) ?? studies.FirstOrDefault();
        if (first is not null) StudyList.SelectedItem = first;
        TrialsPanel.IsExpanded = true;      // expanded: the trials form must stay reachable (and testable) like before; the chart is unaffected
        if (_sources.Count < 2)
            Message.Text = Loc.T("cs.importez_deux_essais_csv_dans");
    }

    private void SelectTraining(object sender, SelectionChangedEventArgs args)
    {
        _trainingSelection = null;
        if (TrainingRangeInfo is not null)
            TrainingRangeInfo.Text = Loc.T("cs.estimation_fichier_complet");
        Populate(TrainingSource, TrainingTime, TrainingMv, TrainingPv);
    }

    private void SelectValidation(object sender, SelectionChangedEventArgs args)
    {
        _validationSelection = null;
        if (ValidationRangeInfo is not null)
            ValidationRangeInfo.Text = Loc.T("cs.validation_fichier_complet");
        Populate(ValidationSource, ValidationTime, ValidationMv, ValidationPv);
    }

    private void Populate(ComboBox source, ComboBox time, ComboBox mv, ComboBox pv)
    {
        try
        {
            Confirmed.IsChecked = false;
            var saved = source.SelectedItem is ImportSummary item ? _workspace.ReadSource(item) : null;
            var mapping = saved?.Mapping;
            var acquisition = saved?.Acquisition;
            time.ItemsSource = mv.ItemsSource = pv.ItemsSource = saved?.Result.Headers;
            time.SelectedItem = mapping?.TimeColumn;
            mv.SelectedItem = acquisition is not null ? "MV" : mapping?.Role == "MV" ? mapping.ValueColumn : null;
            pv.SelectedItem = acquisition is not null ? "PV" : mapping?.Role == "PV" ? mapping.ValueColumn : null;
            time.IsEnabled = mv.IsEnabled = pv.IsEnabled = acquisition is null;
            if (source == TrainingSource)
            {
                MvUnit.Text = acquisition?.CommandUnit ?? (mapping?.Role == "MV" ? mapping.Unit : "");
                PvUnit.Text = acquisition?.ProcessUnit ?? (mapping?.Role == "PV" ? mapping.Unit : "");
            }
        }
        catch (Exception error) { ShowError(error); }
    }

    private async void FindTrialsInRecording(object sender, RoutedEventArgs args)
    {
        var columns = ReadColumns(TrainingTime, TrainingMv, TrainingPv);
        if (TrainingSource.SelectedItem is not ImportSummary import || columns is null ||
            MvUnit.Text.Trim().Length == 0 || PvUnit.Text.Trim().Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(PvTolerance, out var pvTolerance))
        {
            Message.Text = Loc.T("cs.choisissez_lenregistrement_ses_trois_colonnes");
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = Loc.T("cs.recherche_des_echelons_exploitables");
        var token = _lifetime.Token;
        try
        {
            var search = await _workspace.FindStepsAsync(import, columns, MvUnit.Text.Trim(), PvUnit.Text.Trim(),
                mvTolerance, pvTolerance, token);
            token.ThrowIfCancellationRequested();
            Message.Text = Loc.T("cs.echelon_s_exploitable_s_ecarte", search.Candidates.Length, search.Rejected.Length);
            if (search.ModeColumn is not null)
                Message.Text += Loc.T("cs.colonne_utilisee_ligne_s_hors", search.ModeColumn, search.RowsNotInManual);
            var window = new StepSearchWindow(search, import.FileName, MvUnit.Text.Trim(), PvUnit.Text.Trim()) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true || window.SelectedPair is not { } pair) return;
            // Both trials come from this recording: select it for validation too, then set the two explicit ranges.
            ValidationSource.SelectedItem = import;
            ValidationTime.SelectedItem = columns.TimeColumn;
            ValidationMv.SelectedItem = columns.MvColumn;
            ValidationPv.SelectedItem = columns.PvColumn;
            _trainingSelection = new WindowChoice(import.Id, columns, pair.FirstWindow);
            _validationSelection = new WindowChoice(import.Id, columns, pair.SecondWindow);
            TrainingRangeInfo.Text = Loc.T("cs.estimation_indices_source_inclus_echelon", pair.FirstWindow.RowStart, pair.FirstWindow.RowEnd, pair.FirstId);
            ValidationRangeInfo.Text = Loc.T("cs.validation_indices_source_inclus_echelon", pair.SecondWindow.RowStart, pair.SecondWindow.RowEnd, pair.SecondId);
            Confirmed.IsChecked = false;
            Message.Text = Loc.T("cs.deux_plages_independantes_retenues_dans");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private async void PreviewTraining(object sender, RoutedEventArgs args) =>
        await PreviewSourceAsync(TrainingSource, TrainingTime, TrainingMv, TrainingPv, true);

    private async void PreviewValidation(object sender, RoutedEventArgs args) =>
        await PreviewSourceAsync(ValidationSource, ValidationTime, ValidationMv, ValidationPv, false);

    private async Task PreviewSourceAsync(ComboBox source, ComboBox time, ComboBox mv, ComboBox pv, bool training)
    {
        if (source.SelectedItem is not ImportSummary import ||
            ReadColumns(time, mv, pv) is not { } columns)
        {
            Message.Text = Loc.T("cs.selectionnez_un_import_et_trois");
            return;
        }

        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = Loc.T("cs.lecture_des_courbes_brutes_en");
        var token = _lifetime.Token;
        try
        {
            var preview = await _workspace.PreviewTrialAsync(import, columns, token);
            if (token.IsCancellationRequested)
                return;
            Message.Text = Loc.T("cs.courbes_chargees_depuis_le_csv");
            var window = new TrialPreviewWindow(preview, import.FileName,
                string.IsNullOrWhiteSpace(MvUnit.Text) ? Loc.T("cs.unite_de_la_sortie_de") : MvUnit.Text.Trim(),
                string.IsNullOrWhiteSpace(PvUnit.Text) ? Loc.T("cs.unite_de_la_mesure_non") : PvUnit.Text.Trim())
                { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true && window.SelectedWindow is { } range)
            {
                var choice = new WindowChoice(import.Id, columns, range);
                var text = Loc.T("cs.lignes_originales_a_indices_0", range.RowStart + 1, range.RowEnd + 1, range.RowStart, range.RowEnd);
                if (training)
                {
                    _trainingSelection = choice;
                    TrainingRangeInfo.Text = Loc.T("cs.estimation") + text;
                }
                else
                {
                    _validationSelection = choice;
                    ValidationRangeInfo.Text = Loc.T("cs.validation") + text;
                }
                Confirmed.IsChecked = false;
                Message.Text = Loc.T("cs.plage_retenue_verifiez_les_conditions");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private async void ProposeCriteria(object sender, RoutedEventArgs args)
    {
        var trainColumns = ReadColumns(TrainingTime, TrainingMv, TrainingPv);
        var valColumns = ReadColumns(ValidationTime, ValidationMv, ValidationPv);
        if (TrainingSource.SelectedItem is not ImportSummary training || ValidationSource.SelectedItem is not ImportSummary validation ||
            trainColumns is null || valColumns is null || MvUnit.Text.Trim().Length == 0 || PvUnit.Text.Trim().Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(PvTolerance, out var pvTolerance))
        {
            Message.Text = Loc.T("cs.choisissez_les_deux_essais_leurs");
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var proposal = await _workspace.SuggestCriteriaAsync(training, trainColumns, validation, valColumns,
                MvUnit.Text.Trim(), PvUnit.Text.Trim(), mvTolerance, pvTolerance, token,
                _trainingSelection?.Window, _validationSelection?.Window);
            token.ThrowIfCancellationRequested();
            MaximumRmse.Text = proposal.MaximumRmse.ToString("G4", CultureInfo.CurrentCulture);
            MaximumLag.Text = proposal.MaximumResidualLagOne.ToString("G4", CultureInfo.CurrentCulture);
            Message.Text = Loc.T("cs.bruit_mesure_sur_les_plateaux", proposal.BaselineNoise, PvUnit.Text) +
                (proposal.Basis == "noise"
                    ? Loc.T("cs.rmse_maximale", proposal.NoiseMultiple)
                    : Loc.T("cs.bruit_inferieur_a_la_resolution")) +
                Loc.T("cs.ces_criteres_sont_fixes_avant");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private ArxStudy? _lastArx;

    private void TuneFromArx(object sender, RoutedEventArgs args)
    {
        if (_lastArx is not { } study) return;
        if (!study.IsAccepted)
        {
            Message.Text = Loc.T("cs.reglage_impossible_l_etude_arx");
            return;
        }
        if (study.Result.Selected?.FirstOrder is null)
        {
            Message.Text = Loc.T("cs.reglage_impossible_le_modele_arx");
            return;
        }
        try { new GuidedTuningWindow(_workspace, study) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private async void IdentifyArx(object sender, RoutedEventArgs args)
    {
        var trainColumns = ReadColumns(TrainingTime, TrainingMv, TrainingPv);
        var valColumns = ReadColumns(ValidationTime, ValidationMv, ValidationPv);
        var mvUnit = MvUnit.Text.Trim();
        var pvUnit = PvUnit.Text.Trim();
        if (TrainingSource.SelectedItem is not ImportSummary training || ValidationSource.SelectedItem is not ImportSummary validation ||
            trainColumns is null || valColumns is null || mvUnit.Length == 0 || pvUnit.Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(MaximumRmse, out var rmse) || Confirmed.IsChecked != true)
        {
            Message.Text = Loc.T("cs.indiquez_deux_imports_leurs_colonnes");
            return;
        }
        if (TrialIndependence.Check(training, _trainingSelection?.Window, validation, _validationSelection?.Window) is { })
        {
            Message.Text = Loc.T("cs.les_essais_d_estimation_et");
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = Loc.T("cs.identification_arx_sortie_erreur");
        var token = _lifetime.Token;
        try
        {
            var saved = await _workspace.IdentifyArxAsync(training, trainColumns, validation, valColumns, mvUnit, pvUnit,
                mvTolerance, rmse, token, _trainingSelection?.Window, _validationSelection?.Window);
            token.ThrowIfCancellationRequested();
            _lastArx = saved;
            ArxTuneButton.IsEnabled = true;
            Message.Text = saved.Result.Describe(mvUnit, pvUnit) + $"Étude ARX enregistrée ({saved.Id.ToString()[..8]}), {(saved.IsAccepted ? "acceptée" : "refusée : aucun réglage ne peut en être déduit")}.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        var trainColumns = ReadColumns(TrainingTime, TrainingMv, TrainingPv);
        var valColumns = ReadColumns(ValidationTime, ValidationMv, ValidationPv);
        var mvUnit = MvUnit.Text.Trim();
        var pvUnit = PvUnit.Text.Trim();
        if (FamilyChoice.SelectedItem is not ModelFamily family || TrainingSource.SelectedItem is not ImportSummary training ||
            ValidationSource.SelectedItem is not ImportSummary validation ||
            trainColumns is null || valColumns is null || mvUnit.Length == 0 || pvUnit.Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(PvTolerance, out var pvTolerance) ||
            Confirmed.IsChecked != true)
        {
            Message.Text = Loc.T("cs.indiquez_deux_imports_trois_colonnes");
            return;
        }

        if (training.Id == validation.Id &&
            TrialIndependence.Check(training, _trainingSelection?.Window, validation, _validationSelection?.Window) is { } same)
        {
            Message.Text = same == "fit_same_source_overlap"
                ? Loc.T("cs.les_deux_plages_se_recouvrent")
                : Loc.T("cs.le_meme_import_est_selectionne");
            return;
        }

        // A saved choice belongs to a specific original import and exact column mapping.
        // Never silently apply a stale range to newly selected data.
        if ((_trainingSelection is { } tr &&
                (tr.ImportId != training.Id || tr.Columns != trainColumns)) ||
            (_validationSelection is { } va &&
                (va.ImportId != validation.Id || va.Columns != valColumns)))
        {
            Message.Text = Loc.T("cs.les_colonnes_ou_l_import");
            return;
        }

        // Validation criteria are mandatory for every estimate, not only for the comparison (audit NC-02).
        if (!ReadTolerance(MaximumRmse, out var rmse) || !ReadTolerance(MaximumLag, out var lag) || lag > 1)
        {
            Message.Text = Loc.T("cs.indiquez_les_criteres_de_validation");
            return;
        }
        var criteria = new ComparisonCriteria(rmse, lag);
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = Loc.T("cs.estimation_et_evaluation");
        var token = _lifetime.Token;
        try
        {
            ModelComparison? accepted = null;
            if (sender == CompareModels)
            {
                var comparison = await _workspace.CompareAsync(training, trainColumns, validation, valColumns, mvUnit, pvUnit,
                    mvTolerance, pvTolerance, rmse, lag, token, _trainingSelection?.Window, _validationSelection?.Window);
                token.ThrowIfCancellationRequested();
                var lines = new List<string> { Loc.T("cs.essai_de_validation", validation.FileName),
                    Loc.T("cs.seuils_rmse_r1", rmse, pvUnit, lag) };
                lines.AddRange(CandidateLines(comparison, pvUnit));
                lines.Add(comparison.Selected is { } selected
                    ? comparison.Decision == "significantly_better_higher_order" && comparison.HoldoutTest is { } test
                        ? Loc.T("cs.modele_propose_les_deux_modeles", selected, test.MseRatio, test.Critical, test.Level)
                        : Loc.T("cs.modele_propose_ordre_le_plus", selected)
                    : Loc.T("cs.aucun_des_deux_modeles_ne"));
                Message.Text = lines[^1];
                if (comparison.Selected is null)
                {
                    lines.Add(Loc.T("cs.avant_un_nouvel_essai_examinez"));
                    Message.Text = string.Join(Environment.NewLine, lines);
                    return;
                }
                lines.Add(Loc.T("cs.estimer_et_enregistrer_ce_modele"));
                if (MessageBox.Show(Window.GetWindow(this), string.Join(Environment.NewLine, lines), Loc.T("cs.comparaison_sur_essai_independant"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                family = Enum.Parse<ModelFamily>(comparison.Selected);
                FamilyChoice.SelectedItem = family;
                accepted = comparison;
            }
            var study = await _workspace.CalculateAsync(family, training, trainColumns, validation, valColumns,
                mvUnit, pvUnit, mvTolerance, pvTolerance, criteria, token,
                _trainingSelection?.Window, _validationSelection?.Window, accepted);
            var studies = _workspace.Studies();
            StudyList.ItemsSource = studies;
            StudyList.SelectedItem = studies.First(item => item.Id == study.Id);
            Message.Text = study.IsAcceptedForTuning
                ? Loc.T("cs.etude_exploratoire_enregistree")
                : Loc.T("cs.etude_enregistree_mais_refusee_a") +
                  string.Join(", ", study.Assessment!.Reasons.Select(ReasonText)) +
                  Loc.T("cs.aucun_reglage_ne_peut_en");
            StudiesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private static string ReasonText(string reason) => reason switch
    {
        "validation_rmse" => Loc.T("cs.erreur_de_validation_excessive"),
        "validation_residuals" => Loc.T("cs.correlation_residuelle_excessive_ou_indeterminee"),
        "parameter_bound" => Loc.T("cs.parametre_en_borne"),
        "short_horizon" => Loc.T("cs.duree_dessai_insuffisante"),
        "fast_pole_resolution" => Loc.T("cs.pole_rapide_mal_resolu"),
        "ill_conditioned" => Loc.T("cs.estimation_mal_conditionnee"),
        "integrator_validation_tail" => Loc.T("cs.essai_de_validation_non_integrant"),
        _ => reason
    };

    private static IEnumerable<string> CandidateLines(ModelComparison comparison, string pvUnit)
    {
        foreach (var (name, candidate) in comparison.Candidates)
        {
            var reasons = candidate.RejectionReasons.Select(ReasonText);
            yield return $"{name} · RMSE {candidate.ValidationRmse:G5} {pvUnit} · r₁ {candidate.ValidationResidualLagOne?.ToString("G4") ?? "indéterminé"} · " +
                (candidate.RejectionReasons.Length == 0 ? Loc.T("cs.criteres_satisfaits") : string.Join(", ", reasons));
        }
    }

    private void ShowStudy(object sender, SelectionChangedEventArgs args)
    {
        _shown = StudyList.SelectedItem as IdentificationStudy;
        _window = null;
        // Tuning panels exist only for a model accepted on its independent validation trial (NC-02).
        bool tunable = _shown?.IsAcceptedForTuning == true;
        OpenLoopButton.Visibility = _shown?.Family == ModelFamily.FOPDT ? Visibility.Visible : Visibility.Collapsed;
        PiPanel.Visibility = tunable && _shown?.Family == ModelFamily.FOPDT ? Visibility.Visible : Visibility.Collapsed;
        SopdtPanel.Visibility = tunable && _shown?.Family == ModelFamily.SOPDT ? Visibility.Visible : Visibility.Collapsed;
        ReviewButton.Visibility = _shown is null ? Visibility.Collapsed : Visibility.Visible;
        DriftButton.Visibility = tunable && _shown?.Family is ModelFamily.FOPDT or ModelFamily.SOPDT or ModelFamily.Integrateur ? Visibility.Visible : Visibility.Collapsed;
        IntegratorButton.Visibility = tunable && _shown?.Family == ModelFamily.Integrateur ? Visibility.Visible : Visibility.Collapsed;
        SopdtReport.Text = "";
        TargetPanel.Visibility = tunable && _shown?.Family is ModelFamily.FOPDT or ModelFamily.SOPDT
            ? Visibility.Visible : Visibility.Collapsed;
        ResponseProfile.SelectedIndex = 0;
        UpdateResponseTarget();
        PiList.ItemsSource = null;
        PiReport.Text = "";
        if (_shown is { Family: ModelFamily.FOPDT } model && tunable)
        {
            try
            {
                PiList.ItemsSource = _workspace.PiStudies(model.Id);
                if (PiList.Items.Count > 0)
                    PiList.SelectedIndex = 0;
            }
            catch (Exception error) { ShowError(error); }
        }
        if (_shown is not { } study)
        {
            FitReport.Text = PlotLegend.Text = "";
            VerdictCard.Visibility = Visibility.Collapsed;
            ClearCharts();
            RefreshSidePanels();
            return;
        }
        var result = study.Result;
        var trainingName = _sources.FirstOrDefault(import => import.Id == study.TrainingId)?.Label ?? study.TrainingId.ToString();
        var validationName = _sources.FirstOrDefault(import => import.Id == study.ValidationId)?.Label ?? study.ValidationId.ToString();
        var warnings = result.Warnings.Select(code => code switch
        {
            "two_point_estimator" => Loc.T("cs.broida_determination_directe_sur_les"),
            "measured_tail_plateau" => Loc.T("cs.la_valeur_finale_a_ete"),
            "interpolated_crossings" => Loc.T("cs.instants_28_40_interpoles_lineairement"),
            "parameter_bound" => Loc.T("cs.un_parametre_est_proche_dune"),
            "delay_resolution" => Loc.T("cs.le_retard_estime_est_inferieur"),
            "delay_onset_uncertainty" => Loc.T("cs.linstant_de_lechelon_nest_connu", result.Training.OnsetIntervalSeconds, result.DelaySeconds, result.TuningDelaySeconds),
            "short_horizon" => Loc.T("cs.au_moins_un_essai_est"),
            "correlated_residuals" => Loc.T("cs.residus_autocorreles_sur_lessai_destimation"),
            "delay_derivative" => Loc.T("cs.le_retard_estime_est_trop"),
            "fast_pole_resolution" => Loc.T("cs.la_constante_rapide_est_inferieure"),
            "integrator_validation_tail" => Loc.T("cs.la_fin_de_lessai_de"),
            "close_poles" => Loc.T("cs.les_deux_constantes_sont_proches"),
            "ill_conditioned" => Loc.T("cs.sensibilites_locales_trop_proches_pour"),
            _ => code
        });
        var uncertainty = result.StandardErrors is { } errors
            ? Loc.T("cs.erreurs_types_locales_conditionnelles_k", errors.Gain, study.PvUnit, study.MvUnit, errors.TauSeconds, errors.DelaySeconds)
            : Loc.T("cs.erreurs_types_locales_indisponibles");
        var gainUnit = study.Family == ModelFamily.Integrateur
            ? $"{study.PvUnit}/({study.MvUnit}·s)" : $"{study.PvUnit}/{study.MvUnit}";
        var timeConstants = result.SecondTauSeconds is { } fast
            ? Loc.T("cs.lente_s_rapide_s", result.TauSeconds, fast)
            : result.TauSeconds is { } tau ? $" · τ = {tau:G5} s" : "";
        var family = study.Family == ModelFamily.Integrateur ? Loc.T("cs.integrateur") : study.Family.ToString();
        var windows = Loc.T("cs.plage_estimation_plage_validation", FormatWindow(study.TrainingWindow), FormatWindow(study.ValidationWindow));
        var broida = result.BroidaEvidence is { } evidence
            ? Loc.T("cs.broida_t28_s_t40_s", evidence.Crossing28Seconds, evidence.Crossing40Seconds, evidence.TerminalPv, study.PvUnit, evidence.TerminalSamples)
            : "";

        FitReport.Text = $"Modèle {family} exploratoire · K = {result.Gain:G5} {gainUnit}" +
            $"{timeConstants} · θ = {result.DelaySeconds:G5} s (intervalle {result.DelaySeconds:G5} – {result.TuningDelaySeconds:G5} s)\n" +
            $"Estimation ({trainingName}) : {Metrics(result.Training, study.PvUnit)}\n" +
            $"Validation ({validationName}) : {Metrics(result.Validation, study.PvUnit)}\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {study.EngineVersion}\n{windows}{broida}{uncertainty}\n{string.Join(" ", warnings)}";
        FitReport.Text += study.Assessment is { } decision
            ? Loc.T("cs.validation_independante_version_rmse_r1", decision.AlgorithmVersion, decision.Criteria.MaximumRmse, study.PvUnit, decision.Criteria.MaximumResidualLagOne) +
              (decision.Accepted ? Loc.T("cs.modele_accepte") : Loc.T("cs.modele_refuse") + string.Join(", ", decision.Reasons.Select(ReasonText)) +
                  Loc.T("cs.reglage_et_simulation_en_boucle"))
            : study.IsAcceptedForTuning ? "" : Loc.T("cs.aucune_decision_de_validation_enregistree");
        if (study.Comparison is { } saved)
        {
            FitReport.Text += Loc.T("cs.comparaison_enregistree_version_modele_retenu", saved.AlgorithmVersion, saved.Selected, saved.Criteria.MaximumRmse, study.PvUnit, saved.Criteria.MaximumResidualLagOne) +
                string.Join(Environment.NewLine, CandidateLines(saved, study.PvUnit));
        }
        UpdateVerdict(study, gainUnit);
        DrawPlot();
    }

    private static Brush Tone(string key) => PlotColors.Status(key);

    private static Border Tile(string label, string value)
    {
        var tile = new Border
        {
            Background = Tone("MkrSurface"), BorderBrush = Tone("MkrBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 6), MinWidth = 96
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Tone("MkrMuted") });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 16, FontWeight = FontWeights.SemiBold });
        tile.Child = stack;
        return tile;
    }

    /// <summary>At-a-glance verdict: the decision of the validation gate in words AND colour (never colour alone), with the model's key figures.</summary>
    private void UpdateVerdict(IdentificationStudy study, string gainUnit)
    {
        var result = study.Result;
        (string title, string detail, string badge, string fill, string line) = study.Assessment switch
        {
            { Accepted: true } => (Loc.T("cs.modele_accepte_2"), Loc.T("cs.valide_sur_lessai_independant_le"), "MkrSuccess", "MkrSuccessPale", "MkrSuccess"),
            { } refused => (Loc.T("cs.modele_refuse_2"), Loc.T("cs.motifs") + string.Join(", ", refused.Reasons.Select(ReasonText)) + Loc.T("cs.aucun_reglage_nest_propose"), "MkrDanger", "MkrDangerPale", "MkrDanger"),
            _ => (Loc.T("cs.non_decide"), Loc.T("cs.aucune_decision_de_validation_enregistree_2"), "MkrWarning", "MkrWarningPale", "MkrWarning")
        };
        VerdictCard.Background = Tone(fill);
        VerdictCard.BorderBrush = Tone(line);
        VerdictBadge.Background = Tone(badge);
        VerdictTitle.Text = title;
        VerdictDetail.Text = detail;
        System.Windows.Automation.AutomationProperties.SetName(VerdictCard, title + ". " + detail);
        VerdictMetrics.Children.Clear();
        VerdictMetrics.Children.Add(Tile(study.Family == ModelFamily.Integrateur ? Loc.T("cs.pente_k") : Loc.T("cs.gain_k"), $"{result.Gain:G4} {gainUnit}"));
        if (result.TauSeconds is { } tau) VerdictMetrics.Children.Add(Tile(result.SecondTauSeconds is null ? Loc.T("cs.constante") : Loc.T("cs.constante_lente_1"), $"{tau:G4} s"));
        if (result.SecondTauSeconds is { } fast) VerdictMetrics.Children.Add(Tile(Loc.T("cs.constante_rapide_2"), $"{fast:G4} s"));
        VerdictMetrics.Children.Add(Tile(Loc.T("cs.retard"), $"{result.DelaySeconds:G4} s"));
        if (study.Assessment is { } decision && result.Validation is { } validation)
            VerdictMetrics.Children.Add(Tile(Loc.T("cs.erreur_de_validation"), $"{validation.Rmse:G3} / {decision.Criteria.MaximumRmse:G3} {study.PvUnit}"));
        VerdictMetrics.Visibility = Visibility.Collapsed;          // the parameter table already lists these values
        VerdictCard.ToolTip = VerdictDetail.Text;
        VerdictCard.Visibility = Visibility.Visible;                      // the decision is the first thing to read after choosing a study
    }

    private void ChangeResponseProfile(object sender, SelectionChangedEventArgs args) => UpdateResponseTarget();

    private void ResponseTargetChanged(object sender, TextChangedEventArgs args)
    {
        bool hasTarget = ReadTolerance(ResponseTarget, out _);
        ComparePiButton.IsEnabled = CalculatePidButton.IsEnabled = hasTarget;
        if (SopdtReport is not null && SopdtReport.Text.Length > 0)
        {
            SopdtReport.Text = "";
            Message.Text = "";
        }
    }

    private void UpdateResponseTarget()
    {
        if (_shown is null || TargetPanel.Visibility != Visibility.Visible)
            return;

        var model = _shown.Result;
        ResponseTarget.IsReadOnly = ResponseProfile.SelectedIndex == 0;
        if (!ResponseTarget.IsReadOnly)
        {
            TargetHelp.Text = Loc.T("cs.saisissez_la_cible_definie_pour");
            return;
        }

        if (model.DelaySeconds <= 0 || model.Warnings.Contains("delay_resolution"))
        {
            ResponseTarget.Clear();
            TargetHelp.Text = Loc.T("cs.retard_nul_ou_mal_resolu");
            return;
        }

        // Tight SIMC (τc = θ) on the conservative delay bound: an underestimated delay makes the loop aggressive.
        ResponseTarget.Text = model.TuningDelaySeconds.ToString("R", CultureInfo.CurrentCulture);
        TargetHelp.Text = Loc.T("cs.simc_skogestad_2003_pour_explorer", model.TuningDelaySeconds, model.DelaySeconds, model.TuningDelaySeconds);
    }

    private async void CalculateSopdt(object sender, RoutedEventArgs args)
    {
        SopdtReport.Text = "";
        if (_shown is not { Family: ModelFamily.SOPDT } model || !ReadTolerance(ResponseTarget, out var seconds))
        {
            Message.Text = Loc.T("cs.indiquez_un_temps_de_reponse");
            return;
        }
        Form.IsEnabled = TargetPanel.IsEnabled = SopdtPanel.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var pid = await _workspace.CalculateSopdtPidAsync(model, seconds, token);
            token.ThrowIfCancellationRequested();
            string unit = $"{model.MvUnit}/{model.PvUnit}";
            SopdtReport.Text = Loc.T("cs.cible_s_etude_algorithme_forme", seconds, model.Id.ToString()[..8], pid.AlgorithmVersion, pid.Series.Kc, unit, pid.Series.TiSeconds, pid.Series.TdSeconds, pid.Ideal.Kp, unit, pid.Ideal.TiSeconds, pid.Ideal.TdSeconds) +
                (pid.Margins is { } margins ? Loc.T("cs.marges_nominales", margins.Describe()) : Loc.T("cs.marges_nominales_non_calculees")) +
                Loc.T("cs.calcul_continu_exploratoire_filtre_derive");
            if (pid.Warnings.Contains("tight_response"))
                SopdtReport.Text += Loc.T("cs.la_cible_demandee_est_plus");
            Message.Text = Loc.T("cs.reglage_pid_theorique_affiche");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = TargetPanel.IsEnabled = SopdtPanel.IsEnabled = true; }
    }

    private async void CalculatePi(object sender, RoutedEventArgs args)
    {
        if (_shown is not { Family: ModelFamily.FOPDT } model || !ReadTolerance(ResponseTarget, out var seconds))
        {
            Message.Text = Loc.T("cs.selectionnez_une_etude_fopdt_et");
            return;
        }
        Form.IsEnabled = TargetPanel.IsEnabled = PiPanel.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var saved = await _workspace.CalculatePiAsync(model, seconds, token);
            var studies = _workspace.PiStudies(model.Id);
            PiList.ItemsSource = studies;
            PiList.SelectedItem = studies.First(item => item.Id == saved.Id);
            Message.Text = Loc.T("cs.reglages_theoriques_pi_enregistres");
            StudiesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = TargetPanel.IsEnabled = PiPanel.IsEnabled = true; }
    }

    private void OpenLoop(object sender, RoutedEventArgs args)
    {
        if (_shown is { Family: ModelFamily.FOPDT } model)
            new OpenLoopWindow(_workspace, model) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void ShowPidffValues(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } model || PiList.SelectedItem is not PiStudy tuning)
        {
            Message.Text = Loc.T("cs.selectionnez_un_reglage_pi_enregistre");
            return;
        }
        var token = _lifetime.Token;
        try
        {
            var sections = new List<string>();
            foreach (var (simc, label) in new[] { (false, Loc.T("cs.lambda_dahlin")), (true, "SIMC") })
            {
                var result = await _workspace.ConvertToPidffAsync(model, tuning, simc, token);
                token.ThrowIfCancellationRequested();
                sections.Add(result.Conversion is { } conversion
                    ? conversion.Describe(label, model.MvUnit, model.PvUnit)
                    : $"{label} : {result.RefusalText}");
            }
            new BlockValuesWindow(Loc.T("cs.orias_valeurs_du_bloc_pidff"), string.Join("\n──────────\n\n", sections)) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowOtherBlocks(object sender, RoutedEventArgs args)
    {
        if (_shown is not { Family: ModelFamily.FOPDT } model || PiList.SelectedItem is not PiStudy tuning)
        {
            Message.Text = Loc.T("cs.selectionnez_un_reglage_pi_enregistre");
            return;
        }
        try { new BlockTargetWindow(_workspace, model, tuning) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private void OpenReview(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } model) return;
        try { new ReviewWindow(_workspace, model.Id, model.Label) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private async void CheckDrift(object sender, RoutedEventArgs args)
    {
        var columns = ReadColumns(ValidationTime, ValidationMv, ValidationPv);
        if (_shown is not { Family: ModelFamily.FOPDT or ModelFamily.SOPDT or ModelFamily.Integrateur } model || ValidationSource.SelectedItem is not ImportSummary trial || columns is null ||
            MvUnit.Text.Trim().Length == 0 || PvUnit.Text.Trim().Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(PvTolerance, out var pvTolerance))
        {
            Message.Text = Loc.T("cs.choisissez_le_nouvel_essai_dans");
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var report = await _workspace.CheckModelDriftAsync(model, trial, columns, MvUnit.Text.Trim(), PvUnit.Text.Trim(),
                mvTolerance, pvTolerance, _validationSelection?.Window, token);
            token.ThrowIfCancellationRequested();
            Message.Text = Loc.T("cs.controle_de_derive_termine");
            MessageBox.Show(Window.GetWindow(this), report.Describe(MvUnit.Text.Trim(), PvUnit.Text.Trim()), Loc.T("cs.orias_controle_de_derive_du"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private void OpenIntegrator(object sender, RoutedEventArgs args)
    {
        if (_shown is not { Family: ModelFamily.Integrateur } model) return;
        try { new IntegratorWindow(_workspace, model) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private void OpenSimulation(object sender, RoutedEventArgs args)
    {
        if (_shown is not { } model || PiList.SelectedItem is not PiStudy tuning)
        {
            Message.Text = Loc.T("cs.selectionnez_un_reglage_pi_enregistre");
            return;
        }
        try
        {
            new SimulationWindow(_workspace, model, tuning) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowPi(object sender, SelectionChangedEventArgs args)
    {
        SimulateSavedPi.IsEnabled = PidffValues.IsEnabled = OtherBlocks.IsEnabled = PiList.SelectedItem is PiStudy;
        if (_shown is not { } model || PiList.SelectedItem is not PiStudy pi)
        {
            PiReport.Text = "";
            return;
        }
        var result = pi.Result;
        var ratio = $"{model.MvUnit}/{model.PvUnit}";
        string Format(PiCoefficients settings) =>
            Loc.T("cs.kp_ti_s_ki_parallele", settings.Kp, ratio, settings.TiSeconds, settings.ParallelKi, ratio) +
            (settings.Margins is { } margins ? Loc.T("cs.marges_nominales_2") + margins.Describe() : Loc.T("cs.marges_nominales_non_calculees_reglage"));
        var tight = result.Warnings.Contains("tight_response")
            ? Loc.T("cs.le_temps_choisi_est_inferieur") : "";
        PiReport.Text = Loc.T("cs.reglage_enregistre_pour_la_simulation", result.ResponseSeconds, result.AlgorithmVersion, pi.EngineVersion, Format(result.Imc), Format(result.Simc)) +
            (result.ImcPublished is { } published ? Loc.T("cs.imc_publie_rivera_morari_et", Format(published)) : "") +
            Loc.T("cs.forme_ideale_continue_ecart_consigne") + tight;
    }

    private static string FormatWindow(StepWindow? window) => window is { } w
        ? Loc.T("cs.indices_source_inclus", w.RowStart, w.RowEnd)
        : Loc.T("cs.fichier_complet");

    private static string Metrics(FitDiagnostics fit, string unit) =>
        $"{fit.Samples} points · RMSE {fit.Rmse:G4} {unit} · MAE {fit.Mae:G4} {unit} · " +
        $"R² {fit.RSquared?.ToString("G4") ?? "—"} · corrélation résiduelle lag 1 {fit.ResidualLagOne?.ToString("G4") ?? "—"}";

    private FitDiagnostics? DisplayedDiagnostics() => _shown is not { } study ? null :
        PlotSource.SelectedIndex == 0 ? study.Result.Training : study.Result.Validation;

    private void ChangePlotSource(object sender, SelectionChangedEventArgs args) => DrawPlot();

    private void ExportChartsPng(string path)
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(ChartArea);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ChartArea.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ChartArea.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(ChartArea);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(path);
        encoder.Save(file);
    }

    private void ResetPlotZoom(object sender, RoutedEventArgs args)
    {
        _syncing = true;
        foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) chart.ResetView();
        _syncing = false;
    }

    private void ZoomIn(object sender, RoutedEventArgs args) => Chart.ZoomBy(0.7);

    private void ZoomOut(object sender, RoutedEventArgs args) => Chart.ZoomBy(1 / 0.7);

    private void FocusChart(object sender, RoutedEventArgs args) => Chart.Focus();

    private void ExportPlotImage(object sender, RoutedEventArgs args)
    {
        var target = ExportTarget.Choose(Window.GetWindow(this), Loc.T("cs.image_png_png_png"), "diagnostic-modele.png");
        if (target is null) return;
        try { ExportChartsPng(target); Message.Text = Loc.T("cs.image_exportee"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { Message.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
    }

    private void ExportPlotData(object sender, RoutedEventArgs args)
    {
        var target = ExportTarget.Choose(Window.GetWindow(this), Loc.T("cs.csv_csv_csv"), "diagnostic-modele.csv");
        if (target is null) return;
        try { SignalChart.ExportCsv(target, Chart, CommandChart, ResidualChart); Message.Text = Loc.T("cs.donnees_exportees"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { Message.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
    }

    private void ToggleTrials(object sender, RoutedEventArgs args) => PageSetup.IsChecked = true;

    private void PageChanged(object sender, RoutedEventArgs args)
    {
        if (ResultsPage is null || SetupPage is null) return;
        var setup = PageSetup.IsChecked == true;
        ResultsPage.Visibility = setup ? Visibility.Collapsed : Visibility.Visible;
        SetupPage.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Gp(s) as a typeset fraction: numerator over denominator with a rule, exponent as a superscript.</summary>
    private void BuildFormula(ModelFamily family)
    {
        FormulaHost.Children.Clear();
        TextBlock Part(string text, double size = 16, bool italic = false) => new()
        {
            Text = text, FontFamily = new FontFamily("Cambria Math, Segoe UI"), FontSize = size, FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MkrText")
        };
        FormulaHost.Children.Add(Part("Gp(s) = "));
        var numerator = new TextBlock { FontFamily = new FontFamily("Cambria Math, Segoe UI"), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("MkrText") };
        numerator.Inlines.Add(new System.Windows.Documents.Run(family == ModelFamily.Integrateur ? "k′ e" : "K e"));
        numerator.Inlines.Add(new System.Windows.Documents.Run("−θs") { BaselineAlignment = BaselineAlignment.Superscript, FontSize = 11 });
        var denominatorText = family switch
        {
            ModelFamily.SOPDT => "(1 + τ₁s)(1 + τ₂s)",
            ModelFamily.Integrateur => "s",
            _ => "1 + τs"
        };
        var denominator = new TextBlock { Text = denominatorText, FontFamily = new FontFamily("Cambria Math, Segoe UI"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("MkrText") };
        var rule = new Border { Height = 1, Background = (Brush)FindResource("MkrText"), Margin = new Thickness(0, 1, 0, 1) };
        var fraction = new StackPanel();
        fraction.Children.Add(numerator);
        fraction.Children.Add(rule);
        fraction.Children.Add(denominator);
        FormulaHost.Children.Add(fraction);
    }

    /// <summary>Facts of the displayed trial for the status bar: trial, points, A–B window and fit.</summary>
    public string Facts { get; private set; } = "";
    public event EventHandler? FactsChanged;

    private bool _syncing;
    private (double From, double To)? _window;

    private void ClearWindow(object sender, RoutedEventArgs args) { _window = null; DrawPlot(); }

    private bool _chartOnly;

    private void ToggleChartOnly(object sender, RoutedEventArgs args)
    {
        _chartOnly = !_chartOnly;
        PanelColumn.MinWidth = _chartOnly ? 0 : 300;
        PanelColumn.Width = _chartOnly ? new GridLength(0) : new GridLength(0.38, GridUnitType.Star);
        PanelScroll.Visibility = _chartOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool _tableSync;

    public sealed class ModelRow
    {
        public IdentificationStudy Study { get; init; } = null!;
        public string Family { get; init; } = "";
        public string Fit { get; init; } = "";
        public string Rmse { get; init; } = "";
        public string Decision { get; init; } = "";
    }

    private void ModelTableChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_tableSync || ModelTable.SelectedItem is not ModelRow row) return;
        StudyList.SelectedItem = (StudyList.ItemsSource as IEnumerable<IdentificationStudy>)?.FirstOrDefault(item => item.Id == row.Study.Id);
    }

    /// <summary>FIT = 100 · (1 − ‖y − ŷ‖ / ‖y − mean(y)‖), the normalised-RMSE fit of the plotted trial (a display metric, not an acceptance criterion).</summary>
    private static double? FitPercent(FitDiagnostics? fit) => FitPercent(fit?.Points);

    private static double? FitPercent(FitPoint[]? all)
    {
        if (all is null) return null;
        var points = all.Where(p => double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
        if (points.Length < 3) return null;
        var mean = points.Average(p => p.Observed);
        var denominator = Math.Sqrt(points.Sum(p => (p.Observed - mean) * (p.Observed - mean)));
        if (denominator <= 0) return null;
        return 100 * (1 - Math.Sqrt(points.Sum(p => (p.Observed - p.Predicted) * (p.Observed - p.Predicted))) / denominator);
    }

    private static double? Iae(FitPoint[]? all)
    {
        if (all is null) return null;
        var points = all.Where(p => double.IsFinite(p.Seconds) && double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
        if (points.Length < 2) return null;
        var total = 0.0;
        for (var i = 1; i < points.Length; i++)
            total += 0.5 * (Math.Abs(points[i].Observed - points[i].Predicted) + Math.Abs(points[i - 1].Observed - points[i - 1].Predicted)) * (points[i].Seconds - points[i - 1].Seconds);
        return total;
    }

    /// <summary>One result line: label (regular), value (semi-bold, right aligned, optionally the headline figure) and unit (muted) in fixed columns.</summary>
    private UIElement KeyValue(string label, string value, string unit = "", bool headline = false)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 62 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        var name = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(10, 2, 6, 2), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        name.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        grid.Children.Add(name);
        var val = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, FontSize = headline ? 15 : 13, Margin = new Thickness(6, 1, 4, 1), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        val.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        var u = new TextBlock { Text = unit, FontSize = 11, Margin = new Thickness(2, 1, 8, 1), VerticalAlignment = VerticalAlignment.Center };
        u.SetResourceReference(TextBlock.ForegroundProperty, "MkrMuted");
        Grid.SetColumn(u, 2);
        grid.Children.Add(u);
        var border = new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.SetResourceReference(Border.BorderBrushProperty, "MkrBorder");
        return border;
    }

    private void RefreshSidePanels()
    {
        ParameterTable.Children.Clear();
        QualityTable.Children.Clear();
        if (_shown is not { } study)
        {
            FormulaHost.Children.Clear();
            FormulaFamily.Text = Loc.T("cs.aucune_etude_selectionnee");
            ModelTable.ItemsSource = null;
            return;
        }
        var result = study.Result;
        var gainUnit = study.Family == ModelFamily.Integrateur ? $"{study.PvUnit}/({study.MvUnit}·s)" : $"{study.PvUnit}/{study.MvUnit}";
        BuildFormula(study.Family);
        ModelPanel.Header = study.Family switch
        {
            ModelFamily.SOPDT => Loc.T("cs.modele_identifie_sopdt"),
            ModelFamily.Integrateur => Loc.T("cs.modele_identifie_integrateur"),
            _ => Loc.T("cs.modele_identifie_fopdt")
        };
        ParameterTable.Children.Add(KeyValue(study.Family == ModelFamily.Integrateur ? Loc.T("cs.pente_k") : Loc.T("cs.gain_du_procede_k"), result.Gain.ToString("G5", CultureInfo.CurrentCulture), gainUnit));
        if (result.TauSeconds is { } tau) ParameterTable.Children.Add(KeyValue(result.SecondTauSeconds is null ? Loc.T("cs.constante_de_temps") : Loc.T("cs.constante_lente_1"), tau.ToString("G5", CultureInfo.CurrentCulture), "s"));
        if (result.SecondTauSeconds is { } fast) ParameterTable.Children.Add(KeyValue(Loc.T("cs.constante_rapide_2"), fast.ToString("G5", CultureInfo.CurrentCulture), "s"));
        ParameterTable.Children.Add(KeyValue($"Temps mort θ (prudent : {result.TuningDelaySeconds.ToString("G4", CultureInfo.CurrentCulture)} s)", result.DelaySeconds.ToString("G5", CultureInfo.CurrentCulture), "s"));

        var shown = DisplayedDiagnostics();
        var label = PlotSource.SelectedIndex == 0 ? "estimation" : "validation";
        if (shown is not null)
        {
            // Without a selected A–B range the figures cover the whole displayed trial; with one, only the points inside it (display only: nothing is re-estimated).
            var pts = shown.Points;
            if (_window is { } w)
            {
                pts = pts.Where(p => p.Seconds >= w.From && p.Seconds <= w.To).ToArray();
                label += " A–B";
            }
            if (FitPercent(pts) is { } fitValue) QualityTable.Children.Add(KeyValue(Loc.T("cs.fit_nrmse", label), fitValue.ToString("F1", CultureInfo.CurrentCulture), "%", headline: true));
            var finite = pts.Where(p => double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
            if (finite.Length > 0)
            {
                var rmse = Math.Sqrt(finite.Average(p => (p.Observed - p.Predicted) * (p.Observed - p.Predicted)));
                QualityTable.Children.Add(KeyValue(Loc.T("cs.rmse", label), rmse.ToString("G3", CultureInfo.CurrentCulture), study.PvUnit));
                var mean = finite.Average(p => p.Observed);
                var total = finite.Sum(p => (p.Observed - mean) * (p.Observed - mean));
                if (total > 0) QualityTable.Children.Add(KeyValue($"R² ({label})", (1 - finite.Sum(p => (p.Observed - p.Predicted) * (p.Observed - p.Predicted)) / total).ToString("F3", CultureInfo.CurrentCulture), Loc.T("cs.sans_unite")));
            }
            if (Iae(pts) is { } iae) QualityTable.Children.Add(KeyValue($"IAE ({label})", iae.ToString("G3", CultureInfo.CurrentCulture), study.PvUnit + "·s"));
        }
        if (study.Assessment is { } decision)
            VerdictCard.ToolTip = $"{VerdictDetail.Text} Seuil de RMSE : {decision.Criteria.MaximumRmse.ToString("G3", CultureInfo.CurrentCulture)} {study.PvUnit}.";

        // Rows come from what was really computed on these two trials: the candidates of the saved comparison (FOPDT / SOPDT judged on the validation trial),
        // then any other study of another family fitted on the same trials. The FIT percentage exists only where a fitted series is stored.
        var sameTrials = (StudyList.ItemsSource as IEnumerable<IdentificationStudy> ?? []).Where(item => item.TrainingId == study.TrainingId && item.ValidationId == study.ValidationId)
            .GroupBy(item => item.Family).Select(group => group.OrderByDescending(item => item.CalculatedAt).First()).ToList();
        string FamilyName(ModelFamily family) => family == ModelFamily.Integrateur ? Loc.T("cs.integrateur_2") : family.ToString();
        var rows = new List<ModelRow>();
        if (study.Comparison is { } comparison)
            foreach (var (name, candidate) in comparison.Candidates)
            {
                var match = sameTrials.FirstOrDefault(item => FamilyName(item.Family) == name);
                rows.Add(new ModelRow
                {
                    Study = match ?? study,
                    Family = name + (comparison.Selected == name ? " ✓" : ""),
                    Fit = match is not null && FitPercent(match.Result.Validation) is { } f ? f.ToString("F1", CultureInfo.CurrentCulture) : "—",
                    Rmse = candidate.ValidationRmse.ToString("G3", CultureInfo.CurrentCulture),
                    Decision = candidate.RejectionReasons.Length == 0 ? "satisfait" : Loc.T("cs.refuse")
                });
            }
        foreach (var item in sameTrials.Where(item => rows.All(row => !row.Family.StartsWith(FamilyName(item.Family), StringComparison.Ordinal))))
            rows.Add(new ModelRow
            {
                Study = item,
                Family = FamilyName(item.Family),
                Fit = FitPercent(item.Result.Validation) is { } f ? f.ToString("F1", CultureInfo.CurrentCulture) : "—",
                Rmse = item.Result.Validation.Rmse.ToString("G3", CultureInfo.CurrentCulture),
                Decision = item.Assessment is { Accepted: true } ? Loc.T("cs.accepte") : item.Assessment is not null ? Loc.T("cs.refuse") : Loc.T("cs.non_decide_2")
            });
        ModelTable.Columns[1].Header = "FIT (%)";
        ModelTable.Columns[2].Header = Loc.T("cs.rmse", study.PvUnit);
        var unavailable = rows.Any(row => row.Fit == "—");
        ModelNote.Visibility = unavailable ? Visibility.Visible : Visibility.Collapsed;
        ModelNote.Text = unavailable
            ? Loc.T("cs.indicateur_non_disponible_le_fit")
            : "";
        _tableSync = true;
        ModelTable.ItemsSource = rows;
        ModelTable.SelectedItem = rows.FirstOrDefault(row => row.Study.Id == study.Id && row.Family.StartsWith(study.Family == ModelFamily.Integrateur ? Loc.T("cs.integrateur_2") : study.Family.ToString(), StringComparison.Ordinal));
        _tableSync = false;
    }

    private void ClearCharts()
    {
        foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) chart.SetTracks(Array.Empty<ChartTrack>());
    }

    private void DrawPlot()
    {
        if (Chart is null || CommandChart is null || ResidualChart is null || PlotSource is null)
            return;
        var fit = DisplayedDiagnostics();
        var category = PlotSource.SelectedIndex == 0 ? "Estimation" : Loc.T("cs.validation_independante");
        PlotLegend.Text = fit is null ? "" : Loc.T("cs.points_enregistres_affiches_sans_modification", category, fit.Points.Length);
        if (fit is null || _shown is not { } study)
        {
            ClearCharts();
            return;
        }
        var points = fit.Points.Where(p => double.IsFinite(p.Seconds) && double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
        var seconds = points.Select(p => p.Seconds).ToArray();
        foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) chart.XLabel = Loc.T("cs.temps_apres_l_echelon_s");
        var horizon = seconds.Length > 0 ? seconds[^1] : 1;
        var first = seconds.Length > 0 ? seconds[0] : 0;
        Chart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.reponse_du_procede_et_modele"), study.PvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.mesure_observee"), "MkrSignal1", seconds, points.Select(p => p.Observed).ToArray()),
                new ChartSeries(Loc.T("cs.modele_identifie"), "MkrSignal2", seconds, points.Select(p => p.Predicted).ToArray())
            }) { YLabel = Loc.T("cs.mesure", study.PvUnit) }
        });
        CommandChart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.commande_mv"), study.MvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.echelon_applique"), "MkrSignal3", new[] { first, 0, 0, horizon }, new[] { 0, 0, fit.InputStep, fit.InputStep })
            }) { YLabel = $"MV ({study.MvUnit})" }
        });
        ResidualChart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.residus_mesure_modele"), study.PvUnit, new[]
            {
                new ChartSeries(Loc.T("cs.residu"), "MkrSignal4", seconds, points.Select(p => p.Observed - p.Predicted).ToArray())
            }) { YLabel = Loc.T("cs.residu_2", study.PvUnit) }
        });
        // A and B bound the interval the figures are computed on. By default it is the whole displayed trial (the fit uses every point from the step onset to its end);
        // dragging on a chart narrows it, and the indicators are then recomputed on that range (display only, nothing is re-estimated).
        var interval = _window ?? (first, horizon);
        if (seconds.Length > 1)
        {
            Chart.SetEvents(new[] { (interval.Item1, "A"), (interval.Item2, "B") });
            // the markers always show the interval the figures cover; the tint appears only for a range the user selected
            foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) { if (_window is null) chart.SetHighlight(null, null); else chart.SetHighlight(interval.Item1, interval.Item2); }
            PlotLegend.Text += _window is null
                ? Loc.T("cs.ab_intervalle_de_l_ajustement", first, horizon)
                : Loc.T("cs.plage_ab_choisie_de_s", interval.Item1, interval.Item2);
        }
        RefreshSidePanels();
        var shownPoints = _window is { } w ? points.Count(p => p.Seconds >= w.From && p.Seconds <= w.To) : points.Length;
        var fitText = FitPercent(_window is { } w2 ? points.Where(p => p.Seconds >= w2.From && p.Seconds <= w2.To).ToArray() : points) is { } fv ? $"   ·   FIT : {fv.ToString("F1", CultureInfo.CurrentCulture)} %" : "";
        Facts = $"Essai : {category.ToLowerInvariant()}   ·   Durée : {horizon.ToString("G5", CultureInfo.CurrentCulture)} s   ·   Fenêtre : " +
                (_window is { } w3 ? $"{w3.From.ToString("G4", CultureInfo.CurrentCulture)} – {w3.To.ToString("G4", CultureInfo.CurrentCulture)} s" : Loc.T("cs.essai_complet")) +
                $"   ·   Points : {shownPoints.ToString("N0", CultureInfo.CurrentCulture)}{fitText}";
        FactsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static StepColumns? ReadColumns(ComboBox time, ComboBox mv, ComboBox pv) =>
        time.SelectedItem is string t && mv.SelectedItem is string u && pv.SelectedItem is string y &&
        t != u && t != y && u != y ? new StepColumns(t, u, y) : null;

    private static bool ReadTolerance(TextBox input, out double value) =>
        double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
        value is >= 1e-100 and <= 1e100;

    private void ShowError(Exception error)
    {
        Trace.TraceError("identification.study {0}", error.GetType().Name);
        Message.Text = error is CsvInputException input ? input.Code switch
        {
            "preview_mapping" => Loc.T("cs.selectionnez_des_colonnes_de_temps"),
            "preview_time" => Loc.T("cs.les_horodatages_doivent_etre_valides"),
            "preview_data" => Loc.T("cs.la_sortie_de_commande_et"),
            "preview_provenance" => Loc.T("cs.lempreinte_de_la_source_ne"),
            "fit_same_source_window" => Loc.T("cs.deux_essais_dun_meme_enregistrement"),
            "fit_same_source_overlap" => Loc.T("cs.les_deux_plages_dun_meme"),
            "arx_mapping" => Loc.T("cs.colonnes_plage_de_lignes_ou"),
            "arx_time" => Loc.T("cs.horodatages_invalides_non_croissants_ou"),
            "arx_sampling" => Loc.T("cs.pas_d_echantillonnage_irregulier_mkr"),
            "arx_excitation" => Loc.T("cs.la_commande_est_quasi_constante"),
            "arx_ill_conditioned" => Loc.T("cs.estimation_mal_conditionnee_excitation_insuffisante"),
            "arx_data" => Loc.T("cs.pas_assez_d_echantillons_pour"),
            "arx_closed_loop" => Loc.T("cs.les_essais_en_boucle_fermee"),
            "fit_window" => Loc.T("cs.la_plage_selectionnee_est_invalide"),
            "fit_duplicate_source" => Loc.T("cs.les_deux_imports_contiennent_exactement"),
            "fit_independence" => Loc.T("cs.les_periodes_des_deux_essais"),
            "fit_step" => Loc.T("cs.selectionnez_une_plage_qui_montre"),
            "fit_baseline" => Loc.T("cs.la_mesure_avant_lechelon_depasse"),
            "fit_response" => Loc.T("cs.la_variation_de_la_mesure"),
            "fit_amplitude" => Loc.T("cs.les_amplitudes_absolues_des_echelons"),
            "fit_data" => Loc.T("cs.verifiez_les_valeurs_numeriques_de"),
            "fit_time" => Loc.T("cs.les_horodatages_doivent_etre_valides_2"),
            "fit_recorded_mapping" => Loc.T("cs.le_role_ou_lunite_declare"),
            "fit_integrator_tail" => Loc.T("cs.lessai_destimation_ne_montre_pas"),
            "pi_domain" => Loc.T("cs.le_modele_et_le_temps"),
            "pi_range" => Loc.T("cs.la_combinaison_demandee_depasse_la"),
            "fit_dependency" => Loc.T("cs.installez_les_dependances_python_du"),
            "broida_plateau" => Loc.T("cs.broida_refusee_la_fin_de"),
            "broida_shape" => Loc.T("cs.broida_refusee_reponse_non_monotone"),
            "broida_response" => Loc.T("cs.broida_refusee_variation_finale_de"),
            "broida_horizon" => Loc.T("cs.broida_refusee_horizon_insuffisant_pour"),
            "broida_crossing" => Loc.T("cs.broida_refusee_passages_28_40"),
            "broida_domain" => Loc.T("cs.broida_refusee_les_points_conduisent"),
            "fit_solver" => Loc.T("cs.lestimateur_na_pas_converge_dans"),
            "fit_tolerance" => Loc.T("cs.les_tolerances_de_la_sortie"),
            "fit_comparison_limits" => Loc.T("cs.les_criteres_dacceptation_du_modele"),
            "drift_mapping" or "drift_domain" => Loc.T("cs.les_parametres_du_modele_enregistre"),
            "engine_failure" => Loc.T("cs.le_moteur_de_calcul_a"),
            "engine_non_finite" => Loc.T("cs.le_calcul_a_produit_une"),
            _ => Loc.T("cs.verifiez_les_colonnes_et_les")
        } : error is TimeoutException ? error.Message : Loc.T("cs.etude_indisponible_verifiez_le_stockage");
    }
}
