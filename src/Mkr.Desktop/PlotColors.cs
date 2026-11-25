using System.Windows;
using System.Windows.Media;

namespace Mkr.Desktop;

/// <summary>
/// Colours of the hand-drawn scientific plots, read from the current theme at drawing time. A signal is never told apart by colour
/// alone: the plots also use different line styles and legends. Mapping: Signal1 = measurement / estimation, Signal2 = model or second
/// method, Signal3 = command, Signal4 = residual or derivative.
/// </summary>
internal static class PlotColors
{
    private static Brush Get(string key) => (Brush)Application.Current.FindResource(key);

    public static Brush Axis => Get("MkrPlotAxis");
    public static Brush Label => Get("MkrPlotLabel");
    public static Brush Grid => Get("MkrPlotGrid");
    public static Brush Signal1 => Get("MkrSignal1");
    public static Brush Signal2 => Get("MkrSignal2");
    public static Brush Signal3 => Get("MkrSignal3");
    public static Brush Signal4 => Get("MkrSignal4");
    public static Brush Status(string key) => Get(key);
}
