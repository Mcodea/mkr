using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>One row of the study table (display strings only).</summary>
public sealed class StudyRow
{
    public required IdentificationStudy Study { get; init; }
    public string Family => Study.Family.ToString();
    public string Gain { get; init; } = "";
    public string Tau { get; init; } = "";
    public string Delay { get; init; } = "";
    public string Error { get; init; } = "";
    public string Decision { get; init; } = "";
    public string When { get; init; } = "";
}

public partial class WindowView : Window
{
    private static readonly string[] ShortTitles =
        { "Projet", "Données", "Signaux", "Identification", "Validation", "Stratégie", "Bloc", "Réglage", "Simulation", "Rapport" };

    private readonly IImportStore _store;
    private readonly ICsvEngine _engine;
    private readonly Func<Project, IdentificationWorkspace> _createIdentification;
    private IReadOnlyList<JourneyStep> _steps = StudyJourney.Evaluate(null);
    private int _tab;
    private IdentificationWorkspace? _workspace;
    private CsvWorkspace? _csv;
    private Project? _project;
    private readonly List<RadioButton> _tabs = new();

    internal WindowView(IImportStore store, ICsvEngine engine, Func<Project, IdentificationWorkspace> createIdentification)
    {
        _store = store;
        _engine = engine;
        _createIdentification = createIdentification;
        InitializeComponent();
        ThemeChoice.SelectedIndex = (int)ThemeService.Choice;       // Initialize() ran at startup: reflect the active choice
        var version = typeof(WindowView).Assembly.GetName().Version;
        VersionText.Text = version is null ? "MKR · application Windows" : $"MKR {version.Major}.{version.Minor}.{version.Build} · application Windows";
        Chart.CursorTextChanged += text => CursorText.Text = text.Length == 0 ? "Survolez le graphique pour lire les valeurs au curseur." : text;
        ThemeService.Changed += () => { if (_workspace is not null && StudyView.Visibility == Visibility.Visible) UpdateVerdictColour(); };
    }

