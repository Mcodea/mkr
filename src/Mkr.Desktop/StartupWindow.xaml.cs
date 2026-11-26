using System.Windows;
using System.Windows.Threading;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Start-up window: logo, tagline, real version and the real initialisation step in progress (no fake progress, no artificial delay).</summary>
public partial class StartupWindow : Window
{
    public StartupWindow()
    {
        InitializeComponent();
        var (number, build) = ProductIdentity.VersionOf(typeof(StartupWindow).Assembly);
        Tagline.Text = ProductIdentity.Tagline;
        VersionLine.Text = build is null ? $"Version {number}" : $"Version {number} · compilation {build}";
    }

    /// <summary>Shows the step being carried out and lets the window paint before the work continues on the UI thread.</summary>
    public void Report(string step)
    {
        Status.Text = step;
        Dispatcher.Invoke(DispatcherPriority.Render, () => { });
    }
}
