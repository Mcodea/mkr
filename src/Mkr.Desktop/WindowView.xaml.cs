using System.Diagnostics;
using System.Windows.Controls;
using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class WindowView : Window
{
    private readonly IImportStore _store;
    private readonly ICsvEngine _engine;

    private readonly Func<Project, IdentificationWorkspace> _createIdentification;

    internal WindowView(IImportStore store, ICsvEngine engine, Func<Project, IdentificationWorkspace> createIdentification)
    {
        _store = store;
        _engine = engine;
        _createIdentification = createIdentification;
        InitializeComponent();
        ThemeChoice.SelectedIndex = (int)ThemeService.Choice;       // Initialize() ran at startup: reflect the active choice
        var version = typeof(WindowView).Assembly.GetName().Version;
        RefreshFacts();
        VersionText.Text = version is null ? "MKR · application Windows" : $"MKR {version.Major}.{version.Minor}.{version.Build} · application Windows";
    }

    private readonly string[] _navLabels = { "NavHomeLabel", "NavCatalogueLabel" };

    private void ToggleNavigation(object sender, RoutedEventArgs args)
    {
        var collapsed = NavColumn.Width.Value < 100;
        NavColumn.Width = new GridLength(collapsed ? 250 : 56);
        var visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        foreach (var name in _navLabels) if (FindName(name) is UIElement label) label.Visibility = visibility;
        SpaceHeading.Visibility = visibility;
        StudyHeading.Visibility = visibility;
        foreach (var label in _stepLabels) label.Visibility = visibility;
    }

    private readonly List<TextBlock> _stepLabels = new();
    private IReadOnlyList<JourneyStep> _steps = StudyJourney.Evaluate(null);
    private int _selectedStep;

    private void ToggleInspector(object sender, RoutedEventArgs args) => SetInspector(Inspector.Visibility != Visibility.Visible);

    private void SetInspector(bool show)
    {
        Inspector.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        InspectorColumn.Width = new GridLength(show ? 300 : 0);
        // On a 1024 px screen the navigation folds to icons so the workspace keeps its room.
        if (show && ActualWidth < 1200 && NavColumn.Width.Value > 100) ToggleNavigation(this, new RoutedEventArgs());
    }

    private static string StateGlyph(StepState state) => state switch { StepState.Done => "\uE73E", StepState.Ready => "\uE76C", _ => "\uE72E" };
    private static string StateName(StepState state) => state switch { StepState.Done => "terminée", StepState.Ready => "disponible", _ => "fermée" };

    private void RebuildSteps()
    {
        StepList.Children.Clear();
        _stepLabels.Clear();
        var collapsed = NavColumn.Width.Value < 100;
        foreach (var step in _steps)
        {
            var number = step.Number;
            var button = new Button { Style = (Style)FindResource("MkrNavButton"), Padding = new Thickness(12, 6, 8, 6), ToolTip = step.Detail };
            System.Windows.Automation.AutomationProperties.SetName(button, $"Étape {number} {step.Title}, {StateName(step.State)}");
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, "Step" + number);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = StateGlyph(step.State), FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14, Width = 22, VerticalAlignment = VerticalAlignment.Center, Opacity = step.State == StepState.Blocked ? 0.75 : 1 });
            row.Children.Add(new TextBlock { Text = number.ToString(), Width = 22, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            var label = new TextBlock { Text = step.Title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150, Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible };
            _stepLabels.Add(label);
            row.Children.Add(label);
            button.Content = row;
            button.Click += (_, _) => { _selectedStep = number - 1; ShowStep(); SetInspector(true); };
            StepList.Children.Add(button);
        }
    }

    private void ShowStep()
    {
        var step = _steps[_selectedStep];
        InspectorStepTitle.Text = $"Étape {step.Number} · {step.Title}";
        InspectorStepState.Text = step.State switch { StepState.Done => "✔ Terminée", StepState.Ready => "▶ Disponible", _ => "⛔ Fermée" };
        InspectorStepDetail.Text = step.Detail;
        InspectorOpen.IsEnabled = step.State != StepState.Blocked;
    }

    private void OpenSelectedStep(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project || _steps[_selectedStep].State == StepState.Blocked) return;
        if (_selectedStep < 3) OpenProject(sender, args); else OpenIdentification(sender, args);
        RefreshFacts();
    }

    private void RefreshFacts()
    {
        if (ProjectList.SelectedItem is not Project project)
        {
            _steps = StudyJourney.Evaluate(null);
            InspectorProject.Text = "Aucun projet";
            InspectorFacts.Text = "Sélectionnez un projet dans la liste.";
        }
        else
        {
            try
            {
                var brief = new CsvWorkspace(project, _store, _engine).ReadLoopBrief();
                var workspace = _createIdentification(project);
                var imports = workspace.Sources().Count;
                var studies = workspace.Studies();
                var accepted = studies.Count(study => study.IsAcceptedForTuning);
                var described = new[] { brief.Tag, brief.ControlledQuantity, brief.OperatingPoint, brief.Objective, brief.Description }.Any(text => !string.IsNullOrWhiteSpace(text));
                _steps = StudyJourney.Evaluate(new ProjectFacts(described, imports, studies.Count, accepted));
                InspectorProject.Text = project.Name;
                InspectorFacts.Text = $"Boucle : {(string.IsNullOrWhiteSpace(brief.Tag) ? "non renseignée" : brief.Tag)}\nEnregistrements : {imports}\nÉtudes d'identification : {studies.Count}\nModèles acceptés : {accepted}\nCréé le {project.CreatedAt:yyyy-MM-dd} (UTC)";
            }
            catch (Exception error)
            {
                Trace.TraceError("project.facts {0}", error.GetType().Name);
                _steps = StudyJourney.Evaluate(null);
                InspectorFacts.Text = "Impossible de lire l'état du projet. Vérifiez le stockage local.";
            }
        }
        RebuildSteps();
        ShowStep();
    }

    private void ShowHome(object sender, RoutedEventArgs args) => ProjectList.Focus();

    private void ProjectSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs args)
    {
        var project = ProjectList.SelectedItem as Project;
        ActiveProject.Text = project is null ? "Aucun projet sélectionné" : $"Projet : {project.Name}";
        RefreshFacts();
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key != System.Windows.Input.Key.F1) return;
        args.Handled = true;
        OpenHelp(sender, args);
    }

    private void OpenHelp(object sender, RoutedEventArgs args) => new HelpWindow { Owner = this }.ShowDialog();

    private void OpenIdentification(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project project) return;
        try { new IdentificationWindow(_createIdentification(project)) { Owner = this }.ShowDialog(); }
        catch (Exception error)
        {
            Trace.TraceError("identification.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir l’identification. Vérifiez le stockage local.", "MKR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs args)
    {
        if (ThemeChoice.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } && Enum.TryParse(tag, out ThemeChoice choice) && choice != ThemeService.Choice)
            ThemeService.Apply(choice);
    }

    private void OpenCatalogue(object sender, RoutedEventArgs args) => BlockCatalogueWindow.OpenFor(this);

    private void OpenProject(object sender, RoutedEventArgs args)
    {
        if (sender is System.Windows.Controls.DataGrid && ProjectList.SelectedItem is null) return;
        if (ProjectList.SelectedItem is not Project project)
        {
            MessageBox.Show(this, "Sélectionnez un projet dans la liste.", "MKR");
            return;
        }
        try
        {
            new CsvWindow(new CsvWorkspace(project, _store, _engine), _createIdentification(project)) { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            Trace.TraceError("project.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir le projet. Vérifiez le stockage local.", "MKR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
