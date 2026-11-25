using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="BlockTargetView"/>: the same view is shown inline in the study workspace.</summary>
internal sealed class BlockTargetWindow : Window
{
    internal BlockTargetWindow(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        Title = "MKR — Valeurs des blocs PID_INT et PID_Compact (non qualifiées)";
        Width = 900; Height = 820; MinWidth = 620; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var view = new BlockTargetView(workspace, model, tuning);
        view.CloseRequested += (_, _) => Close();
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
