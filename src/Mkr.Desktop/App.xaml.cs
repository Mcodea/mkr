using Mkr.Core;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Markup;
using System.IO;
using System.Windows;
using Mkr.Infrastructure;

namespace Mkr.Desktop;

public partial class App : Application
{
    private static void FitToScreen(object sender, RoutedEventArgs args)
    {
        if (sender is not Window window || window.WindowState != WindowState.Normal || !window.IsLoaded) return;
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, area.Width);
        window.MinHeight = Math.Min(window.MinHeight, area.Height);
        window.MaxWidth = area.Width;
        window.MaxHeight = area.Height;
        if (window.Width > area.Width) window.Width = area.Width;
        if (window.Height > area.Height) window.Height = area.Height;
        var width = Math.Min(window.ActualWidth, area.Width);
        var height = Math.Min(window.ActualHeight, area.Height);
        if (!double.IsNaN(window.Left)) window.Left = Math.Max(area.Left, Math.Min(window.Left, area.Right - width));
        if (!double.IsNaN(window.Top)) window.Top = Math.Max(area.Top, Math.Min(window.Top, area.Bottom - height));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Dates and numbers follow the user's regional settings everywhere, including text bound in XAML (WPF would otherwise format with en-US).
        // MKR_CULTURE forces a culture (used by the UI captures, which must show the French format whatever the machine's settings).
        try
        {
            if (Environment.GetEnvironmentVariable("MKR_CULTURE") is { Length: > 0 } forced)
            {
                var culture = CultureInfo.GetCultureInfo(forced);
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
        }
        catch (CultureNotFoundException) { }
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        // Every window fits the usable screen area (1024x768 laptops, docked taskbars) and stays fully on screen.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(FitToScreen));
        StartupWindow? startup = null;
        try
        {
            var directory = Environment.GetEnvironmentVariable("MKR_DATA_DIRECTORY") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKR");
            Directory.CreateDirectory(directory);
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(directory, "mkr.log")));
            Trace.AutoFlush = true;
            Mkr.Core.CurrentUser.Provider = new WindowsIdentityProvider();
            Loc.Initialize(directory);
            ThemeService.RegisterWindowDefaults();
            ThemeService.Initialize(directory);              // the start-up window already follows the chosen theme
            startup = new StartupWindow();
            startup.Show();
            startup.Report(Loc.T("startup.step.database"));
            var store = new SqliteProjectStore(Path.Combine(directory, "projects.db"));
            startup.Report(Loc.T("startup.step.engine"));
            var bundled = Path.Combine(AppContext.BaseDirectory, "python", "python.exe");
            var python = Environment.GetEnvironmentVariable("MKR_PYTHON") ?? (File.Exists(bundled) ? bundled : "python");
            var engine = new PythonDiagnostics(python, Path.Combine(AppContext.BaseDirectory, "engine", "mkr_engine.py"));
            var model = new WorkspaceViewModel(store, engine);
            MainWindow = new WindowView(store, engine, project => new IdentificationWorkspace(project, store, store, engine, store, engine, engine, engine)) { DataContext = model };
            MainWindow.Closed += (_, _) => model.CancelDiagnostics();
            startup.Report(Loc.T("startup.step.workspace"));
            // Automated captures only: keep the start-up window visible for the given time (never set in normal use).
            if (int.TryParse(Environment.GetEnvironmentVariable("MKR_STARTUP_HOLD_MS"), out var hold) && hold > 0)
            {
                // wait while keeping the message loop alive, so that the window stays responsive (UI automation and screen capture need it)
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Min(hold, 10_000)) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            MainWindow.Show();
            startup.Close();
        }
        catch (Exception error)
        {
            startup?.Close();
            Trace.TraceError("startup {0}", error.GetType().Name);
            Trace.TraceError("startup detail {0}", error);          // local log only: the window text below stays generic
            var detail = error is InvalidDataException ? error.Message : Loc.T("cs.verifiez_lacces_au_dossier_local");
            MessageBox.Show(Loc.T("cs.impossible_douvrir_le_stockage_orias", detail), "ORIAS", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
