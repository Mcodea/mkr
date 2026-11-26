using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>
/// Final validation of a study: what the project really contains (accepted model, PI tuning, stored simulation) gathered in one place, with its limits.
/// It states facts and flags what is missing; it never qualifies the tuning for industrial use, and shows no current-versus-proposed comparison
/// because the tuning in service is not an input of MKR (it never reads or writes the controller).
/// </summary>
public partial class ReportView : UserControl, IStepView
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private PiStudy? _tuning;
    private PiSimulationStudy? _simulation;

    internal ReportView(IdentificationWorkspace workspace, IdentificationStudy model)
    {
        InitializeComponent();
        _workspace = workspace;
        _model = model;
        Chart.CursorTextChanged += text => CursorLine.Text = text.Length == 0 ? "Survolez le graphique pour lire les valeurs au curseur." : text;
        Subject.Text = $"Étude : {StudyNameConverter.Name(model)}";
        try
        {
            _tuning = workspace.PiStudies(model.Id).OrderByDescending(item => item.CalculatedAt).FirstOrDefault();
            _simulation = _tuning is null ? null : workspace.Simulations(_tuning.Id).OrderByDescending(item => item.CalculatedAt).FirstOrDefault();
        }
        catch (Exception error) when (error is System.IO.IOException or InvalidOperationException) { Trace.TraceError("report.load {0}", error.GetType().Name); }
        Build();
    }

    public void Cancel() { }

    private static string F(double? value, string format = "G4") => value is { } v ? v.ToString(format, CultureInfo.CurrentCulture) : "—";

    private UIElement Line(string glyph, string brush, string text)
    {
        var row = new Grid { Margin = new Thickness(10, 4, 10, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
        mark.SetResourceReference(TextBlock.ForegroundProperty, brush);
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        Grid.SetColumn(label, 1);
        row.Children.Add(mark);
        row.Children.Add(label);
        return row;
    }

    private TextBlock Cell(string text, int row, int column, bool header = false)
    {
        var cell = new TextBlock
        {
            Text = text, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
            FontWeight = header || column == 0 ? FontWeights.SemiBold : FontWeights.Normal, TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right
        };
        cell.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        if (header) cell.SetResourceReference(TextBlock.BackgroundProperty, "MkrSurfaceAlt");
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        return cell;
    }

    private static void Shape(Grid grid, int rows, params double[] widths)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
        for (var i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }

    private void Build()
    {
        var accepted = _model.IsAcceptedForTuning;
        Checklist.Children.Clear();
        const string ok = "", no = "", warn = "";
        Checklist.Children.Add(Line(accepted ? ok : no, accepted ? "MkrSuccess" : "MkrDanger",
            accepted ? "Modèle accepté à la validation indépendante (essai distinct de l'estimation)." : "Modèle non accepté : aucun réglage ni simulation n'en est déduit."));
        Checklist.Children.Add(Line(_tuning is not null ? ok : warn, _tuning is not null ? "MkrSuccess" : "MkrWarning",
            _tuning is not null ? "Réglage PI calculé (Lambda et SIMC) et enregistré." : "Aucun réglage PI enregistré : calculez-le à l'étape Réglage."));
        Checklist.Children.Add(Line(_simulation is not null ? ok : warn, _simulation is not null ? "MkrSuccess" : "MkrWarning",
            _simulation is not null ? "Simulation en boucle fermée enregistrée sur le modèle identifié." : "Aucune simulation enregistrée : lancez-la à l'étape Simulation."));
        var margins = _tuning?.Result.Simc.Margins;
        if (margins is not null)
            Checklist.Children.Add(Line(margins.Warnings.Length == 0 ? ok : warn, margins.Warnings.Length == 0 ? "MkrSuccess" : "MkrWarning",
                "Marges de la boucle nominale (SIMC) : " + margins.Describe() + "."));
        else if (_tuning is not null)
            Checklist.Children.Add(Line(warn, "MkrWarning", "Marges de stabilité non enregistrées pour ce réglage (étude antérieure à leur calcul)."));
        Checklist.Children.Add(Line("", "MkrMuted", "Résultats théoriques sur le modèle identifié : à qualifier sur l'installation. MKR n'écrit jamais dans l'automate."));

        // final parameters
        Shape(ParameterGrid, 3, 1.4, 1, 1, 1);
        string[] heads = ["Méthode", $"Kp ({_model.MvUnit}/{_model.PvUnit})", "Ti (s)", "Ki parallèle"];
        for (var c = 0; c < heads.Length; c++) ParameterGrid.Children.Add(Cell(heads[c], 0, c, header: true));
        if (_tuning is { } tuning)
        {
            void Row(int row, string name, PiCoefficients pi)
            {
                ParameterGrid.Children.Add(Cell(name, row, 0));
                ParameterGrid.Children.Add(Cell(F(pi.Kp), row, 1));
                ParameterGrid.Children.Add(Cell(F(pi.TiSeconds), row, 2));
                ParameterGrid.Children.Add(Cell(F(pi.ParallelKi), row, 3));
            }
            Row(1, "Lambda (Dahlin)", tuning.Result.Imc);
            Row(2, "SIMC", tuning.Result.Simc);
            ParameterNote.Text = $"Cible de boucle fermée τc = {F(tuning.Result.ResponseSeconds)} s. Bloc constructeur : valeurs documentaires non qualifiées, à demander dans l'étape Bloc ; le réglage actuellement en service n'est pas une donnée de MKR, aucune comparaison « actuel / proposé » n'est donc affichée.";
        }
        else ParameterNote.Text = "Aucun réglage enregistré.";

        // indicators
        Shape(IndicatorGrid, 3, 1.3, 1, 1, 1, 1);
        string[] indicators = ["Méthode", $"IAE ({_model.PvUnit}·s)", $"Dépassement ({_model.PvUnit})", "Réponse ±5 % (s)", "Marge de phase (°)"];
        for (var c = 0; c < indicators.Length; c++) IndicatorGrid.Children.Add(Cell(indicators[c], 0, c, header: true));
        var unavailable = false;
        string N(double? value, string format = "G4") { if (value is null) { unavailable = true; return "—"; } return F(value, format); }
        if (_simulation is { } sim && _tuning is { } t2)
        {
            void Row(int row, string name, SimulationRun run, PiCoefficients pi)
            {
                IndicatorGrid.Children.Add(Cell(name, row, 0));
                IndicatorGrid.Children.Add(Cell(N(run.Iae), row, 1));
                IndicatorGrid.Children.Add(Cell(N(run.SetpointResponse?.OvershootPv), row, 2));
                IndicatorGrid.Children.Add(Cell(N(run.SetpointResponse?.SettlingSeconds), row, 3));
                IndicatorGrid.Children.Add(Cell(N(pi.Margins?.PhaseMarginDegrees, "F1"), row, 4));
            }
            Row(1, "Lambda (Dahlin)", sim.Result.Imc, t2.Result.Imc);
            Row(2, "SIMC", sim.Result.Simc, t2.Result.Simc);
            IndicatorNote.Text = (unavailable ? "— : indicateur non disponible (seuil non franchi dans la fenêtre simulée, ou marge non enregistrée pour ce réglage). " : "") +
                $"Saturation de la commande : Lambda {F(sim.Result.Imc.SaturationSeconds)} s, SIMC {F(sim.Result.Simc.SaturationSeconds)} s. Aucun critère d'acceptation n'est saisi : MKR ne déclare pas d'objectif « atteint ».";
        }
        else IndicatorNote.Text = "Indicateurs indisponibles : aucune simulation enregistrée.";

        var limits = new List<string>();
        limits.AddRange(_model.Result.Warnings.Select(code => "Modèle : " + code));
        if (_tuning is not null) limits.AddRange(_tuning.Result.Warnings.Select(code => "Réglage : " + code));
        if (_simulation is not null) limits.AddRange(_simulation.Result.Warnings.Select(code => "Simulation : " + code));
        limits.Add("Simulation sur modèle nominal, sans qualification industrielle : limites et vitesse de l'actionneur, bruit et non-linéarités du procédé réel à vérifier sur installation.");
        Limits.Text = string.Join("\n", limits.Select(line => "· " + line));

        ExportButton.IsEnabled = _simulation is not null && _tuning is not null;
        DrawChart();
    }

    private void DrawChart()
    {
        if (_simulation is not { } study || study.Result.Imc.Points.Length < 3 || study.Result.Simc.Points.Length != study.Result.Imc.Points.Length)
        {
            Chart.SetTracks(Array.Empty<ChartTrack>());
            Empty.Visibility = Visibility.Visible;
            Empty.Text = "Aucune simulation enregistrée pour cette étude. Le graphique de comparaison apparaît dès qu'une simulation est enregistrée à l'étape Simulation.";
            return;
        }
        Empty.Visibility = Visibility.Collapsed;
        var imc = study.Result.Imc.Points;
        var simc = study.Result.Simc.Points;
        var seconds = imc.Select(p => p.Seconds).ToArray();
        Chart.XLabel = "Temps depuis le début du scénario (s)";
        Chart.SetTracks(new[]
        {
            new ChartTrack("Comparaison des réponses – Lambda et SIMC", _model.PvUnit, new[]
            {
                new ChartSeries("Consigne", "MkrSignal3", new[] { seconds[0], seconds[^1] }, new[] { study.Scenario.Setpoint, study.Scenario.Setpoint }),
                new ChartSeries("Mesure Lambda (Dahlin)", "MkrSignal1", seconds, imc.Select(p => p.Pv).ToArray()),
                new ChartSeries("Mesure SIMC", "MkrSignal2", simc.Select(p => p.Seconds).ToArray(), simc.Select(p => p.Pv).ToArray())
            }) { Weight = 2, YLabel = $"Mesure ({_model.PvUnit})" },
            new ChartTrack("Commande appliquée (écart)", _model.MvUnit, new[]
            {
                new ChartSeries("Lambda (Dahlin)", "MkrSignal1", imc.Where(p => p.Mv.HasValue).Select(p => p.Seconds).ToArray(), imc.Where(p => p.Mv.HasValue).Select(p => p.Mv!.Value).ToArray()),
                new ChartSeries("SIMC", "MkrSignal2", simc.Where(p => p.Mv.HasValue).Select(p => p.Seconds).ToArray(), simc.Where(p => p.Mv.HasValue).Select(p => p.Mv!.Value).ToArray())
            }) { YLabel = $"MV ({_model.MvUnit})" }
        });
    }

    private void ZoomIn(object sender, RoutedEventArgs args) => Chart.ZoomBy(0.7);

    private void ZoomOut(object sender, RoutedEventArgs args) => Chart.ZoomBy(1 / 0.7);

    private void ResetView(object sender, RoutedEventArgs args) => Chart.ResetView();

    private void ExportSheet(object sender, RoutedEventArgs args)
    {
        if (_simulation is not { } simulation || _tuning is not { } tuning) return;
        var dialog = new SaveFileDialog { Title = "Exporter la fiche d'étude", FileName = $"MKR-{simulation.Id:N}.pdf", Filter = "PDF (*.pdf)|*.pdf" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            _workspace.ExportSheet(dialog.FileName, _model, tuning, simulation);
            Message.Text = "Fiche d'étude exportée.";
        }
        catch (Exception error)
        {
            Trace.TraceError("report.export {0}", error.GetType().Name);
            Message.Text = "Export PDF impossible. Vérifiez le dossier de destination et le journal MKR.";
        }
    }
}
