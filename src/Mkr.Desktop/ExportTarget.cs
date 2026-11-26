using System.IO;
using System.Windows;

namespace Mkr.Desktop;

/// <summary>
/// Chooses where an export is written. Normally the standard « Save as » dialog; when the environment variable
/// <c>MKR_EXPORT_DIRECTORY</c> names an existing folder (automated UI tests only), the proposed file name is written
/// there without a dialog, so that the writing itself can be checked without driving an operating system dialog.
/// </summary>
internal static class ExportTarget
{
    public static string? Choose(Window? owner, string filter, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("MKR_EXPORT_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            return Path.Combine(directory, fileName);
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = fileName };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
}
