using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Mkr.Desktop;

public enum ThemeChoice { System, Light, Dark }

/// <summary>
/// Colour theme of the application. The first merged dictionary of App.xaml is the colour theme: switching replaces it, and every
/// DynamicResource follows at once. The choice is remembered in the data folder; MKR_THEME (system|light|dark) overrides it (tests, captures).
/// </summary>
public static class ThemeService
{
    private static string? _file;

    public static ThemeChoice Choice { get; private set; } = ThemeChoice.System;
    public static bool IsDark { get; private set; }

    /// <summary>Raised after the theme dictionary was swapped, so hand-drawn plots can redraw with the new colours.</summary>
    public static event Action? Changed;

    /// <summary>
    /// An implicit Style for Window never reaches the application's own window classes (WindowView, CsvWindow…: WPF styles apply to the
    /// exact type only), so each window takes its background, text colour and font from the theme here, as resource references:
    /// a theme switch then updates open windows too.
    /// </summary>
    public static void RegisterWindowDefaults() =>
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is not Window window) return;
            window.SetResourceReference(Control.BackgroundProperty, "MkrCanvas");
            window.SetResourceReference(Control.ForegroundProperty, "MkrText");
            window.SetResourceReference(Control.FontFamilyProperty, "MkrFont");
            window.SetResourceReference(Control.FontSizeProperty, "MkrFontBody");
        }));

    public static void Initialize(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "theme.txt");
        var choice = ThemeChoice.System;
        try
        {
            if (Environment.GetEnvironmentVariable("MKR_THEME") is { } forced && Enum.TryParse(forced, true, out ThemeChoice fromEnvironment))
                choice = fromEnvironment;
            else if (File.Exists(_file) && Enum.TryParse(File.ReadAllText(_file).Trim(), true, out ThemeChoice saved))
                choice = saved;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Apply(choice, remember: false);
    }

    public static void Apply(ThemeChoice choice, bool remember = true)
    {
        Choice = choice;
        IsDark = choice == ThemeChoice.Dark || (choice == ThemeChoice.System && !SystemUsesLightTheme());
        var theme = new ResourceDictionary { Source = new Uri($"Resources/Themes/{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Relative) };
        Application.Current.Resources.MergedDictionaries[0] = theme;
        Changed?.Invoke();
        if (!remember || _file is null) return;
        try { File.WriteAllText(_file, choice.ToString()); }
        catch (IOException) { }                                     // a theme that cannot be remembered is still applied
        catch (UnauthorizedAccessException) { }
    }

    private static bool SystemUsesLightTheme()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException) { return true; }
    }
}
