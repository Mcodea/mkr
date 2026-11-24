using System.Diagnostics;
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
        VersionText.Text = version is null ? "MKR · application Windows" : $"MKR {version.Major}.{version.Minor}.{version.Build} · application Windows";
    }

    private readonly string[] _navLabels = { "NavHomeLabel", "NavCatalogueLabel", "NavDataLabel", "NavIdentificationLabel" };

    private void ToggleNavigation(object sender, RoutedEventArgs args)
    {
        var collapsed = NavColumn.Width.Value < 100;
        NavColumn.Width = new GridLength(collapsed ? 230 : 56);
        var visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        foreach (var name in _navLabels) if (FindName(name) is UIElement label) label.Visibility = visibility;
        SpaceHeading.Visibility = visibility;
        StudyHeading.Visibility = visibility;
    }

    private void ShowHome(object sender, RoutedEventArgs args) => ProjectList.Focus();

    private void ProjectSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs args)
    {
        var project = ProjectList.SelectedItem as Project;
        ActiveProject.Text = project is null ? "Aucun projet sélectionné" : $"Projet : {project.Name}";
        NavData.IsEnabled = NavIdentification.IsEnabled = project is not null;
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
