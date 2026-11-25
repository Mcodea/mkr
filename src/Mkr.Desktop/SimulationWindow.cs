using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="SimulationView"/>: the same view is shown inline in the study workspace.</summary>
internal sealed class SimulationWindow : Window
{
    internal SimulationWindow(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        Title = "MKR — Simulation PI";
        Width = 1140; Height = 850; MinWidth = 760; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var view = new SimulationView(workspace, model, tuning);
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
