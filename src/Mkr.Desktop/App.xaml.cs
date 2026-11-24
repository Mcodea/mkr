using System.Diagnostics;
using System.IO;
using System.Windows;
using Mkr.Infrastructure;

namespace Mkr.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var directory = Environment.GetEnvironmentVariable("MKR_DATA_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKR");
            Directory.CreateDirectory(directory);
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(directory, "mkr.log")));
            Trace.AutoFlush = true;
            Mkr.Core.CurrentUser.Provider = new WindowsIdentityProvider();
            var store = new SqliteProjectStore(Path.Combine(directory, "projects.db"));
            var bundled = Path.Combine(AppContext.BaseDirectory, "python", "python.exe");
            var python = Environment.GetEnvironmentVariable("MKR_PYTHON") ?? (File.Exists(bundled) ? bundled : "python");
            var engine = new PythonDiagnostics(python, Path.Combine(AppContext.BaseDirectory, "engine", "mkr_engine.py"));
            var model = new WorkspaceViewModel(store, engine);
            MainWindow = new WindowView(store, engine, project => new IdentificationWorkspace(project, store, store, engine, store, engine, engine, engine)) { DataContext = model };
            MainWindow.Closed += (_, _) => model.CancelDiagnostics();
            MainWindow.Show();
        }
        catch (Exception error)
        {
            Trace.TraceError("startup {0}", error.GetType().Name);
            var detail = error is InvalidDataException ? error.Message : "Vérifiez l’accès au dossier local MKR.";
            MessageBox.Show($"Impossible d’ouvrir le stockage MKR. {detail}", "MKR", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
