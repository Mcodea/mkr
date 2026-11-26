using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="OpenLoopView"/>: the same view is shown inline in the study workspace.</summary>
internal sealed class OpenLoopWindow : Window
{
    internal OpenLoopWindow(IdentificationWorkspace workspace, IdentificationStudy model)
    {
        Title = Loc.T("cs.orias_simulation_en_boucle_ouverte");
        Width = 900; Height = 720; MinWidth = 540; MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var view = new OpenLoopView(workspace, model);
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
