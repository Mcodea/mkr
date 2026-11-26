using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Mkr.Desktop;

/// <summary>
/// A borderless (custom caption) window that is maximised would cover the taskbar. Windows asks the window for its maximised rectangle
/// (WM_GETMINMAXINFO): answer with the work area of the monitor the window is on, so the status bar stays above the taskbar.
/// </summary>
public partial class WindowView
{
    private const int WmGetMinMaxInfo = 0x0024, WmDisplayChange = 0x007E, WmSettingChange = 0x001A;
    private const int SpiSetWorkArea = 0x002F;
    private const uint MonitorDefaultToNearest = 2;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(MaximisedRectangle);
    }

    private IntPtr MaximisedRectangle(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // The screen size or the work area changed (resolution, scaling, taskbar moved): a maximised window is not asked again by Windows, so maximise it anew.
        if (message == WmDisplayChange || (message == WmSettingChange && wParam.ToInt64() == SpiSetWorkArea))
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (WindowState != WindowState.Maximized) return;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return IntPtr.Zero;
        }
        if (message != WmGetMinMaxInfo) return IntPtr.Zero;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;
        var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition.X = info.Work.Left - info.Monitor.Left;
        limits.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
        limits.MaxSize.X = info.Work.Right - info.Work.Left;
        limits.MaxSize.Y = info.Work.Bottom - info.Work.Top;
        Marshal.StructureToPtr(limits, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point32 { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect32 { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point32 Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect32 Monitor, Work; public int Flags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
