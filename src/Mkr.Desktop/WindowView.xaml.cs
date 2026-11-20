using System.Diagnostics;
using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class WindowView : Window
{
    private readonly IImportStore _store;
    private readonly ICsvEngine _engine;

    private readonly IFopdtStore _studies;
    private readonly IFopdtEngine _fopdt;

    public WindowView(IImportStore store, ICsvEngine engine, IFopdtStore studies, IFopdtEngine fopdt)
    {
        _store = store;
        _engine = engine;
        _studies = studies;
        _fopdt = fopdt;
        InitializeComponent();
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
            new CsvWindow(new CsvWorkspace(project, _store, _engine), new FopdtWorkspace(project, _store, _studies, _fopdt)) { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            Trace.TraceError("project.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir le projet. Vérifiez le stockage local.", "MKR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
