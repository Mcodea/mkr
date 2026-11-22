using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class IdentificationWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly CancellationTokenSource _lifetime = new();
    private IdentificationStudy? _shown;
    private sealed record WindowChoice(Guid ImportId, StepColumns Columns, StepWindow Window);
    private WindowChoice? _trainingSelection;
    private WindowChoice? _validationSelection;
    private double? _plotFrom;
    private double? _plotTo;
    private double? _dragStartX;
    private readonly IReadOnlyList<ImportSummary> _sources;

    internal IdentificationWindow(IdentificationWorkspace workspace)
    {
        _workspace = workspace;
        InitializeComponent();
        FamilyChoice.ItemsSource = Enum.GetValues<ModelFamily>();
        FamilyChoice.SelectedItem = ModelFamily.FOPDT;
        _sources = _workspace.Sources();
        TrainingSource.ItemsSource = ValidationSource.ItemsSource = _sources;
        StudyList.ItemsSource = _workspace.Studies();
        if (_sources.Count < 2)
            Message.Text = "Importez deux essais CSV dans ce projet.";
        Closed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };
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
            var columns = source.SelectedItem is ImportSummary item ? _workspace.Columns(item) : [];
            time.ItemsSource = mv.ItemsSource = pv.ItemsSource = columns;
            time.SelectedIndex = mv.SelectedIndex = pv.SelectedIndex = -1;
            Confirmed.IsChecked = false;
        }
        catch (Exception error) { ShowError(error); }
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
            Message.Text = "Sélectionnez un import et trois colonnes distinctes (temps, MV, PV) pour visualiser les signaux.";
            return;
        }

        Form.IsEnabled = false;
        Message.Text = "Lecture des courbes brutes en cours…";
        var token = _lifetime.Token;
        try
        {
            var preview = await _workspace.PreviewTrialAsync(import, columns, token);
            if (token.IsCancellationRequested)
                return;
            Message.Text = "Courbes chargées depuis le CSV enregistré, sans modification de l'étude.";
            var window = new TrialPreviewWindow(preview, import.FileName,
                string.IsNullOrWhiteSpace(MvUnit.Text) ? "unité MV non renseignée" : MvUnit.Text.Trim(),
                string.IsNullOrWhiteSpace(PvUnit.Text) ? "unité PV non renseignée" : PvUnit.Text.Trim())
                { Owner = this };
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
        finally { Form.IsEnabled = true; }
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

        if (training.Id == validation.Id)
        {
            Message.Text = "Le même import est sélectionné deux fois. Sélectionnez un autre essai pour la validation.";
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

        Form.IsEnabled = false;
        Message.Text = "Estimation et évaluation…";
        var token = _lifetime.Token;
        try
        {
            ModelComparison? accepted = null;
            if (sender == CompareModels)
            {
                if (!ReadTolerance(MaximumRmse, out var rmse) || !ReadTolerance(MaximumLag, out var lag) || lag > 1)
                {
                    Message.Text = "Indiquez une RMSE maximale positive en unité PV et une limite de corrélation supérieure à 0 et inférieure ou égale à 1.";
                    return;
                }
                var comparison = await _workspace.CompareAsync(training, trainColumns, validation, valColumns, mvUnit, pvUnit,
                    mvTolerance, pvTolerance, rmse, lag, token, _trainingSelection?.Window, _validationSelection?.Window);
                token.ThrowIfCancellationRequested();
                var lines = new List<string> { $"Essai de validation : {validation.FileName}",
                    $"Seuils : RMSE ≤ {rmse:G5} {pvUnit} · |r₁| ≤ {lag:G5}" };
                lines.AddRange(CandidateLines(comparison, pvUnit));
                lines.Add(comparison.Selected is { } selected
                    ? $"Modèle proposé : {selected}, ordre le plus faible satisfaisant les critères."
                    : "Aucun des deux modèles ne satisfait les critères.");
                Message.Text = lines[^1];
                if (comparison.Selected is null)
                {
                    lines.Add("Avant un nouvel essai, examinez les courbes et les résidus de validation. Vérifiez les unités, les plages choisies et les conditions du procédé pour les deux acquisitions.");
                    Message.Text = string.Join(Environment.NewLine, lines);
                    return;
                }
                lines.Add("Estimer et enregistrer ce modèle avec les critères et les résultats de comparaison ?");
                if (MessageBox.Show(this, string.Join(Environment.NewLine, lines), "Comparaison sur essai indépendant",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                family = Enum.Parse<ModelFamily>(comparison.Selected);
                FamilyChoice.SelectedItem = family;
                accepted = comparison;
            }
            var study = await _workspace.CalculateAsync(family, training, trainColumns, validation, valColumns,
                mvUnit, pvUnit, mvTolerance, pvTolerance, token,
                _trainingSelection?.Window, _validationSelection?.Window, accepted);
            var studies = _workspace.Studies();
            StudyList.ItemsSource = studies;
            StudyList.SelectedItem = studies.First(item => item.Id == study.Id);
            Message.Text = "Étude exploratoire enregistrée.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = true; }
    }

    private static IEnumerable<string> CandidateLines(ModelComparison comparison, string pvUnit)
    {
        foreach (var (name, candidate) in comparison.Candidates)
        {
            var reasons = candidate.RejectionReasons.Select(reason => reason switch
            {
                "validation_rmse" => "erreur de validation excessive",
                "validation_residuals" => "corrélation résiduelle excessive ou indéterminée",
                "parameter_bound" => "paramètre en borne",
                "short_horizon" => "durée d’essai insuffisante",
                "fast_pole_resolution" => "pôle rapide mal résolu",
                "ill_conditioned" => "estimation mal conditionnée",
                _ => reason
            });
            yield return $"{name} · RMSE {candidate.ValidationRmse:G5} {pvUnit} · r₁ {candidate.ValidationResidualLagOne?.ToString("G4") ?? "indéterminé"} · " +
                (candidate.RejectionReasons.Length == 0 ? "critères satisfaits" : string.Join(", ", reasons));
        }
    }

    private void ShowStudy(object sender, SelectionChangedEventArgs args)
    {
        _shown = StudyList.SelectedItem as IdentificationStudy;
        _plotFrom = _plotTo = _dragStartX = null;
        OpenLoopButton.Visibility = PiPanel.Visibility = _shown?.Family == ModelFamily.FOPDT ? Visibility.Visible : Visibility.Collapsed;
        SopdtPanel.Visibility = _shown?.Family == ModelFamily.SOPDT ? Visibility.Visible : Visibility.Collapsed;
        SopdtReport.Text = "";
        TargetPanel.Visibility = _shown?.Family is ModelFamily.FOPDT or ModelFamily.SOPDT
            ? Visibility.Visible : Visibility.Collapsed;
        ResponseProfile.SelectedIndex = 0;
        UpdateResponseTarget();
        PiList.ItemsSource = null;
        PiReport.Text = "";
        if (_shown is { Family: ModelFamily.FOPDT } model)
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
            Plot.Children.Clear();
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
              $"PV terminale mesurée = {evidence.TerminalPv:G6} {study.PvUnit} " +
              $"sur {evidence.TerminalSamples} échantillons.\n"
            : "";

        FitReport.Text = $"Modèle {family} exploratoire · K = {result.Gain:G5} {gainUnit}" +
            $"{timeConstants} · θ = {result.DelaySeconds:G5} s\n" +
            $"Estimation ({trainingName}) : {Metrics(result.Training, study.PvUnit)}\n" +
            $"Validation ({validationName}) : {Metrics(result.Validation, study.PvUnit)}\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {study.EngineVersion}\n{windows}{broida}{uncertainty}\n{string.Join(" ", warnings)}";
        if (study.Comparison is { } saved)
        {
            FitReport.Text += $"\nComparaison enregistrée · version {saved.AlgorithmVersion} · modèle retenu {saved.Selected}\n" +
                $"Seuils : RMSE ≤ {saved.Criteria.MaximumRmse:G5} {study.PvUnit} · |r₁| ≤ {saved.Criteria.MaximumResidualLagOne:G5}\n" +
                string.Join(Environment.NewLine, CandidateLines(saved, study.PvUnit));
        }
        DrawPlot();
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

        ResponseTarget.Text = model.DelaySeconds.ToString("R", CultureInfo.CurrentCulture);
        TargetHelp.Text = "SIMC, Skogestad (2003) · Pour explorer une réponse rapide, τc = retard du modèle. Vérifiez en simulation le temps obtenu et l’effort de commande.";
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
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = TargetPanel.IsEnabled = PiPanel.IsEnabled = true; }
    }

    private void OpenLoop(object sender, RoutedEventArgs args)
    {
        if (_shown is { Family: ModelFamily.FOPDT } model)
            new OpenLoopWindow(_workspace, model) { Owner = this }.ShowDialog();
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
            new SimulationWindow(_workspace, model, tuning) { Owner = this }.ShowDialog();
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ShowPi(object sender, SelectionChangedEventArgs args)
    {
        SimulateSavedPi.IsEnabled = PiList.SelectedItem is PiStudy;
        if (_shown is not { } model || PiList.SelectedItem is not PiStudy pi)
        {
            PiReport.Text = "";
            return;
        }
        var result = pi.Result;
        var ratio = $"{model.MvUnit}/{model.PvUnit}";
        string Format(PiCoefficients settings) =>
            $"Kp = {settings.Kp:G5} {ratio}, Ti = {settings.TiSeconds:G5} s, Ki parallèle = {settings.ParallelKi:G5} {ratio}/s";
        var tight = result.Warnings.Contains("tight_response")
            ? "\nLe temps choisi est inférieur au retard estimé. Examinez la robustesse." : "";
        PiReport.Text = $"Réglage enregistré pour la simulation · cible {result.ResponseSeconds:G5} s · méthode {result.AlgorithmVersion} · moteur {pi.EngineVersion}\n" +
            $"IMC/Lambda : {Format(result.Imc)}\nSIMC : {Format(result.Simc)}\n" +
            "Forme idéale continue, e = SP − PV. Modèle exploratoire et contraintes d’actionneur à vérifier." + tight;
    }

    private static string FormatWindow(StepWindow? window) => window is { } w
        ? $"indices source {w.RowStart}..{w.RowEnd} (inclus)"
        : "fichier complet";

    private static string Metrics(FitDiagnostics fit, string unit) =>
        $"{fit.Samples} points · RMSE {fit.Rmse:G4} {unit} · MAE {fit.Mae:G4} {unit} · " +
        $"R² {fit.RSquared?.ToString("G4") ?? "—"} · corrélation résiduelle lag 1 {fit.ResidualLagOne?.ToString("G4") ?? "—"}";

    private void ResizePlot(object sender, SizeChangedEventArgs args) => DrawPlot();

    private FitDiagnostics? DisplayedDiagnostics() => _shown is not { } study ? null :
        PlotSource.SelectedIndex == 0 ? study.Result.Training : study.Result.Validation;

    private void ChangePlotSource(object sender, SelectionChangedEventArgs args)
    {
        _plotFrom = _plotTo = _dragStartX = null;
        DrawPlot();
    }

    private void ResetPlotZoom(object sender, RoutedEventArgs args)
    {
        _plotFrom = _plotTo = _dragStartX = null;
        DrawPlot();
    }

    private void PlotStartSelection(object sender, MouseButtonEventArgs args)
    {
        var points = DisplayedDiagnostics()?.Points;
        if (points is not { Length: >= 2 })
            return;
        var x = args.GetPosition(Plot).X;
        var start = _plotFrom ?? points[0].Seconds;
        var end = _plotTo ?? points[^1].Seconds;
        if (!IdentificationDiagnosticsPlot.TryTimeAt(x, Plot.ActualWidth, start, end, out _))
            return;
        _dragStartX = x;
        Plot.CaptureMouse();
    }

    private void PlotEndSelection(object sender, MouseButtonEventArgs args)
    {
        if (_dragStartX is not { } x0)
            return;
        _dragStartX = null;
        Plot.ReleaseMouseCapture();
        var points = DisplayedDiagnostics()?.Points;
        if (points is not { Length: >= 2 })
            return;
        var x1 = args.GetPosition(Plot).X;
        if (Math.Abs(x1 - x0) < 12)
            return;
        var start = _plotFrom ?? points[0].Seconds;
        var end = _plotTo ?? points[^1].Seconds;
        if (!IdentificationDiagnosticsPlot.TryTimeAt(x0, Plot.ActualWidth, start, end, out var a) ||
            !IdentificationDiagnosticsPlot.TryTimeAt(x1, Plot.ActualWidth, start, end, out var b))
            return;
        var first = Math.Min(a, b);
        var last = Math.Max(a, b);
        // Show only intervals containing at least two genuine stored samples.
        if (points.Count(p => p.Seconds >= first && p.Seconds <= last) < 2)
        {
            Message.Text = "Sélection trop courte : choisissez au moins deux points mesurés.";
            return;
        }
        _plotFrom = first;
        _plotTo = last;
        DrawPlot();
    }

    private void PlotCancelSelection(object sender, MouseEventArgs args)
    {
        if (_dragStartX is null)
            return;
        _dragStartX = null;
        Plot.ReleaseMouseCapture();
    }

    private void DrawPlot()
    {
        if (Plot is null || PlotSource is null)
            return;
        var fit = DisplayedDiagnostics();
        var category = PlotSource.SelectedIndex == 0 ? "Estimation" : "Validation indépendante";
        PlotLegend.Text = fit is null ? "" :
            $"{category} · PV ({_shown!.PvUnit}) · bleu : mesurée · orange : prédite · violet : résidu (mesurée − prédite)" +
            (_plotFrom.HasValue && _plotTo.HasValue
                ? $" · Zoom [{_plotFrom.Value:G5}, {_plotTo.Value:G5}] s" : "");
        IdentificationDiagnosticsPlot.Draw(Plot, fit?.Points, _shown?.PvUnit ?? "",
                                           _plotFrom, _plotTo);
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
            "preview_mapping" => "Sélectionnez des colonnes de temps, MV et PV distinctes.",
            "preview_time" => "Les horodatages doivent être valides, UTC explicites et strictement croissants.",
            "preview_data" => "MV et PV doivent contenir des nombres finis sur chaque ligne pour cet aperçu.",
            "preview_provenance" => "L’empreinte de la source ne correspond plus à l’import sélectionné.",
            "fit_window" => "La plage sélectionnée est invalide ou ne couvre pas suffisamment l'essai. Vérifiez les points avant/après l'échelon.",
            "fit_duplicate_source" => "Les deux imports contiennent exactement les mêmes octets. Choisissez un essai de validation acquis séparément.",
            "fit_independence" => "Les périodes des deux essais se recouvrent en UTC. Vérifiez les horodatages et les plages sélectionnées, puis choisissez deux essais disjoints.",
            "fit_step" => "Sélectionnez une plage qui montre la commande avant et après un seul changement de niveau. Si la commande est déjà au nouveau niveau à la première mesure, utilisez un enregistrement qui commence avant le changement. Vérifiez aussi la stabilité des deux plateaux dans la tolérance déclarée.",
            "fit_baseline" => "La PV avant l’échelon dépasse la tolérance déclarée.",
            "fit_response" => "La variation de PV ne suffit pas pour estimer un modèle.",
            "fit_amplitude" => "Les amplitudes absolues des échelons doivent rester dans un rapport de 0,5 à 2.",
            "fit_data" => "Vérifiez les valeurs numériques MV/PV : les données manquantes et non finies empêchent l’estimation.",
            "fit_time" => "Les horodatages doivent inclure un décalage UTC et être strictement croissants.",
            "fit_recorded_mapping" => "Le rôle ou l’unité déclaré contredit le mapping enregistré lors de l’import.",
            "fit_integrator_tail" => "L’essai d’estimation ne montre pas une dérive suffisante en fin d’échelon.",
            "pi_domain" => "Le modèle et le temps de réponse sont hors du domaine de calcul PI.",
            "pi_range" => "La combinaison demandée dépasse la plage numérique des réglages PI.",
            "fit_dependency" => "Installez les dépendances Python du fichier engine/requirements.txt avec l’interpréteur MKR.",
            "broida_plateau" => "Broïda refusée : la fin de l’essai ne présente pas un plateau PV stable dans la tolérance.",
            "broida_shape" => "Broïda refusée : réponse non monotone ou dépassement incompatibles avec l’hypothèse.",
            "broida_response" => "Broïda refusée : variation finale PV insuffisante.",
            "broida_horizon" => "Broïda refusée : horizon insuffisant pour établir une valeur finale et la dynamique.",
            "broida_crossing" => "Broïda refusée : passages 28 % / 40 % indéterminables ou incohérents.",
            "broida_domain" => "Broïda refusée : les points conduisent à un retard négatif ou à une constante non physique.",
            "fit_solver" => "L’estimateur n’a pas convergé dans les limites prévues.",
            _ => "Vérifiez les colonnes et les données des deux imports."
        } : error is TimeoutException ? error.Message : "Étude indisponible. Vérifiez le stockage et le journal MKR.";
    }
}
