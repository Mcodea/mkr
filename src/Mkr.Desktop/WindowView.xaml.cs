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
    }

    private void ThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs args)
    {
        if (ThemeChoice.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } && Enum.TryParse(tag, out ThemeChoice choice) && choice != ThemeService.Choice)
            ThemeService.Apply(choice);
    }

    private void OpenCatalogue(object sender, RoutedEventArgs args) => BlockCatalogueWindow.OpenFor(this);

    private void OpenProject(object sender, RoutedEventArgs args)
    {
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
