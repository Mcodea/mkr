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
    }

    private void OpenCatalogue(object sender, RoutedEventArgs args)
    {
        try
        {
            new BlockCatalogueWindow { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            Trace.TraceError("catalogue.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible de lire le catalogue installé.", "MKR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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
