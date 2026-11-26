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
    public string Label => $"{Family} · {When} · {Decision}";
}

public partial class WindowView : Window
{
    private static readonly string[] ShortTitles =
        { "Projet", "Données", "Diagnostic", "Identification", "Validation", "Réglage PID", "Bloc", "Paramètres PI", "Simulation", "Rapport" };

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
        ReflectTheme();       // Initialize() ran at startup: reflect the active choice in the menu
        BuildToolbar();
        var version = typeof(WindowView).Assembly.GetName().Version;
        VersionText.Text = version is null ? "MKR · application Windows" : $"MKR {version.Major}.{version.Minor}.{version.Build} · application Windows";
        Chart.CursorTextChanged += text => CursorText.Text = text.Length == 0 ? "Survolez le graphique pour lire les valeurs au curseur." : text;
        TitleBar.SizeChanged += (_, e) => LayoutTitle(e.NewSize.Width);
        StateChanged += (_, _) => ReflectWindowState();
        Loaded += (_, _) => ReflectWindowState();
        SizeChanged += (_, e) => ToolbarBand.Visibility = e.NewSize.Height < 820 ? Visibility.Collapsed : Visibility.Visible;     // low screens: the toolbar only repeats the navigation
        ThemeService.Changed += () => { if (_workspace is not null && StudyView.Visibility == Visibility.Visible) UpdateVerdictColour(); };
    }

    /// <summary>Narrow windows put the stage chevrons on their own line under the title instead of clipping their labels.</summary>
    private void LayoutTitle(double width)
    {
        var narrow = width < 1000;
        Grid.SetRow(Stages, narrow ? 1 : 0);
        Grid.SetColumn(Stages, narrow ? 0 : 1);
        Grid.SetColumnSpan(Stages, narrow ? 2 : 1);
        Stages.Margin = new Thickness(0, narrow ? 8 : 0, 0, 0);
    }

    private void CaptionMinimize(object sender, RoutedEventArgs args) => WindowState = WindowState.Minimized;
    private void CaptionMaximize(object sender, RoutedEventArgs args) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CaptionClose(object sender, RoutedEventArgs args) => Close();

    /// <summary>A borderless maximised window overshoots the screen by the resize border: pull the content back inside, and show the matching glyph.</summary>
    private void ReflectWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        RootFrame.Margin = new Thickness(0);          // the maximised rectangle is answered by the work area (WindowView.Chrome.cs): no overshoot to compensate
        MaximizeGlyph.Text = maximized ? "\uE923" : "\uE922";
    }

    private string _defaultFacts = "";

    /// <summary>The status bar carries the facts of the screen shown (identification: trial, points, window, fit), the project counts elsewhere.</summary>
    private void ApplyFacts()
    {
        StatusFacts.Text = _tab == 3 && _identification is { Facts.Length: > 0 } view ? view.Facts : _defaultFacts;
    }

    private bool _assisted = true;

    private void ReflectTheme()
    {
        MenuThemeSystem.IsChecked = ThemeService.Choice == ThemeChoice.System;
        MenuThemeLight.IsChecked = ThemeService.Choice == ThemeChoice.Light;
        MenuThemeDark.IsChecked = ThemeService.Choice == ThemeChoice.Dark;
    }

    private void MenuTheme(object sender, RoutedEventArgs args)
    {
        if (sender is MenuItem { Tag: string tag } && Enum.TryParse(tag, out ThemeChoice choice)) ThemeService.Apply(choice);
        ReflectTheme();
    }

    private void MenuMode(object sender, RoutedEventArgs args)
    {
        _assisted = ReferenceEquals(sender, MenuAssisted);
        MenuAssisted.IsChecked = _assisted;
        MenuExpert.IsChecked = !_assisted;
        UpdateNext();
    }

    private void MenuNewProject(object sender, RoutedEventArgs args)
    {
        if (StudyView.Visibility == Visibility.Visible) LeaveStudy();
        ProjectName.Focus();
    }

    private void MenuHome(object sender, RoutedEventArgs args) { if (StudyView.Visibility == Visibility.Visible) LeaveStudy(); }
    private void MenuQuit(object sender, RoutedEventArgs args) => Close();
    private void MenuDescription(object sender, RoutedEventArgs args) { if (StudyView.Visibility == Visibility.Visible) GoToDescription(); }
    private void MenuAudit(object sender, RoutedEventArgs args) { if (StudyView.Visibility == Visibility.Visible) OpenAudit(); }
    private void MenuReview(object sender, RoutedEventArgs args) { if (StudyView.Visibility == Visibility.Visible) OpenReview(); }

    /// <summary>Quick access to the main stages, like a desktop toolbar; disabled until a project is open.</summary>
    private readonly List<Button> _toolbarButtons = new();

    private void BuildToolbar()
    {
        void Add(string glyph, string label, string tooltip, Action action, bool needsStudy)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Style = (Style)FindResource("MkrBarButton"), Content = content, ToolTip = tooltip, Padding = new Thickness(10, 5, 10, 5), Foreground = (Brush)FindResource("MkrText") };
            button.SetResourceReference(Control.ForegroundProperty, "MkrText");
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => action();
            if (needsStudy) _toolbarButtons.Add(button);
            Toolbar.Children.Add(button);
        }
        void Separator() => Toolbar.Children.Add(new Border { Width = 1, Margin = new Thickness(6, 4, 6, 4), Background = (Brush)FindResource("MkrBorder") });
        Add("\uE80F", "Accueil", "Liste des projets", () => { if (StudyView.Visibility == Visibility.Visible) LeaveStudy(); }, false);
        Add("\uE81C", "Journal d'audit", "Journal chaîné des actions du projet", () => { if (StudyView.Visibility == Visibility.Visible) OpenAudit(); }, true);
        Separator();
        Add("\uE768", "Acquisition", "Données du procédé : import CSV, acquisition en lecture seule", () => SelectTab(1), true);
        Add("\uE9D9", "Identifier", "Identification du modèle et validation sur un essai indépendant", () => SelectTab(3), true);
        Add("\uE9F5", "Régler PID", "Réglage guidé : objectif de robustesse, comparaison, courbes", () => SelectTab(5), true);
        Add("\uE916", "Simuler", "Simulation en boucle fermée du réglage", () => SelectTab(8), true);
        Add("\uE749", "Rapport", "Fiche d'étude", () => SelectTab(9), true);
        foreach (var button in _toolbarButtons) button.IsEnabled = false;
    }

    private void SetToolbarEnabled(bool enabled) { foreach (var button in _toolbarButtons) button.IsEnabled = enabled; }

    /// <summary>The next step the project can do, shown as the status-bar button of a desktop wizard (assisted mode only).</summary>
    private void UpdateNext()
    {
        var next = _assisted && StudyView.Visibility == Visibility.Visible ? StudyJourney.NextStep(_steps) : null;
        NextButton.Visibility = next is null ? Visibility.Collapsed : Visibility.Visible;
        if (next is null) return;
        NextButton.Content = $"Suivant : {ShortTitles[next.Number - 1]}  \u203A";
        NextButton.ToolTip = $"{next.Title}. {next.Detail}";
        NextButton.IsEnabled = _tab != next.Number - 1;
    }

    private void GoToNext(object sender, RoutedEventArgs args)
    {
        if (StudyJourney.NextStep(_steps) is { } next) SelectTab(next.Number - 1);
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
        ActiveProject.Text = _project is null ? "aucun projet sélectionné" : _project.Name;
        OpenStudy.IsEnabled = _project is not null;
    }

    /// <summary>« Ouvrir le projet sélectionné » (and double-click): one window, so the project opens in the workspace on its data tab.</summary>
    private void OpenProject(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project)
        {
            if (sender is not DataGrid) MessageBox.Show(this, "Sélectionnez un projet dans la liste.", "MKR");
            return;
        }
        EnterStudy(sender, args);
        SelectTab(1);
    }

    // ---------- study workspace ----------
    private void EnterStudy(object sender, RoutedEventArgs args)
    {
        if (ProjectList.SelectedItem is not Project project) return;
        _project = project;
        HomeView.Visibility = Visibility.Collapsed;
        StudyView.Visibility = Visibility.Visible;
        SetToolbarEnabled(true);
        Title = $"MKR — {project.Name}";
        LoadStudy();
    }

    private void LeaveStudy()
    {
        ReleaseStepHost();
        StudyView.Visibility = Visibility.Collapsed;
        SetToolbarEnabled(false);
        NextButton.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        Title = "MKR — Espace projets";
    }

    private static string StateGlyph(StepState state) => state switch { StepState.Done => "", StepState.Ready => "", _ => "" };
    private static string StateName(StepState state) => state switch { StepState.Done => "terminée", StepState.Ready => "disponible", _ => "fermée" };

    private void LoadStudy(bool keepIdentification = false)
    {
        if (_project is null) return;
        try
        {
            ReleaseStepHost(keepIdentification);
            _csv = new CsvWorkspace(_project, _store, _engine);
            _workspace = _createIdentification(_project);
            var brief = _csv.ReadLoopBrief();
            var imports = _workspace.Sources().Count;
            var studies = _workspace.Studies();
            var accepted = studies.Count(study => study.IsAcceptedForTuning);
            var described = new[] { brief.Tag, brief.ControlledQuantity, brief.OperatingPoint, brief.Objective, brief.Description }.Any(text => !string.IsNullOrWhiteSpace(text));
            _steps = StudyJourney.Evaluate(new ProjectFacts(described, imports, studies.Count, accepted));
            _defaultFacts = $"Enregistrements : {imports}   ·   Études : {studies.Count}   ·   Modèles acceptés : {accepted}";
            StatusFacts.Text = _defaultFacts;
            InspectorFacts.Text = $"{_project.Name}\nBoucle : {(string.IsNullOrWhiteSpace(brief.Tag) ? "non renseignée" : brief.Tag)}\nEnregistrements : {imports}\nÉtudes : {studies.Count}\nModèles acceptés : {accepted}" +
                (StudyJourney.NextStep(_steps) is { } next ? $"\n\nProchaine étape : {next.Number}. {next.Title}\n{next.Detail}" : "");
            var rows = studies.OrderByDescending(study => study.CalculatedAt).Select(Row).ToList();
            StudyGrid.ItemsSource = rows;
            StudyPicker.ItemsSource = rows;
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

    private static readonly string[] NavGlyphs =
        { "\uE80F", "\uE8F1", "\uE9D9", "\uE9D2", "\uE73E", "\uE9F5", "\uE8C8", "\uE8EF", "\uE768", "\uE749" };

    /// <summary>Vertical navigation: one item per step of the journey, with its state (done, ready, closed).</summary>
    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        _tabs.Clear();
        foreach (var step in _steps)
        {
            var index = step.Number - 1;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = NavGlyphs[index], FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Width = 20 });
            content.Children.Add(new TextBlock { Text = ShortTitles[index], VerticalAlignment = VerticalAlignment.Center });
            if (step.State != StepState.Ready)
                content.Children.Add(new TextBlock { Text = step.State == StepState.Done ? "\uE73E" : "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Opacity = 0.75 });
            var tab = new RadioButton { Style = (Style)FindResource("MkrNavItem"), GroupName = "Steps", IsChecked = index == _tab, Content = content };
            System.Windows.Automation.AutomationProperties.SetName(tab, $"Étape {step.Number} {step.Title}, {StateName(step.State)}");
            System.Windows.Automation.AutomationProperties.SetAutomationId(tab, "Step" + step.Number);
            tab.ToolTip = step.Detail;
            tab.Checked += (_, _) => { _tab = index; BuildRibbon(); };
            _tabs.Add(tab);
            TabStrip.Children.Add(tab);
        }
        BuildStages();
        BuildRibbon();
    }

    /// <summary>The seven stages of a regulation study as chevrons, like the reference screens: done, current, ahead.</summary>
    private static readonly (string Title, int[] Steps, int Tab)[] StageMap =
    {
        ("Données", new[] { 0, 1 }, 1), ("Diagnostic", new[] { 2 }, 2), ("Identification", new[] { 3 }, 3), ("Validation", new[] { 4 }, 4),
        ("Réglage", new[] { 5, 6, 7 }, 5), ("Simulation", new[] { 8 }, 8), ("Rapport", new[] { 9 }, 9)
    };

    private void BuildStages()
    {
        Stages.Children.Clear();
        Stages.ColumnDefinitions.Clear();
        for (var i = 0; i < StageMap.Length; i++)
        {
            var (title, steps, tab) = StageMap[i];
            Stages.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var current = steps.Contains(_tab);
            var done = _steps.Count > 0 && steps.All(s => _steps[s].State == StepState.Done);
            var closed = _steps.Count > 0 && _steps[steps[0]].State == StepState.Blocked;
            var fill = current ? "MkrNavy" : done ? "MkrSurfaceAlt" : "MkrSurface";
            var chevron = new System.Windows.Shapes.Polygon
            {
                Points = new PointCollection { new Point(0, 0), new Point(88, 0), new Point(100, 50), new Point(88, 100), new Point(0, 100), new Point(12, 50) },
                Stretch = Stretch.Fill, StrokeThickness = 1, Margin = new Thickness(i == 0 ? 0 : -6, 0, 0, 0)
            };
            chevron.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, fill);
            chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, current ? "MkrNavy" : "MkrBorder");
            var label = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            if (done || closed)
                label.Children.Add(new TextBlock { Text = done ? "\uE73E" : "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                                                   Foreground = (Brush)FindResource(current ? "MkrNavyText" : done ? "MkrSuccess" : "MkrMuted") });
            var text = new TextBlock { Text = title, FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty, current ? "MkrNavyText" : closed ? "MkrMuted" : "MkrText");
            label.Children.Add(text);
            var cell = new Grid { Cursor = System.Windows.Input.Cursors.Hand, Background = Brushes.Transparent };
            cell.Children.Add(chevron);
            cell.Children.Add(label);
            var target = tab;
            cell.MouseLeftButtonUp += (_, _) => SelectTab(target);
            System.Windows.Automation.AutomationProperties.SetName(cell, $"Étape {title}");
            System.Windows.Automation.AutomationProperties.SetAutomationId(cell, "Stage" + (i + 1));
            cell.ToolTip = _steps.Count > 0 ? _steps[steps[0]].Detail : title;
            Grid.SetColumn(cell, i);
            Stages.Children.Add(cell);
        }
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
        ScreenTitle.Text = _tab == 5 ? "Réglage PID" : _tab == 2 ? "Diagnostic des données" : step.Title;
        BuildStages();
        var open = step.State != StepState.Blocked;
        var selected = (StudyGrid.SelectedItem as StudyRow)?.Study;
        switch (_tab)
        {
            case 0:
                RibbonCommands.Children.Add(Command("", "Accueil", "Retourner à la liste des projets", LeaveStudy));
                RibbonCommands.Children.Add(Command("", "Description de la boucle", "Repère, grandeur réglée, point de fonctionnement, objectif", () => GoToDescription()));
                RibbonCommands.Children.Add(Command("", "Journal d'audit", "Journal chaîné des actions du projet", OpenAudit));
                break;
            case 1:
                break;                      // shown in the workspace itself: the data and process view (UpdateStepHost)
            case 2:
            case 3:
                break;                      // shown in the workspace itself: the identification view (UpdateStepHost)
            case 4:
                RibbonCommands.Children.Add(Command("", "Revue de l'étude", "Statut de revue (calculé, vérifié, approuvé) avec quatre yeux", OpenReview, selected is not null));
                break;
            case 5:
                break;                      // the guided tuning is shown in the workspace itself (UpdateStepHost)
            case 6:
                RibbonCommands.Children.Add(Command("\uE8F1", "Catalogue des blocs constructeurs", "Schneider et Siemens, compatibilités et sources", () => OpenCatalogue(this, new RoutedEventArgs())));
                break;
            case 7:
                if (selected is { Family: ModelFamily.Integrateur })
                    RibbonCommands.Children.Add(Command("\uE9D2", "Réglage intégrateur", "Règle SIMC des procédés intégrateurs", () => OpenWindow(() => new IntegratorWindow(_workspace!, selected)), open));
                break;
            case 8:
                RibbonCommands.Children.Add(Command("\uE916", _openLoop ? "Boucle fermée" : "Boucle ouverte", _openLoop ? "Revenir à la comparaison PI en boucle fermée" : "Réponse du modèle à un échelon, sans régulateur", () => { _openLoop = !_openLoop; BuildRibbon(); }, open && selected is not null));
                break;
            case 9:
                RibbonCommands.Children.Add(Command("\uE749", "Fiche d'étude PDF", "Depuis la fenêtre de simulation : « Fiche de mise au point (PDF) »", () => OpenWithTuning(tuning => new SimulationWindow(_workspace!, selected!, tuning)), open && selected is not null));
                break;
        }
        UpdateStepHost(selected, open);
        StudyTablePanel.Visibility = _tab == 4 ? Visibility.Visible : Visibility.Collapsed;     // the study table only serves the validation view
        // chart commands are at hand whenever the chart (not a hosted step view) is shown
        if (StepHost.Visibility != Visibility.Visible)
        {
            RibbonCommands.Children.Add(new Border { Width = 1, Margin = new Thickness(8, 6, 8, 6), Background = (Brush)FindResource("MkrBorder") });
            RibbonCommands.Children.Add(Command("\uE740", _focusChart ? "Réduire" : "Agrandir", "Donner tout l'espace au graphique (masque le panneau de valeurs et le tableau)", () => { ToggleChartFocus(); BuildRibbon(); }));
            RibbonCommands.Children.Add(Command("\uE71E", "Vue complète", "Revenir à l'horizon complet du graphique (double-clic ou Origine)", () => Chart.ResetView()));
            RibbonCommands.Children.Add(Command("\uEB9F", "Exporter l'image", "Enregistrer le graphique en PNG", () => ExportImage(this, new RoutedEventArgs())));
            RibbonCommands.Children.Add(Command("\uE74E", "Exporter les données", "Enregistrer toutes les courbes en CSV avec leurs unités", () => ExportData(this, new RoutedEventArgs())));
        }
        RibbonNote.Text = open ? step.Detail : $"Étape fermée. {step.Detail}";
        RibbonHost.Visibility = RibbonCommands.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;     // no empty command band
        ApplyFacts();
        UpdateNext();
    }

    // ---------- step views hosted in the workspace ----------
    private string? _hostKey;
    private IStepView? _hosted;
    private UIElement? _hostChild;           // what the guided tuning / simulation branch put into the host (its own view or a scroller around it)

    /// <summary>Releases the hosted view (cancels its pending calculations) and forgets it so that the next evaluation rebuilds it.</summary>
    private void ReleaseStepHost(bool keepIdentification = false)
    {
        _hosted?.Cancel();
        _hosted = null;
        _hostChild = null;
        _hostKey = null;
        _openLoop = false;
        if (!keepIdentification && _identification is not null) { _identification.Cancel(); _identification = null; }
        _diagnostic = null;
        if (!keepIdentification && _data is not null) { _data.Cancel(); _data = null; _dataHost = null; }
        StepHost.Child = null;
        StepHost.Visibility = Visibility.Collapsed;
    }

    private static TextBlock Placeholder(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24, 20, 24, 0), FontSize = 15
    };

    /// <summary>A view that needs more room than the workspace offers scrolls as a whole instead of squeezing its result areas.</summary>
    private static ScrollViewer Scrolling(UserControl view, double minimumHeight)
    {
        view.MinHeight = minimumHeight;
        return new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private IdentificationView? _identification;
    private CsvView? _data;
    private DiagnosticView? _diagnostic;
    private bool _openLoop;                   // simulation tab: the open-loop response instead of the closed-loop PI comparison
    private UIElement? _dataHost;

    private static bool IsIdentificationTab(int tab) => tab is 3 or 7;

    /// <summary>
    /// Shows the step view belonging to the selected tab inside the workspace: the identification view (signals, identification, validation and PI tunings,
    /// one instance shared by those tabs so that the user's inputs survive), the guided tuning (strategy) and the closed-loop simulation. Guided tuning and
    /// simulation are rebuilt only when the tab, the study or the tuning changes. A closed step says why instead.
    /// </summary>
    private void UpdateStepHost(IdentificationStudy? selected, bool open)
    {
        if (_workspace is null || !(IsIdentificationTab(_tab) || _tab is 1 or 2 or 5 or 6 or 8))
        {
            if (StepHost.Visibility == Visibility.Visible) { StepHost.Child = null; StepHost.Visibility = Visibility.Collapsed; }
            _hosted?.Cancel();
            _hosted = null;
            _hostKey = null;
            return;
        }
        if (_tab == 1)
        {
            _hosted?.Cancel();
            _hosted = null;
            _hostKey = "data";
            StepHost.Visibility = Visibility.Visible;
            try
            {
                if (_data is null)
                {
                    _data = new CsvView(_csv!);
                    _data.IdentificationRequested += (_, _) => SelectTab(3);
                    _data.DataChanged += (_, _) => RefreshAfterChange();
                }
                if (_dataHost is null) { _data.MinHeight = 420; _dataHost = _data; }
                if (!ReferenceEquals(StepHost.Child, _dataHost)) StepHost.Child = _dataHost;
            }
            catch (Exception error)
            {
                Trace.TraceError("step.host {0}", error.GetType().Name);
                StepHost.Child = Placeholder("Impossible d'afficher cette étape. Vérifiez le stockage local.");
            }
            return;
        }
        if (_tab == 2)
        {
            _hosted?.Cancel();
            _hosted = null;
            _hostKey = "diagnostic";
            StepHost.Visibility = Visibility.Visible;
            try
            {
                _diagnostic = new DiagnosticView(_csv!);
                StepHost.Child = _diagnostic;
            }
            catch (Exception error)
            {
                Trace.TraceError("step.host {0}", error.GetType().Name);
                StepHost.Child = Placeholder("Impossible d'afficher cette étape. Vérifiez le stockage local.");
            }
            return;
        }
        if (IsIdentificationTab(_tab))
        {
            _hosted?.Cancel();
            _hosted = null;
            _hostKey = "identification";
            StepHost.Visibility = Visibility.Visible;
            if (!open)
            {
                StepHost.Child = Placeholder(_steps[_tab].Detail);
                return;
            }
            try
            {
                if (_identification is null)
                {
                    _identification = new IdentificationView(_workspace, selected);
                    _identification.StudiesChanged += (_, _) => RefreshAfterChange();
                    _identification.FactsChanged += (_, _) => ApplyFacts();
                }
                if (!ReferenceEquals(StepHost.Child, _identification)) StepHost.Child = _identification;
            }
            catch (Exception error)
            {
                Trace.TraceError("step.host {0}", error.GetType().Name);
                StepHost.Child = Placeholder("Impossible d'afficher cette étape. Vérifiez le stockage local.");
            }
            return;
        }
        PiStudy? tuning = null;
        if (_tab is 6 or 8 && selected is not null)
            tuning = _workspace.PiStudies(selected.Id).OrderByDescending(item => item.CalculatedAt).FirstOrDefault();
        var key = $"{_tab}|{selected?.Id}|{selected?.IsAcceptedForTuning}|{tuning?.Id}|{open}|{_openLoop}";
        if (key == _hostKey && _hostChild is not null && ReferenceEquals(StepHost.Child, _hostChild)) return;
        _hosted?.Cancel();
        _hosted = null;
        _hostChild = null;
        _hostKey = key;
        StepHost.Visibility = Visibility.Visible;
        if (!open || selected is null || !selected.IsAcceptedForTuning)
        {
            StepHost.Child = Placeholder(selected is null
                ? "Aucune étude affichée. Identifiez le procédé (onglet Identification) puis faites accepter le modèle (onglet Validation)."
                : "Le modèle affiché n'a pas été accepté à la validation indépendante : aucun réglage ni simulation n'est autorisé. Choisissez une étude acceptée ou refaites l'identification.");
            return;
        }
        try
        {
            if (_tab == 5)
            {
                var view = new GuidedTuningView(_workspace, selected) { ShowClose = false };
                _hosted = view;
                { view.MinHeight = 420; StepHost.Child = _hostChild = view; }
            }
            else if (_tab == 8 && _openLoop)
            {
                if (selected.Family != ModelFamily.FOPDT)
                    StepHost.Child = Placeholder("La simulation en boucle ouverte porte sur un modèle du premier ordre avec retard (FOPDT).");
                else
                {
                    var view = new OpenLoopView(_workspace, selected);
                    _hosted = view;
                    StepHost.Child = _hostChild = view;
                }
            }
            else if (tuning is null)
                StepHost.Child = Placeholder("Aucun réglage PI enregistré pour cette étude. Calculez-en un dans l'onglet Réglage, puis revenez ici.");
            else if (_tab == 6)
            {
                var view = new BlockTargetView(_workspace, selected, tuning) { ShowClose = false };
                _hosted = view;
                StepHost.Child = _hostChild = Scrolling(view, 760);
            }
            else
            {
                var view = new SimulationView(_workspace, selected, tuning);
                _hosted = view;
                StepHost.Child = _hostChild = view;
            }
        }
        catch (Exception error)
        {
            Trace.TraceError("step.host {0}", error.GetType().Name);
            StepHost.Child = Placeholder("Impossible d'afficher cette étape. Vérifiez le stockage local.");
        }
    }

    private void SelectTab(int index)
    {
        if (index >= 0 && index < _tabs.Count) _tabs[index].IsChecked = true;
    }

    /// <summary>« Description de la boucle »: the process description lives in the data view (Procédé tab).</summary>
    private void GoToDescription()
    {
        SelectTab(1);
        _data?.ShowLoopDescription();
    }

    /// <summary>A study or a tuning was saved from a hosted view: refresh the table, the journey and the inspector without losing the view.</summary>
    private void RefreshAfterChange()
    {
        if (_project is null) return;
        LoadStudy(keepIdentification: true);
    }

    private void OpenWindow(Func<Window> create)
    {
        if (_workspace is null) return;
        try { create().ShowDialogOwned(this); }
        catch (Exception error)
        {
            Trace.TraceError("window.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir cette fenêtre. Vérifiez le stockage local.", "MKR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        LoadStudy();
    }

    /// <summary>Opens a window that needs the last saved PI tuning of the displayed study, or says what to do first.</summary>
    private void OpenWithTuning(Func<PiStudy, Window> create)
    {
        if (_workspace is null || (StudyGrid.SelectedItem as StudyRow)?.Study is not { } study) return;
        var tuning = _workspace.PiStudies(study.Id).OrderByDescending(item => item.CalculatedAt).FirstOrDefault();
        if (tuning is null)
        {
            RibbonNote.Text = "Aucun réglage PI enregistré pour cette étude. Calculez-en un à l'étape 8 (Calculer les réglages).";
            return;
        }
        OpenWindow(() => create(tuning));
    }

    private void OpenAudit()
    {
        if (_csv is null) return;
        try { new AuditWindow(_csv) { Owner = this }.ShowDialog(); }
        catch (Exception error) { Trace.TraceError("audit.open {0}", error.GetType().Name); }
    }

    private void OpenReview()
    {
        if (_workspace is null || (StudyGrid.SelectedItem as StudyRow)?.Study is not { } study) return;
        try { new ReviewWindow(_workspace, study.Id, study.Label) { Owner = this }.ShowDialog(); }
        catch (Exception error) { Trace.TraceError("review.open {0}", error.GetType().Name); }
    }

    // ---------- values panel and chart ----------
    private void StudySelected(object sender, SelectionChangedEventArgs args)
    {
        if (!ReferenceEquals(StudyPicker.SelectedItem, StudyGrid.SelectedItem)) StudyPicker.SelectedItem = StudyGrid.SelectedItem;
        ShowStudy((StudyGrid.SelectedItem as StudyRow)?.Study);
    }

    private void PickerChanged(object sender, SelectionChangedEventArgs args)
    {
        if (StudyPicker.SelectedItem is not null && !ReferenceEquals(StudyPicker.SelectedItem, StudyGrid.SelectedItem)) StudyGrid.SelectedItem = StudyPicker.SelectedItem;
    }

    private void ToggleTable(object sender, RoutedEventArgs args)
    {
        var show = StudyGrid.Visibility != Visibility.Visible;
        StudyGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TableToggle.Content = (show ? "▾ " : "▸ ") + "Tableau des études d'identification";
    }

    private bool _focusChart;

    /// <summary>Gives the whole workspace to the chart: values panel and study table are hidden until toggled back.</summary>
    private void ToggleChartFocus()
    {
        _focusChart = !_focusChart;
        ValuesColumn.Width = new GridLength(_focusChart ? 0 : 300);
        ValuesPanel.Visibility = _focusChart ? Visibility.Collapsed : Visibility.Visible;
        TableToggle.Visibility = _focusChart ? Visibility.Collapsed : Visibility.Visible;
        if (_focusChart) StudyGrid.Visibility = Visibility.Collapsed;
        CursorText.Text = _focusChart ? "Graphique agrandi. Cliquez de nouveau sur « Agrandir » pour retrouver les valeurs et le tableau." : CursorText.Text;
    }
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
            ActiveStudy.Text = "Étude : aucune";
            VerdictText.Text = "AUCUNE ÉTUDE";
            ValuesText.Text = "";
            ValuesNote.Text = _steps.Count > 3 && _steps[3].State == StepState.Blocked ? "Importez des données (onglet Données), puis identifiez le procédé." : "Aucune identification enregistrée. Ouvrez l'onglet Identification.";
            Chart.SetTracks(Array.Empty<ChartTrack>());
            UpdateVerdictColour();
            BuildRibbon();
            return;
        }
        var result = study.Result;
        ActiveStudy.Text = $"Étude : {study.Family} · {study.CalculatedAt.ToLocalTime():g} · " + (study.IsAcceptedForTuning ? "modèle accepté" : "modèle refusé");
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
            }) { Weight = 2 },
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

internal static class WindowExtensions
{
    /// <summary>Shows a window as a modal dialog of <paramref name="owner"/>.</summary>
    public static void ShowDialogOwned(this Window window, Window owner)
    {
        window.Owner = owner;
        window.ShowDialog();
    }
}
