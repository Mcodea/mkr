using System.Diagnostics;
using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class WindowView : Window
{
    private readonly IImportStore _store;
    private readonly ICsvEngine _engine;

    private readonly IIdentificationStore _studies;
    private readonly IIdentificationEngine _identification;

    public WindowView(IImportStore store, ICsvEngine engine, IIdentificationStore studies, IIdentificationEngine identification)
    {
        _store = store;
        _engine = engine;
        _studies = studies;
        _identification = identification;
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
            new CsvWindow(new CsvWorkspace(project, _store, _engine), new IdentificationWorkspace(project, _store, _studies, _identification)) { Owner = this }.ShowDialog();
        }
        catch (Exception error)
        {
            Trace.TraceError("project.open {0}", error.GetType().Name);
            MessageBox.Show(this, "Impossible d’ouvrir le projet. Vérifiez le stockage local.", "MKR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
