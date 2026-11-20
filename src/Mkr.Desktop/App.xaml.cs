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
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKR");
            Directory.CreateDirectory(directory);
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(directory, "mkr.log")));
            Trace.AutoFlush = true;
            var store = new SqliteProjectStore(Path.Combine(directory, "projects.db"));
            var python = Environment.GetEnvironmentVariable("MKR_PYTHON") ?? "python";
            var engine = new PythonDiagnostics(python, Path.Combine(AppContext.BaseDirectory, "engine", "mkr_engine.py"));
            var model = new WorkspaceViewModel(store, engine);
            MainWindow = new WindowView { DataContext = model };
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
