using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="CsvView"/> (project data and process description): the same view is shown inline in the study workspace.</summary>
internal sealed class CsvWindow : Window
{
    internal CsvWindow(CsvWorkspace workspace, IdentificationWorkspace identification)
    {
        Title = $"ORIAS — {workspace.ProjectName}";
        Width = 1160; Height = 820; MinWidth = 800; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var view = new CsvView(workspace);
        view.IdentificationRequested += (_, _) =>
        {
            try { new IdentificationWindow(identification) { Owner = this }.ShowDialog(); }
            catch (Exception error) { System.Diagnostics.Trace.TraceError("identification.open {0}", error.GetType().Name); }
        };
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
