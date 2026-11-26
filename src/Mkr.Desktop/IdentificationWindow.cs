using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="IdentificationView"/>: the same view is shown inline in the study workspace.</summary>
internal sealed class IdentificationWindow : Window
{
    internal IdentificationWindow(IdentificationWorkspace workspace)
    {
        Title = "ORIAS — Identification";
        Width = 960; Height = 780; MinWidth = 620; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var view = new IdentificationView(workspace);
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
