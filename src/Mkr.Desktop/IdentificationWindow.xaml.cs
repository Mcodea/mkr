using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class IdentificationWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly CancellationTokenSource _lifetime = new();
    private IdentificationStudy? _shown;
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

    private void SelectTraining(object sender, SelectionChangedEventArgs args) =>
        Populate(TrainingSource, TrainingTime, TrainingMv, TrainingPv);

    private void SelectValidation(object sender, SelectionChangedEventArgs args) =>
        Populate(ValidationSource, ValidationTime, ValidationMv, ValidationPv);

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

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        var trainColumns = ReadColumns(TrainingTime, TrainingMv, TrainingPv);
        var valColumns = ReadColumns(ValidationTime, ValidationMv, ValidationPv);
        var mvUnit = MvUnit.Text.Trim();
        var pvUnit = PvUnit.Text.Trim();
        if (FamilyChoice.SelectedItem is not ModelFamily family || TrainingSource.SelectedItem is not ImportSummary training ||
            ValidationSource.SelectedItem is not ImportSummary validation || training.Id == validation.Id ||
            trainColumns is null || valColumns is null || mvUnit.Length == 0 || pvUnit.Length == 0 ||
            !ReadTolerance(MvTolerance, out var mvTolerance) || !ReadTolerance(PvTolerance, out var pvTolerance) ||
            Confirmed.IsChecked != true)
        {
            Message.Text = "Indiquez deux imports, trois colonnes distinctes par essai, les unités et tolérances positives, puis confirmez les conditions d’essai.";
            return;
        }

        Form.IsEnabled = false;
        Message.Text = "Estimation et évaluation…";
        var token = _lifetime.Token;
        try
        {
            var study = await _workspace.CalculateAsync(family, training, trainColumns, validation, valColumns,
                mvUnit, pvUnit, mvTolerance, pvTolerance, token);
            var studies = _workspace.Studies();
            StudyList.ItemsSource = studies;
            StudyList.SelectedItem = studies.First(item => item.Id == study.Id);
            Message.Text = "Étude exploratoire enregistrée.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Form.IsEnabled = true; }
    }

    private void ShowStudy(object sender, SelectionChangedEventArgs args)
    {
        _shown = StudyList.SelectedItem as IdentificationStudy;
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
        FitReport.Text = $"Modèle {family} exploratoire · K = {result.Gain:G5} {gainUnit}" +
            $"{timeConstants} · θ = {result.DelaySeconds:G5} s\n" +
            $"Estimation ({trainingName}) : {Metrics(result.Training, study.PvUnit)}\n" +
            $"Validation ({validationName}) : {Metrics(result.Validation, study.PvUnit)}\n" +
            $"Méthode {result.AlgorithmVersion} · moteur {study.EngineVersion}\n{uncertainty}\n{string.Join(" ", warnings)}";
        PlotLegend.Text = $"Validation après l’échelon · PV ({study.PvUnit}) · bleu : mesurée · orange : prédite";
        DrawPlot();
    }

    private static string Metrics(FitDiagnostics fit, string unit) =>
        $"{fit.Samples} points · RMSE {fit.Rmse:G4} {unit} · MAE {fit.Mae:G4} {unit} · " +
        $"R² {fit.RSquared?.ToString("G4") ?? "—"} · corrélation résiduelle lag 1 {fit.ResidualLagOne?.ToString("G4") ?? "—"}";

    private void ResizePlot(object sender, SizeChangedEventArgs args) => DrawPlot();

    private void DrawPlot()
    {
        if (Plot is null)
            return;
        Plot.Children.Clear();
        if (_shown is not { } study || Plot.ActualWidth <= 100)
            return;
        var values = study.Result.Validation.Points;
        var minimum = values.Min(p => Math.Min(p.Observed, p.Predicted));
        var maximum = values.Max(p => Math.Max(p.Observed, p.Predicted));
        var span = maximum > minimum ? maximum - minimum : 1;
        const double left = 75, top = 24, height = 165;
        var width = Plot.ActualWidth - left - 24;
        var seconds = values[^1].Seconds;
        var actual = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2.5 };
        var predicted = new Polyline { Stroke = Brushes.DarkOrange, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 } };
        Plot.Children.Add(new System.Windows.Shapes.Path
        {
            Stroke = Brushes.Gray,
            Data = Geometry.Parse(FormattableString.Invariant($"M {left},{top} L {left},{top + height} L {left + width},{top + height}"))
        });
        foreach (var point in values)
        {
            var x = left + point.Seconds / seconds * width;
            actual.Points.Add(new Point(x, top + (maximum - point.Observed) / span * height));
            predicted.Points.Add(new Point(x, top + (maximum - point.Predicted) / span * height));
        }
        Plot.Children.Add(actual);
        Plot.Children.Add(predicted);
        LabelAt($"{maximum:G4}", 0, top - 12);
        LabelAt($"{minimum:G4}", 0, top + height - 14);
        LabelAt("0 s", left, top + height + 3);
        LabelAt($"{seconds:G4} s", Math.Max(left, left + width - 68), top + height + 3);
    }

    private void LabelAt(string text, double x, double y)
    {
        var label = new TextBlock { Text = text };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        Plot.Children.Add(label);
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
            "fit_independence" => "Les deux essais doivent provenir de fichiers distincts et de plages temporelles disjointes.",
            "fit_step" => "Il faut un seul échelon MV et deux plateaux dans la tolérance déclarée.",
            "fit_baseline" => "La PV avant l’échelon dépasse la tolérance déclarée.",
            "fit_response" => "La variation de PV ne suffit pas pour estimer un modèle.",
            "fit_amplitude" => "Les amplitudes absolues des échelons doivent rester dans un rapport de 0,5 à 2.",
            "fit_data" => "Vérifiez les valeurs numériques MV/PV : les données manquantes et non finies empêchent l’estimation.",
            "fit_time" => "Les horodatages doivent inclure un décalage UTC et être strictement croissants.",
            "fit_recorded_mapping" => "Le rôle ou l’unité déclaré contredit le mapping enregistré lors de l’import.",
            "fit_integrator_tail" => "L’essai d’estimation ne montre pas une dérive suffisante en fin d’échelon.",
            "fit_dependency" => "Installez les dépendances Python du fichier engine/requirements.txt avec l’interpréteur MKR.",
            "fit_solver" => "L’estimateur n’a pas convergé dans les limites prévues.",
            _ => "Vérifiez les colonnes et les données des deux imports."
        } : error is TimeoutException ? error.Message : "Étude indisponible. Vérifiez le stockage et le journal MKR.";
    }
}
