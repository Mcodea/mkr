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
            Message.Text = "Importez deux essais CSV dans ce projet.";
    }

    private void SelectTraining(object sender, SelectionChangedEventArgs args)
    {
        _trainingSelection = null;
        if (TrainingRangeInfo is not null)
            TrainingRangeInfo.Text = "Estimation : fichier complet";
        Populate(TrainingSource, TrainingTime, TrainingMv, TrainingPv);
    }

    private void SelectValidation(object sender, SelectionChangedEventArgs args)
    {
        _validationSelection = null;
        if (ValidationRangeInfo is not null)
            ValidationRangeInfo.Text = "Validation : fichier complet";
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
            Message.Text = "Choisissez l’enregistrement, ses trois colonnes, les unités et les tolérances de la sortie de commande et de la mesure avant la recherche.";
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = "Recherche des échelons exploitables…";
        var token = _lifetime.Token;
        try
        {
            var search = await _workspace.FindStepsAsync(import, columns, MvUnit.Text.Trim(), PvUnit.Text.Trim(),
                mvTolerance, pvTolerance, token);
            token.ThrowIfCancellationRequested();
            Message.Text = $"{search.Candidates.Length} échelon(s) exploitable(s), {search.Rejected.Length} écarté(s).";
            if (search.ModeColumn is not null)
                Message.Text += $" Colonne {search.ModeColumn} utilisée : {search.RowsNotInManual:N0} ligne(s) hors manuel écartée(s).";
            var window = new StepSearchWindow(search, import.FileName, MvUnit.Text.Trim(), PvUnit.Text.Trim()) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true || window.SelectedPair is not { } pair) return;
            // Both trials come from this recording: select it for validation too, then set the two explicit ranges.
            ValidationSource.SelectedItem = import;
            ValidationTime.SelectedItem = columns.TimeColumn;
            ValidationMv.SelectedItem = columns.MvColumn;
            ValidationPv.SelectedItem = columns.PvColumn;
            _trainingSelection = new WindowChoice(import.Id, columns, pair.FirstWindow);
            _validationSelection = new WindowChoice(import.Id, columns, pair.SecondWindow);
            TrainingRangeInfo.Text = $"Estimation : indices source {pair.FirstWindow.RowStart}..{pair.FirstWindow.RowEnd} (inclus), échelon n°{pair.FirstId}";
            ValidationRangeInfo.Text = $"Validation : indices source {pair.SecondWindow.RowStart}..{pair.SecondWindow.RowEnd} (inclus), échelon n°{pair.SecondId}";
            Confirmed.IsChecked = false;
            Message.Text = "Deux plages indépendantes retenues dans le même enregistrement. Vérifiez les conditions d’essai (boucle ouverte, sans saturation) puis confirmez-les.";
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
            Message.Text = "Sélectionnez un import et trois colonnes distinctes (temps, sortie de commande, mesure) pour visualiser les signaux.";
            return;
        }

        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = "Lecture des courbes brutes en cours…";
        var token = _lifetime.Token;
        try
        {
            var preview = await _workspace.PreviewTrialAsync(import, columns, token);
            if (token.IsCancellationRequested)
                return;
            Message.Text = "Courbes chargées depuis le CSV enregistré, sans modification de l'étude.";
            var window = new TrialPreviewWindow(preview, import.FileName,
                string.IsNullOrWhiteSpace(MvUnit.Text) ? "unité de la sortie de commande non renseignée" : MvUnit.Text.Trim(),
                string.IsNullOrWhiteSpace(PvUnit.Text) ? "unité de la mesure non renseignée" : PvUnit.Text.Trim())
                { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true && window.SelectedWindow is { } range)
            {
                var choice = new WindowChoice(import.Id, columns, range);
                var text = $"Lignes originales {range.RowStart + 1} à {range.RowEnd + 1}" +
                    $" (indices 0-based {range.RowStart}..{range.RowEnd}, bornes incluses)";
                if (training)
                {
                    _trainingSelection = choice;
                    TrainingRangeInfo.Text = "Estimation : " + text;
                }
                else
                {
                    _validationSelection = choice;
                    ValidationRangeInfo.Text = "Validation : " + text;
                }
                Confirmed.IsChecked = false;
                Message.Text = "Plage retenue. Vérifiez les conditions d'essai et confirmez-les avant estimation.";
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
            Message.Text = "Choisissez les deux essais, leurs colonnes, les unités et les tolérances avant de proposer des critères.";
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
            Message.Text = $"Bruit mesuré sur les plateaux avant échelon : σ = {proposal.BaselineNoise:G4} {PvUnit.Text}. " +
                (proposal.Basis == "noise"
                    ? $"RMSE maximale = {proposal.NoiseMultiple:G2} σ."
                    : "Bruit inférieur à la résolution déclarée : RMSE maximale = moitié de la tolérance de la mesure.") +
                " Ces critères sont fixés avant tout ajustement. Modifiez-les si l’étude impose une exigence.";
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
            Message.Text = "Réglage impossible : l'étude ARX a été refusée à la validation indépendante. Aucun réglage n'en est déduit.";
            return;
        }
        if (study.Result.Selected?.FirstOrder is null)
        {
            Message.Text = "Réglage impossible : le modèle ARX retenu est du second ordre ; MKR ne documente pas encore de réduction au premier ordre pour le régler. Aucun réglage n'est inventé.";
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
            Message.Text = "Indiquez deux imports, leurs colonnes, les unités, la tolérance de la commande, la RMSE maximale, puis confirmez les conditions d'essai (boucle ouverte).";
            return;
        }
        if (TrialIndependence.Check(training, _trainingSelection?.Window, validation, _validationSelection?.Window) is { })
        {
            Message.Text = "Les essais d'estimation et de validation doivent être indépendants (imports ou plages distincts, sans ligne commune).";
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = "Identification ARX / sortie-erreur…";
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
            Message.Text = "Indiquez deux imports, trois colonnes distinctes par essai, les unités et tolérances positives, puis confirmez les conditions d’essai.";
            return;
        }

        if (training.Id == validation.Id &&
            TrialIndependence.Check(training, _trainingSelection?.Window, validation, _validationSelection?.Window) is { } same)
        {
            Message.Text = same == "fit_same_source_overlap"
                ? "Les deux plages se recouvrent. Choisissez deux échelons distincts, sans ligne commune."
                : "Le même import est sélectionné deux fois. Choisissez un autre essai, ou deux plages distinctes du même enregistrement (bouton « Rechercher les essais »).";
            return;
        }

        // A saved choice belongs to a specific original import and exact column mapping.
        // Never silently apply a stale range to newly selected data.
        if ((_trainingSelection is { } tr &&
                (tr.ImportId != training.Id || tr.Columns != trainColumns)) ||
            (_validationSelection is { } va &&
                (va.ImportId != validation.Id || va.Columns != valColumns)))
        {
            Message.Text = "Les colonnes ou l'import ont changé après la sélection de plage. Rouvrez la prévisualisation ou réinitialisez le formulaire.";
            return;
        }

        // Validation criteria are mandatory for every estimate, not only for the comparison (audit NC-02).
        if (!ReadTolerance(MaximumRmse, out var rmse) || !ReadTolerance(MaximumLag, out var lag) || lag > 1)
        {
            Message.Text = "Indiquez les critères de validation : une RMSE maximale positive (unité de la mesure) et une limite de corrélation supérieure à 0 et inférieure ou égale à 1.";
            return;
        }
        var criteria = new ComparisonCriteria(rmse, lag);
        Form.IsEnabled = Form0.IsEnabled = false;
        Message.Text = "Estimation et évaluation…";
        var token = _lifetime.Token;
        try
        {
            ModelComparison? accepted = null;
            if (sender == CompareModels)
            {
                var comparison = await _workspace.CompareAsync(training, trainColumns, validation, valColumns, mvUnit, pvUnit,
                    mvTolerance, pvTolerance, rmse, lag, token, _trainingSelection?.Window, _validationSelection?.Window);
                token.ThrowIfCancellationRequested();
                var lines = new List<string> { $"Essai de validation : {validation.FileName}",
                    $"Seuils : RMSE ≤ {rmse:G5} {pvUnit} · |r₁| ≤ {lag:G5}" };
                lines.AddRange(CandidateLines(comparison, pvUnit));
                lines.Add(comparison.Selected is { } selected
                    ? comparison.Decision == "significantly_better_higher_order" && comparison.HoldoutTest is { } test
                        ? $"Modèle proposé : {selected}. Les deux modèles satisfont les critères, mais le second ordre prédit l’essai de validation significativement mieux (rapport des erreurs quadratiques {test.MseRatio:G3} > {test.Critical:G3}, niveau {test.Level:P0})."
                        : $"Modèle proposé : {selected}, ordre le plus faible satisfaisant les critères."
                    : "Aucun des deux modèles ne satisfait les critères.");
                Message.Text = lines[^1];
                if (comparison.Selected is null)
                {
                    lines.Add("Avant un nouvel essai, examinez les courbes et les résidus de validation. Vérifiez les unités, les plages choisies et les conditions du procédé pour les deux acquisitions.");
                    Message.Text = string.Join(Environment.NewLine, lines);
                    return;
                }
                lines.Add("Estimer et enregistrer ce modèle avec les critères et les résultats de comparaison ?");
                if (MessageBox.Show(Window.GetWindow(this), string.Join(Environment.NewLine, lines), "Comparaison sur essai indépendant",
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
                ? "Étude exploratoire enregistrée."
                : "Étude enregistrée mais refusée à la validation indépendante : " +
                  string.Join(", ", study.Assessment!.Reasons.Select(ReasonText)) +
                  ". Aucun réglage ne peut en être déduit.";
            StudiesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = Form0.IsEnabled = true; }
    }

    private static string ReasonText(string reason) => reason switch
    {
        "validation_rmse" => "erreur de validation excessive",
        "validation_residuals" => "corrélation résiduelle excessive ou indéterminée",
        "parameter_bound" => "paramètre en borne",
        "short_horizon" => "durée d’essai insuffisante",
        "fast_pole_resolution" => "pôle rapide mal résolu",
        "ill_conditioned" => "estimation mal conditionnée",
        "integrator_validation_tail" => "essai de validation non intégrant",
        _ => reason
    };

    private static IEnumerable<string> CandidateLines(ModelComparison comparison, string pvUnit)
    {
        foreach (var (name, candidate) in comparison.Candidates)
        {
            var reasons = candidate.RejectionReasons.Select(ReasonText);
            yield return $"{name} · RMSE {candidate.ValidationRmse:G5} {pvUnit} · r₁ {candidate.ValidationResidualLagOne?.ToString("G4") ?? "indéterminé"} · " +
                (candidate.RejectionReasons.Length == 0 ? "critères satisfaits" : string.Join(", ", reasons));
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
            "two_point_estimator" => "Broïda : détermination directe sur les passages 28 % / 40 %, sans optimisation numérique.",
            "measured_tail_plateau" => "La valeur finale a été estimée sur six mesures terminales ; confirmer la stationnarité réelle.",
            "interpolated_crossings" => "Instants 28 % / 40 % interpolés linéairement entre deux échantillons réels.",
            "parameter_bound" => "Un paramètre est proche d’une borne de recherche.",
            "delay_resolution" => "Le retard estimé est inférieur ou égal à la résolution de détection de l’échelon.",
            "delay_onset_uncertainty" => $"L’instant de l’échelon n’est connu qu’à {result.Training.OnsetIntervalSeconds:G4} s près : retard réel entre {result.DelaySeconds:G5} et {result.TuningDelaySeconds:G5} s. Un échantillonnage plus fin réduirait cet écart.",
            "short_horizon" => "Au moins un essai est trop court au regard de la constante de temps lente après le retard.",
            "correlated_residuals" => "Résidus autocorrélés sur l’essai d’estimation.",
            "delay_derivative" => "Le retard estimé est trop proche d’un instant échantillonné pour linéariser sa réponse.",
            "fast_pole_resolution" => "La constante rapide est inférieure au pas médian d’échantillonnage.",
            "integrator_validation_tail" => "La fin de l’essai de validation ne présente pas la dérive attendue.",
            "close_poles" => "Les deux constantes sont proches et peuvent être difficiles à distinguer.",
            "ill_conditioned" => "Sensibilités locales trop proches pour séparer les paramètres.",
            _ => code
        });
        var uncertainty = result.StandardErrors is { } errors
            ? $"Erreurs-types locales conditionnelles (K, τ, θ) : {errors.Gain:G4} {study.PvUnit}/{study.MvUnit}, {errors.TauSeconds:G4} s, {errors.DelaySeconds:G4} s."
            : "Erreurs-types locales indisponibles.";
        var gainUnit = study.Family == ModelFamily.Integrateur
            ? $"{study.PvUnit}/({study.MvUnit}·s)" : $"{study.PvUnit}/{study.MvUnit}";
        var timeConstants = result.SecondTauSeconds is { } fast
            ? $" · τ lente = {result.TauSeconds:G5} s · τ rapide = {fast:G5} s"
            : result.TauSeconds is { } tau ? $" · τ = {tau:G5} s" : "";
        var family = study.Family == ModelFamily.Integrateur ? "intégrateur" : study.Family.ToString();
        var windows = $"Plage estimation : {FormatWindow(study.TrainingWindow)} · plage validation : {FormatWindow(study.ValidationWindow)}\n";
        var broida = result.BroidaEvidence is { } evidence
            ? $"Broïda : t28 = {evidence.Crossing28Seconds:G6} s ; t40 = {evidence.Crossing40Seconds:G6} s ; " +
              $"Mesure terminale observée = {evidence.TerminalPv:G6} {study.PvUnit} " +
              $"sur {evidence.TerminalSamples} échantillons.\n"
            : "";

        FitReport.Text = $"Modèle {family} exploratoire · K = {result.Gain:G5} {gainUnit}" +
            $"{timeConstants} · θ = {result.DelaySeconds:G5} s (intervalle {result.DelaySeconds:G5} – {result.TuningDelaySeconds:G5} s)\n" +
            $"Estimation ({trainingName}) : {Metrics(result.Training, study.PvUnit)}\n" +
            $"Validation ({validationName}) : {Metrics(result.Validation, study.PvUnit)}\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {study.EngineVersion}\n{windows}{broida}{uncertainty}\n{string.Join(" ", warnings)}";
        FitReport.Text += study.Assessment is { } decision
            ? $"\nValidation indépendante · version {decision.AlgorithmVersion} · RMSE ≤ {decision.Criteria.MaximumRmse:G5} {study.PvUnit} · " +
              $"|r₁| ≤ {decision.Criteria.MaximumResidualLagOne:G5} · " +
              (decision.Accepted ? "modèle accepté" : "MODÈLE REFUSÉ : " + string.Join(", ", decision.Reasons.Select(ReasonText)) +
                  ". Réglage et simulation en boucle fermée indisponibles.")
            : study.IsAcceptedForTuning ? "" : "\nAucune décision de validation enregistrée : réglage indisponible. Refaites l’estimation avec des critères.";
        if (study.Comparison is { } saved)
        {
            FitReport.Text += $"\nComparaison enregistrée · version {saved.AlgorithmVersion} · modèle retenu {saved.Selected}\n" +
                $"Seuils : RMSE ≤ {saved.Criteria.MaximumRmse:G5} {study.PvUnit} · |r₁| ≤ {saved.Criteria.MaximumResidualLagOne:G5}\n" +
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
            { Accepted: true } => ("MODÈLE ACCEPTÉ", "Validé sur l’essai indépendant : le réglage et la simulation sont disponibles.", "MkrSuccess", "MkrSuccessPale", "MkrSuccess"),
            { } refused => ("MODÈLE REFUSÉ", "Motifs : " + string.Join(", ", refused.Reasons.Select(ReasonText)) + ". Aucun réglage n’est proposé.", "MkrDanger", "MkrDangerPale", "MkrDanger"),
            _ => ("NON DÉCIDÉ", "Aucune décision de validation enregistrée : refaites l’estimation avec des critères.", "MkrWarning", "MkrWarningPale", "MkrWarning")
        };
        VerdictCard.Background = Tone(fill);
        VerdictCard.BorderBrush = Tone(line);
        VerdictBadge.Background = Tone(badge);
        VerdictTitle.Text = title;
        VerdictDetail.Text = detail;
        System.Windows.Automation.AutomationProperties.SetName(VerdictCard, title + ". " + detail);
        VerdictMetrics.Children.Clear();
        VerdictMetrics.Children.Add(Tile(study.Family == ModelFamily.Integrateur ? "Pente k′" : "Gain K", $"{result.Gain:G4} {gainUnit}"));
        if (result.TauSeconds is { } tau) VerdictMetrics.Children.Add(Tile(result.SecondTauSeconds is null ? "Constante τ" : "Constante lente τ₁", $"{tau:G4} s"));
        if (result.SecondTauSeconds is { } fast) VerdictMetrics.Children.Add(Tile("Constante rapide τ₂", $"{fast:G4} s"));
        VerdictMetrics.Children.Add(Tile("Retard θ", $"{result.DelaySeconds:G4} s"));
        if (study.Assessment is { } decision && result.Validation is { } validation)
            VerdictMetrics.Children.Add(Tile("Erreur de validation", $"{validation.Rmse:G3} / {decision.Criteria.MaximumRmse:G3} {study.PvUnit}"));
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
            TargetHelp.Text = "Saisissez la cible définie pour cette étude, en secondes.";
            return;
        }

        if (model.DelaySeconds <= 0 || model.Warnings.Contains("delay_resolution"))
        {
            ResponseTarget.Clear();
            TargetHelp.Text = "Retard nul ou mal résolu par les mesures. Affinez l’identification du retard ou choisissez Personnalisée si l’étude impose une cible.";
            return;
        }

        // Tight SIMC (τc = θ) on the conservative delay bound: an underestimated delay makes the loop aggressive.
        ResponseTarget.Text = model.TuningDelaySeconds.ToString("R", CultureInfo.CurrentCulture);
        TargetHelp.Text = $"SIMC, Skogestad (2003) · Pour explorer une réponse rapide, τc = retard. MKR retient la borne prudente " +
            $"{model.TuningDelaySeconds:G5} s : l’échelon de la sortie de commande a eu lieu entre deux mesures, le retard réel est compris entre " +
            $"{model.DelaySeconds:G5} et {model.TuningDelaySeconds:G5} s. Vérifiez en simulation le temps obtenu et l’effort de commande.";
    }

    private async void CalculateSopdt(object sender, RoutedEventArgs args)
    {
        SopdtReport.Text = "";
        if (_shown is not { Family: ModelFamily.SOPDT } model || !ReadTolerance(ResponseTarget, out var seconds))
        {
            Message.Text = "Indiquez un temps de réponse strictement positif en secondes.";
            return;
        }
        Form.IsEnabled = TargetPanel.IsEnabled = SopdtPanel.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var pid = await _workspace.CalculateSopdtPidAsync(model, seconds, token);
            token.ThrowIfCancellationRequested();
            string unit = $"{model.MvUnit}/{model.PvUnit}";
            SopdtReport.Text = $"Cible {seconds:G5} s · étude {model.Id.ToString()[..8]} · algorithme {pid.AlgorithmVersion}\n" +
                $"Forme série : Kc {pid.Series.Kc:G6} {unit}, Ti {pid.Series.TiSeconds:G6} s, Td {pid.Series.TdSeconds:G6} s\n" +
                $"Forme idéale : Kp {pid.Ideal.Kp:G6} {unit}, Ti {pid.Ideal.TiSeconds:G6} s, Td {pid.Ideal.TdSeconds:G6} s\n" +
                (pid.Margins is { } margins ? $"Marges nominales : {margins.Describe()}\n" : "Marges nominales non calculées.\n") +
                "Calcul continu exploratoire. Filtre dérivé à définir, limites de l’actionneur et équivalence constructeur à vérifier.";
            if (pid.Warnings.Contains("tight_response"))
                SopdtReport.Text += "\nLa cible demandée est plus courte que le retard du procédé.";
            Message.Text = "Réglage PID théorique affiché.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = TargetPanel.IsEnabled = SopdtPanel.IsEnabled = true; }
    }

    private async void CalculatePi(object sender, RoutedEventArgs args)
    {
        if (_shown is not { Family: ModelFamily.FOPDT } model || !ReadTolerance(ResponseTarget, out var seconds))
        {
            Message.Text = "Sélectionnez une étude FOPDT et indiquez un temps de réponse strictement positif en secondes.";
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
            Message.Text = "Réglages théoriques PI enregistrés.";
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
            Message.Text = "Sélectionnez un réglage PI enregistré.";
            return;
        }
        var token = _lifetime.Token;
        try
        {
            var sections = new List<string>();
            foreach (var (simc, label) in new[] { (false, "Lambda (Dahlin)"), (true, "SIMC") })
            {
                var result = await _workspace.ConvertToPidffAsync(model, tuning, simc, token);
                token.ThrowIfCancellationRequested();
                sections.Add(result.Conversion is { } conversion
                    ? conversion.Describe(label, model.MvUnit, model.PvUnit)
                    : $"{label} : {result.RefusalText}");
            }
            new BlockValuesWindow("MKR — Valeurs du bloc PIDFF (non qualifiées)", string.Join("\n──────────\n\n", sections)) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowOtherBlocks(object sender, RoutedEventArgs args)
    {
        if (_shown is not { Family: ModelFamily.FOPDT } model || PiList.SelectedItem is not PiStudy tuning)
        {
            Message.Text = "Sélectionnez un réglage PI enregistré.";
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
            Message.Text = "Choisissez le nouvel essai dans la zone Validation (source, colonnes), les unités et les tolérances.";
            return;
        }
        Form.IsEnabled = Form0.IsEnabled = false;
        var token = _lifetime.Token;
        try
        {
            var report = await _workspace.CheckModelDriftAsync(model, trial, columns, MvUnit.Text.Trim(), PvUnit.Text.Trim(),
                mvTolerance, pvTolerance, _validationSelection?.Window, token);
            token.ThrowIfCancellationRequested();
            Message.Text = "Contrôle de dérive terminé.";
            MessageBox.Show(Window.GetWindow(this), report.Describe(MvUnit.Text.Trim(), PvUnit.Text.Trim()), "MKR — Contrôle de dérive du modèle");
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
            Message.Text = "Sélectionnez un réglage PI enregistré.";
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
            $"Kp = {settings.Kp:G5} {ratio}, Ti = {settings.TiSeconds:G5} s, Ki parallèle = {settings.ParallelKi:G5} {ratio}/s\n    " +
            (settings.Margins is { } margins ? "Marges nominales : " + margins.Describe() : "Marges nominales non calculées (réglage enregistré avant leur introduction).");
        var tight = result.Warnings.Contains("tight_response")
            ? "\nLe temps choisi est inférieur au retard estimé. Examinez la robustesse." : "";
        PiReport.Text = $"Réglage enregistré pour la simulation · cible {result.ResponseSeconds:G5} s · méthode {result.AlgorithmVersion} · moteur {pi.EngineVersion}\n" +
            $"Lambda : {Format(result.Imc)}\nSIMC : {Format(result.Simc)}\n" +
            (result.ImcPublished is { } published ? $"IMC publié (Rivera, Morari et Skogestad 1986, Padé du premier ordre) : {Format(published)}\n" : "") +
            "Forme idéale continue, écart = consigne − mesure. Marges calculées sur le modèle nominal seul : elles ne remplacent pas l’analyse de sensibilité. Modèle exploratoire et contraintes d’actionneur à vérifier." + tight;
    }

    private static string FormatWindow(StepWindow? window) => window is { } w
        ? $"indices source {w.RowStart}..{w.RowEnd} (inclus)"
        : "fichier complet";

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
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Image PNG (*.png)|*.png", FileName = "diagnostic-modele.png" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { ExportChartsPng(dialog.FileName); Message.Text = "Image exportée."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { Message.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
    }

    private void ExportPlotData(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "diagnostic-modele.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { SignalChart.ExportCsv(dialog.FileName, Chart, CommandChart, ResidualChart); Message.Text = "Données exportées."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { Message.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
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

    private UIElement KeyValue(string label, string value, string unit = "")
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        var name = new TextBlock { Text = label, Margin = new Thickness(10, 2, 6, 2), TextWrapping = TextWrapping.Wrap };
        name.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        grid.Children.Add(name);
        var val = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 1, 4, 1), TextAlignment = TextAlignment.Right };
        val.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        var u = new TextBlock { Text = unit, Margin = new Thickness(2, 1, 8, 1), Foreground = (Brush)FindResource("MkrMuted") };
        Grid.SetColumn(u, 2);
        grid.Children.Add(u);
        return new Border { Child = grid, BorderBrush = (Brush)FindResource("MkrBorder"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private void RefreshSidePanels()
    {
        ParameterTable.Children.Clear();
        QualityTable.Children.Clear();
        if (_shown is not { } study)
        {
            FormulaHost.Children.Clear();
            FormulaFamily.Text = "Aucune étude sélectionnée";
            ModelTable.ItemsSource = null;
            return;
        }
        var result = study.Result;
        var gainUnit = study.Family == ModelFamily.Integrateur ? $"{study.PvUnit}/({study.MvUnit}·s)" : $"{study.PvUnit}/{study.MvUnit}";
        BuildFormula(study.Family);
        ModelPanel.Header = study.Family switch
        {
            ModelFamily.SOPDT => "Modèle identifié · SOPDT",
            ModelFamily.Integrateur => "Modèle identifié · intégrateur",
            _ => "Modèle identifié · FOPDT"
        };
        ParameterTable.Children.Add(KeyValue(study.Family == ModelFamily.Integrateur ? "Pente k′" : "Gain du procédé K", result.Gain.ToString("G5", CultureInfo.CurrentCulture), gainUnit));
        if (result.TauSeconds is { } tau) ParameterTable.Children.Add(KeyValue(result.SecondTauSeconds is null ? "Constante de temps τ" : "Constante lente τ₁", tau.ToString("G5", CultureInfo.CurrentCulture), "s"));
        if (result.SecondTauSeconds is { } fast) ParameterTable.Children.Add(KeyValue("Constante rapide τ₂", fast.ToString("G5", CultureInfo.CurrentCulture), "s"));
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
            if (FitPercent(pts) is { } fitValue) QualityTable.Children.Add(KeyValue($"FIT ({label}, NRMSE)", fitValue.ToString("F1", CultureInfo.CurrentCulture), "%"));
            var finite = pts.Where(p => double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
            if (finite.Length > 0)
            {
                var rmse = Math.Sqrt(finite.Average(p => (p.Observed - p.Predicted) * (p.Observed - p.Predicted)));
                QualityTable.Children.Add(KeyValue($"RMSE ({label})", rmse.ToString("G3", CultureInfo.CurrentCulture), study.PvUnit));
                var mean = finite.Average(p => p.Observed);
                var total = finite.Sum(p => (p.Observed - mean) * (p.Observed - mean));
                if (total > 0) QualityTable.Children.Add(KeyValue($"R² ({label})", (1 - finite.Sum(p => (p.Observed - p.Predicted) * (p.Observed - p.Predicted)) / total).ToString("F3", CultureInfo.CurrentCulture)));
            }
            if (Iae(pts) is { } iae) QualityTable.Children.Add(KeyValue($"IAE ({label})", iae.ToString("G3", CultureInfo.CurrentCulture), study.PvUnit + "·s"));
        }
        if (study.Assessment is { } decision)
            VerdictCard.ToolTip = $"{VerdictDetail.Text} Seuil de RMSE : {decision.Criteria.MaximumRmse.ToString("G3", CultureInfo.CurrentCulture)} {study.PvUnit}.";

        var rows = (StudyList.ItemsSource as IEnumerable<IdentificationStudy> ?? []).Where(item => item.TrainingId == study.TrainingId && item.ValidationId == study.ValidationId).GroupBy(item => item.Family).Select(group => group.OrderByDescending(item => item.CalculatedAt).First()).Select(item => new ModelRow
        {
            Study = item,
            Family = item.Family == ModelFamily.Integrateur ? "Intégrateur" : item.Family.ToString(),
            Fit = FitPercent(item.Result.Validation) is { } f ? f.ToString("F1", CultureInfo.CurrentCulture) : "—",
            Rmse = item.Result.Validation.Rmse.ToString("G3", CultureInfo.CurrentCulture),
            Decision = item.Assessment is { Accepted: true } ? "accepté" : item.Assessment is not null ? "refusé" : "non décidé"
        }).ToList();
        _tableSync = true;
        ModelTable.ItemsSource = rows;
        ModelTable.SelectedItem = rows.FirstOrDefault(row => row.Study.Id == study.Id);
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
        var category = PlotSource.SelectedIndex == 0 ? "Estimation" : "Validation indépendante";
        PlotLegend.Text = fit is null ? "" : $"{category} · {fit.Points.Length} points enregistrés, affichés sans modification.";
        if (fit is null || _shown is not { } study)
        {
            ClearCharts();
            return;
        }
        var points = fit.Points.Where(p => double.IsFinite(p.Seconds) && double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
        var seconds = points.Select(p => p.Seconds).ToArray();
        foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) chart.XLabel = "Temps après l'échelon (s)";
        var horizon = seconds.Length > 0 ? seconds[^1] : 1;
        var first = seconds.Length > 0 ? seconds[0] : 0;
        Chart.SetTracks(new[]
        {
            new ChartTrack("Réponse du procédé et modèle identifié", study.PvUnit, new[]
            {
                new ChartSeries("Mesure observée", "MkrSignal1", seconds, points.Select(p => p.Observed).ToArray()),
                new ChartSeries("Modèle identifié", "MkrSignal2", seconds, points.Select(p => p.Predicted).ToArray())
            }) { YLabel = $"Mesure ({study.PvUnit})" }
        });
        CommandChart.SetTracks(new[]
        {
            new ChartTrack("Commande (MV)", study.MvUnit, new[]
            {
                new ChartSeries("Échelon appliqué", "MkrSignal3", new[] { first, 0, 0, horizon }, new[] { 0, 0, fit.InputStep, fit.InputStep })
            }) { YLabel = $"MV ({study.MvUnit})" }
        });
        ResidualChart.SetTracks(new[]
        {
            new ChartTrack("Résidus (mesure − modèle)", study.PvUnit, new[]
            {
                new ChartSeries("Résidu", "MkrSignal4", seconds, points.Select(p => p.Observed - p.Predicted).ToArray())
            }) { YLabel = $"Résidu ({study.PvUnit})" }
        });
        // A and B are the bounds the user selected by dragging on the chart (left button); the shading is that range. Nothing is shown until then.
        if (_window is { } range)
        {
            Chart.SetEvents(new[] { (range.From, "A"), (range.To, "B") });
            Chart.SetHighlight(range.From, range.To);
            CommandChart.SetHighlight(range.From, range.To);
            ResidualChart.SetHighlight(range.From, range.To);
            PlotLegend.Text += $" · plage A–B : de {range.From:G4} s à {range.To:G4} s (indicateurs recalculés sur cette plage, sans nouvelle estimation).";
        }
        else
        {
            Chart.SetEvents(Array.Empty<(double, string)>());
            foreach (var chart in new[] { Chart, CommandChart, ResidualChart }) chart.SetHighlight(null, null);
            PlotLegend.Text += " · glissez sur le graphique pour choisir une plage A–B.";
        }
        RefreshSidePanels();
        var shownPoints = _window is { } w ? points.Count(p => p.Seconds >= w.From && p.Seconds <= w.To) : points.Length;
        var fitText = FitPercent(_window is { } w2 ? points.Where(p => p.Seconds >= w2.From && p.Seconds <= w2.To).ToArray() : points) is { } fv ? $"   ·   FIT : {fv.ToString("F1", CultureInfo.CurrentCulture)} %" : "";
        Facts = $"Essai : {category.ToLowerInvariant()}   ·   Durée : {horizon.ToString("G5", CultureInfo.CurrentCulture)} s   ·   Fenêtre : " +
                (_window is { } w3 ? $"{w3.From.ToString("G4", CultureInfo.CurrentCulture)} – {w3.To.ToString("G4", CultureInfo.CurrentCulture)} s" : "essai complet") +
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
            "preview_mapping" => "Sélectionnez des colonnes de temps, de sortie de commande et de mesure distinctes.",
            "preview_time" => "Les horodatages doivent être valides, sans heure locale inexistante ou ambiguë (changement d’heure), et strictement croissants.",
            "preview_data" => "La sortie de commande et la mesure doivent contenir des nombres finis sur chaque ligne pour cet aperçu.",
            "preview_provenance" => "L’empreinte de la source ne correspond plus à l’import sélectionné.",
            "fit_same_source_window" => "Deux essais d’un même enregistrement exigent deux plages explicites (bouton « Rechercher les essais »).",
            "fit_same_source_overlap" => "Les deux plages d’un même enregistrement se recouvrent : choisissez deux échelons distincts.",
            "arx_mapping" => "Colonnes, plage de lignes ou tolérances invalides pour l'identification ARX / OE.",
            "arx_time" => "Horodatages invalides, non croissants, ou moins de 40 échantillons dans un essai.",
            "arx_sampling" => "Pas d'échantillonnage irrégulier : MKR n'interpole jamais les mesures. Rééchantillonnez l'enregistrement à pas fixe à la source.",
            "arx_excitation" => "La commande est quasi constante sur un essai : il n'y a pas d'information sur la dynamique.",
            "arx_ill_conditioned" => "Estimation mal conditionnée : excitation insuffisante ou signaux redondants.",
            "arx_data" => "Pas assez d'échantillons pour estimer un modèle ARX / OE.",
            "arx_closed_loop" => "Les essais en boucle fermée ne se traitent pas par ARX : utilisez l'identification en boucle fermée.",
            "fit_window" => "La plage sélectionnée est invalide ou ne couvre pas suffisamment l'essai. Vérifiez les points avant/après l'échelon.",
            "fit_duplicate_source" => "Les deux imports contiennent exactement les mêmes octets. Choisissez un essai de validation acquis séparément.",
            "fit_independence" => "Les périodes des deux essais se recouvrent en UTC. Vérifiez les horodatages et les plages sélectionnées, puis choisissez deux essais disjoints. Deux segments consécutifs d’un historique se recouvrent volontairement : raccourcissez la fin de la première plage pour qu’elle précède le début de la seconde.",
            "fit_step" => "Sélectionnez une plage qui montre la commande avant et après un seul changement de niveau. Si la commande est déjà au nouveau niveau à la première mesure, utilisez un enregistrement qui commence avant le changement. Vérifiez aussi la stabilité des deux plateaux dans la tolérance déclarée.",
            "fit_baseline" => "La mesure avant l’échelon dépasse la tolérance déclarée.",
            "fit_response" => "La variation de la mesure ne suffit pas pour estimer un modèle.",
            "fit_amplitude" => "Les amplitudes absolues des échelons doivent rester dans un rapport de 0,5 à 2.",
            "fit_data" => "Vérifiez les valeurs numériques de la sortie de commande et de la mesure : les données manquantes et non finies empêchent l’estimation.",
            "fit_time" => "Les horodatages doivent être valides, sans heure locale inexistante ou ambiguë (changement d’heure), et strictement croissants. Pour une heure locale, le format et le fuseau sont ceux déclarés à l’import.",
            "fit_recorded_mapping" => "Le rôle ou l’unité déclaré contredit le mapping enregistré lors de l’import.",
            "fit_integrator_tail" => "L’essai d’estimation ne montre pas une dérive suffisante en fin d’échelon.",
            "pi_domain" => "Le modèle et le temps de réponse sont hors du domaine de calcul PI.",
            "pi_range" => "La combinaison demandée dépasse la plage numérique des réglages PI.",
            "fit_dependency" => "Installez les dépendances Python du fichier engine/requirements.txt avec l’interpréteur MKR.",
            "broida_plateau" => "Broïda refusée : la fin de l’essai ne présente pas un plateau stable de la mesure dans la tolérance.",
            "broida_shape" => "Broïda refusée : réponse non monotone ou dépassement incompatibles avec l’hypothèse.",
            "broida_response" => "Broïda refusée : variation finale de la mesure insuffisante.",
            "broida_horizon" => "Broïda refusée : horizon insuffisant pour établir une valeur finale et la dynamique.",
            "broida_crossing" => "Broïda refusée : passages 28 % / 40 % indéterminables ou incohérents.",
            "broida_domain" => "Broïda refusée : les points conduisent à un retard négatif ou à une constante non physique.",
            "fit_solver" => "L’estimateur n’a pas convergé dans les limites prévues.",
            "fit_tolerance" => "Les tolérances de la sortie de commande et de la mesure doivent être des nombres strictement positifs.",
            "fit_comparison_limits" => "Les critères d’acceptation du modèle (RMSE, corrélation) sont invalides.",
            "drift_mapping" or "drift_domain" => "Les paramètres du modèle enregistré ne conviennent pas au contrôle de dérive.",
            "engine_failure" => "Le moteur de calcul a échoué de façon inattendue : relancez l’opération ; si cela se reproduit, consultez le journal MKR.",
            "engine_non_finite" => "Le calcul a produit une valeur non finie : vérifiez l’échelle des données et les paramètres saisis.",
            _ => "Vérifiez les colonnes et les données des deux imports."
        } : error is TimeoutException ? error.Message : "Étude indisponible. Vérifiez le stockage et le journal MKR.";
    }
}