    private void ThemeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ThemeChoice.SelectedItem is ComboBoxItem { Tag: string tag } && Enum.TryParse(tag, out ThemeChoice choice) && choice != ThemeService.Choice)
            ThemeService.Apply(choice);
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key != System.Windows.Input.Key.F1) return;
        args.Handled = true;
        OpenHelp(sender, args);
    }

    private void OpenHelp(object sender, RoutedEventArgs args) => new HelpWindow { Owner = this }.ShowDialog();
    private void OpenCatalogue(object sender, RoutedEventArgs args) => BlockCatalogueWindow.OpenFor(this, _csv is null ? null : _csv.ReadLoopBrief);

    // ---------- home ----------
    private void ProjectSelected(object sender, SelectionChangedEventArgs args)
    {
        _project = ProjectList.SelectedItem as Project;
        ActiveProject.Text = _project is null ? "Aucun projet sélectionné" : $"Projet : {_project.Name}";
        OpenStudy.IsEnabled = _project is not null;
    }

    private void OpenProject(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project project)
        {
            if (sender is not DataGrid) MessageBox.Show(this, "Sélectionnez un projet dans la liste.", "MKR");
            return;
        }
        try
        {
            new CsvWindow(new CsvWorkspace(project, _store, _engine), _createIdentification(project)) { Owner = this }.ShowDialog();
            if (StudyView.Visibility == Visibility.Visible) LoadStudy();
        }
        catch (Exception error)
        {
            Trace.TraceError("project.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir le projet. Vérifiez le stockage local.", "MKR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---------- study workspace ----------
    private void EnterStudy(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project project) return;
        _project = project;
        HomeView.Visibility = Visibility.Collapsed;
        StudyView.Visibility = Visibility.Visible;
        RibbonHost.Visibility = Visibility.Visible;
        Title = $"MKR — {project.Name}";
        LoadStudy();
    }

    private void LeaveStudy()
    {
        StudyView.Visibility = Visibility.Collapsed;
        RibbonHost.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        Title = "MKR — Espace projets";
    }

    private static string StateGlyph(StepState state) => state switch { StepState.Done => "", StepState.Ready => "", _ => "" };
    private static string StateName(StepState state) => state switch { StepState.Done => "terminée", StepState.Ready => "disponible", _ => "fermée" };

    private void LoadStudy()
    {
        if (_project is null) return;
        try
        {
            _csv = new CsvWorkspace(_project, _store, _engine);
            _workspace = _createIdentification(_project);
            var brief = _csv.ReadLoopBrief();
            var imports = _workspace.Sources().Count;
            var studies = _workspace.Studies();
            var accepted = studies.Count(study => study.IsAcceptedForTuning);
            var described = new[] { brief.Tag, brief.ControlledQuantity, brief.OperatingPoint, brief.Objective, brief.Description }.Any(text => !string.IsNullOrWhiteSpace(text));
            _steps = StudyJourney.Evaluate(new ProjectFacts(described, imports, studies.Count, accepted));
            InspectorFacts.Text = $"{_project.Name}\nBoucle : {(string.IsNullOrWhiteSpace(brief.Tag) ? "non renseignée" : brief.Tag)}\nEnregistrements : {imports}\nÉtudes : {studies.Count}\nModèles acceptés : {accepted}";
            var rows = studies.OrderByDescending(study => study.CalculatedAt).Select(Row).ToList();
            StudyGrid.ItemsSource = rows;
            BuildTabs();
            if (rows.Count > 0) StudyGrid.SelectedIndex = 0; else ShowStudy(null);
        }
        catch (Exception error)
        {
            Trace.TraceError("project.facts {0}", error.GetType().Name);
            _steps = StudyJourney.Evaluate(null);
            InspectorFacts.Text = "Impossible de lire l'état du projet. Vérifiez le stockage local.";
            BuildTabs();
        }
    }

    private static StudyRow Row(IdentificationStudy study)
    {
        var result = study.Result;
        string F(double v) => v.ToString("G4", CultureInfo.CurrentCulture);
        return new StudyRow
        {
            Study = study,
            Gain = $"{F(result.Gain)} {study.PvUnit}/{study.MvUnit}",
            Tau = result.TauSeconds is { } tau ? (result.SecondTauSeconds is { } fast ? $"{F(tau)} / {F(fast)}" : F(tau)) : "—",
            Delay = F(result.DelaySeconds),
            Error = $"{F(result.Validation.Rmse)} {study.PvUnit}",
            Decision = study.IsAcceptedForTuning ? "Accepté" : "Refusé",
            When = study.CalculatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        };
    }

    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        _tabs.Clear();
        foreach (var step in _steps)
        {
            var index = step.Number - 1;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            if (step.State != StepState.Ready)
                content.Children.Add(new TextBlock { Text = step.State == StepState.Done ? "\uE73E" : "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            content.Children.Add(new TextBlock { Text = ShortTitles[index], VerticalAlignment = VerticalAlignment.Center });
            var tab = new RadioButton { Style = (Style)FindResource("MkrRibbonTab"), GroupName = "Steps", IsChecked = index == _tab, Content = content };
            System.Windows.Automation.AutomationProperties.SetName(tab, $"Étape {step.Number} {step.Title}, {StateName(step.State)}");
            System.Windows.Automation.AutomationProperties.SetAutomationId(tab, "Step" + step.Number);
            tab.ToolTip = step.Detail;
            tab.Checked += (_, _) => { _tab = index; BuildRibbon(); };
            _tabs.Add(tab);
            TabStrip.Children.Add(tab);
        }
        BuildRibbon();
    }

    private Button Command(string glyph, string label, string tooltip, Action action, bool enabled = true)
    {
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = label, FontSize = 11, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0), MaxWidth = 86 });
        var button = new Button { Style = (Style)FindResource("MkrRibbonButton"), Content = content, ToolTip = tooltip, IsEnabled = enabled };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private void BuildRibbon()
    {
        RibbonCommands.Children.Clear();
        if (_steps.Count == 0) return;
        var step = _steps[_tab];
        var open = step.State != StepState.Blocked;
        const string inIdentification = "S'ouvre dans la fenêtre Identification et réglage. ";
        var selected = (StudyGrid.SelectedItem as StudyRow)?.Study;
        switch (_tab)
        {
            case 0:
                RibbonCommands.Children.Add(Command("", "Accueil", "Retourner à la liste des projets", LeaveStudy));
                RibbonCommands.Children.Add(Command("", "Description de la boucle", "Repère, grandeur réglée, point de fonctionnement, objectif", () => OpenProject(this, new RoutedEventArgs())));
                RibbonCommands.Children.Add(Command("", "Journal d'audit", "Journal chaîné des actions du projet", OpenAudit));
                break;
            case 1:
                RibbonCommands.Children.Add(Command("", "Importer un CSV", "Importer un fichier ou acquérir en lecture seule depuis l'automate", () => OpenProject(this, new RoutedEventArgs())));
                RibbonCommands.Children.Add(Command("", "Acquisition automate", "Lecture seule Modbus TCP / S7comm", () => OpenProject(this, new RoutedEventArgs())));
                break;
            case 2:
                RibbonCommands.Children.Add(Command("", "Rechercher des essais", inIdentification, OpenIdentification, open));
                RibbonCommands.Children.Add(Command("", "Courbes brutes", inIdentification, OpenIdentification, open));
                break;
            case 3:
                RibbonCommands.Children.Add(Command("", "Identifier le procédé", inIdentification, OpenIdentification, open));
                break;
            case 4:
                RibbonCommands.Children.Add(Command("", "Revue de l'étude", "Statut de revue (calculé, vérifié, approuvé) avec quatre yeux", OpenReview, selected is not null));
                RibbonCommands.Children.Add(Command("", "Contrôle de dérive", inIdentification, OpenIdentification, open));
                break;
            case 5:
                RibbonCommands.Children.Add(Command("", "Choisir IMC ou SIMC", inIdentification, OpenIdentification, open));
                break;
            case 6:
                RibbonCommands.Children.Add(Command("", "Catalogue des blocs constructeurs", "Schneider et Siemens, compatibilités et sources", () => OpenCatalogue(this, new RoutedEventArgs())));
                RibbonCommands.Children.Add(Command("", "Valeurs du bloc", inIdentification, OpenIdentification, open));
                break;
            case 7:
                RibbonCommands.Children.Add(Command("", "Calculer les réglages", inIdentification, OpenIdentification, open));
                break;
            case 8:
                RibbonCommands.Children.Add(Command("", "Simuler", inIdentification, OpenIdentification, open));
                break;
            case 9:
                RibbonCommands.Children.Add(Command("", "Fiche d'étude PDF", inIdentification, OpenIdentification, open));
                break;
        }
        // chart commands are always at hand, whichever step is shown
        RibbonCommands.Children.Add(new Border { Width = 1, Margin = new Thickness(8, 6, 8, 6), Background = (Brush)FindResource("MkrBorder") });
        RibbonCommands.Children.Add(Command("\uE71E", "Vue complète", "Revenir à l'horizon complet du graphique (double-clic ou Origine)", () => Chart.ResetView()));
        RibbonCommands.Children.Add(Command("\uEB9F", "Exporter l'image", "Enregistrer le graphique en PNG", () => ExportImage(this, new RoutedEventArgs())));
        RibbonCommands.Children.Add(Command("\uE74E", "Exporter les données", "Enregistrer toutes les courbes en CSV avec leurs unités", () => ExportData(this, new RoutedEventArgs())));
        RibbonNote.Text = open ? step.Detail : $"Étape fermée. {step.Detail}";
    }

    private void OpenAudit()
    {
        if (_csv is null) return;
        try { new AuditWindow(_csv) { Owner = this }.ShowDialog(); }
        catch (Exception error) { Trace.TraceError("audit.open {0}", error.GetType().Name); }
    }

    private void OpenIdentification()
    {
        if (_workspace is null) return;
        try { new IdentificationWindow(_workspace) { Owner = this }.ShowDialog(); }
        catch (Exception error)
        {
            Trace.TraceError("identification.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir l’identification. Vérifiez le stockage local.", "MKR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        LoadStudy();
    }

    private void OpenReview()
    {
        if (_workspace is null || (StudyGrid.SelectedItem as StudyRow)?.Study is not { } study) return;
        try { new ReviewWindow(_workspace, study.Id, study.Label) { Owner = this }.ShowDialog(); }
        catch (Exception error) { Trace.TraceError("review.open {0}", error.GetType().Name); }
    }

    // ---------- values panel and chart ----------
    private void StudySelected(object sender, SelectionChangedEventArgs args) => ShowStudy((StudyGrid.SelectedItem as StudyRow)?.Study);
    private void TrialChanged(object sender, RoutedEventArgs args) { if (IsLoaded) ShowStudy((StudyGrid.SelectedItem as StudyRow)?.Study); }

    private void UpdateVerdictColour()
    {
        var study = (StudyGrid.SelectedItem as StudyRow)?.Study;
        Verdict.Background = study is null ? (Brush)FindResource("MkrBorderStrong") : (Brush)FindResource(study.IsAcceptedForTuning ? "MkrSuccess" : "MkrDanger");
    }

    private void ShowStudy(IdentificationStudy? study)
    {
        if (study is null)
        {
            VerdictText.Text = "AUCUNE ÉTUDE";
            ValuesText.Text = "";
            ValuesNote.Text = _steps.Count > 3 && _steps[3].State == StepState.Blocked ? "Importez des données (onglet Données), puis identifiez le procédé." : "Aucune identification enregistrée. Ouvrez l'onglet Identification.";
            Chart.SetTracks(Array.Empty<ChartTrack>());
            UpdateVerdictColour();
            BuildRibbon();
            return;
        }
        var result = study.Result;
        VerdictText.Text = study.IsAcceptedForTuning ? "MODÈLE ACCEPTÉ" : "MODÈLE REFUSÉ";
        UpdateVerdictColour();
        string F(double v) => v.ToString("G5", CultureInfo.CurrentCulture);
        var lines = new List<string> { $"Famille  {study.Family}", $"K  {F(result.Gain)} {study.PvUnit}/{study.MvUnit}" };
        if (result.TauSeconds is { } tau) lines.Add(result.SecondTauSeconds is { } fast ? $"τ₁  {F(tau)} s\nτ₂  {F(fast)} s" : $"τ  {F(tau)} s");
        lines.Add($"θ  {F(result.DelaySeconds)} s");
        lines.Add($"θ prudent  {F(result.TuningDelaySeconds)} s");
        lines.Add($"RMSE valid.  {F(result.Validation.Rmse)} {study.PvUnit}");
        if (result.Validation.RSquared is { } r2) lines.Add($"R² valid.  {F(r2)}");
        ValuesText.Text = string.Join("\n", lines);
        ValuesNote.Text = study.IsAcceptedForTuning
            ? "Validé sur un essai indépendant. Les réglages restent à qualifier sur banc."
            : $"Refusé : {(study.Assessment is { Reasons.Length: > 0 } a ? string.Join(", ", a.Reasons) : "critères non satisfaits")}. Aucun réglage n'en est déduit.";

        var fit = ShowValidation.IsChecked == true ? result.Validation : result.Training;
        var seconds = fit.Points.Select(p => p.Seconds).ToArray();
        var step = fit.InputStep;
        var horizon = seconds.Length > 0 ? seconds[^1] : 1;
        Chart.XLabel = "Temps depuis l'échelon de la sortie de commande (s)";
        Chart.SetTracks(new[]
        {
            new ChartTrack("Mesure", study.PvUnit, new[]
            {
                new ChartSeries("Observée", "MkrSignal1", seconds, fit.Points.Select(p => p.Observed).ToArray()),
                new ChartSeries("Modèle", "MkrSignal2", seconds, fit.Points.Select(p => p.Predicted).ToArray())
            }),
            new ChartTrack("Sortie de commande (écart)", study.MvUnit, new[]
            {
                new ChartSeries("Échelon appliqué", "MkrSignal3", new[] { -horizon * 0.05, 0, 0, horizon }, new[] { 0, 0, step, step })
            })
        });
        BuildRibbon();
    }

    private void ResetZoom(object sender, RoutedEventArgs args) => Chart.ResetView();

    private void ExportImage(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Image PNG (*.png)|*.png", FileName = "courbes-etude.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportPng(dialog.FileName); CursorText.Text = "Image exportée."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { CursorText.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
    }

    private void ExportData(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "courbes-etude.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportCsv(dialog.FileName); CursorText.Text = "Données exportées."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { CursorText.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
    }
}
