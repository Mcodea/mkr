using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Thin dialog host of <see cref="GuidedTuningView"/>: the same view is shown inline in the study workspace.</summary>
internal sealed class GuidedTuningWindow : Window
{
    internal GuidedTuningWindow(IdentificationWorkspace workspace, IdentificationStudy model) : this(new GuidedTuningView(workspace, model)) { }

    /// <summary>From an accepted ARX / OE study (ADR 0008).</summary>
    internal GuidedTuningWindow(IdentificationWorkspace workspace, ArxStudy study) : this(new GuidedTuningView(workspace, study)) { }

    private GuidedTuningWindow(GuidedTuningView view)
    {
        Title = Loc.T("cs.orias_reglage_guide");
        Width = 900; Height = 720; MinWidth = 640; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        view.CloseRequested += (_, _) => Close();
        Content = view;
        Closed += (_, _) => view.Cancel();
    }
}
