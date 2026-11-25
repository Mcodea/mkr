using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Collects the installation values a PID_INT or PID_Compact conversion needs. Nothing is pre-filled or guessed.</summary>
public partial class BlockTargetWindow : Window
{
    private static readonly string[] CompactVersions = { "V2.0", "V2.1", "V2.2", "V2.3", "V2.4", "V3.0" };
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly PiStudy _tuning;
    private readonly CancellationTokenSource _lifetime = new();

    internal BlockTargetWindow(IdentificationWorkspace workspace, IdentificationStudy model, PiStudy tuning)
    {
        InitializeComponent();
        _workspace = workspace;
        _model = model;
        _tuning = tuning;
        foreach (var version in CompactVersions) Version.Items.Add(version);
        Closed += (_, _) => _lifetime.Cancel();
    }

    private string? Block => (BlockChoice.SelectedItem as ComboBoxItem)?.Tag as string;

    private void BlockChanged(object sender, SelectionChangedEventArgs args)
    {
        IntPanel.Visibility = Block == "PID_INT" ? Visibility.Visible : Visibility.Collapsed;
        CompactPanel.Visibility = Block == "PID_Compact" ? Visibility.Visible : Visibility.Collapsed;
        Compute.IsEnabled = Block is not null;
        Values.Text = "";
        Status.Text = "";
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value) && value > 0;

    private bool TryInstallation(out Dictionary<string, object> values)
    {
        values = new Dictionary<string, object>();
        if (Block == "PID_INT")
        {
            if (!TryNumber(PvSpan.Text, out var pv) || !TryNumber(MvSpan.Text, out var mv) || !TryNumber(Ts.Text, out var ts)) return false;
            values["pvSpan"] = pv; values["mvSpan"] = mv; values["tsSeconds"] = ts;
            return true;
        }
        if (Version.SelectedItem is not string version || !TryNumber(PercentPerUnit.Text, out var percent)) return false;
        values["version"] = version; values["outputPercentPerMvUnit"] = percent;
        var cpu = (Cpu.SelectedItem as ComboBoxItem)?.Tag as string;
        if (!string.IsNullOrEmpty(cpu))
        {
            if (string.IsNullOrWhiteSpace(Firmware.Text)) return false;      // a CPU family without a firmware cannot be checked
            values["cpuFamily"] = cpu; values["firmware"] = Firmware.Text.Trim();
        }
        return true;
    }

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        if (Block is not { } block) return;
        if (!TryInstallation(out var installation))
        {
            Status.Text = "Renseignez toutes les valeurs demandées (nombres strictement positifs).";
            return;
        }
        var token = _lifetime.Token;
        try
        {
            var sections = new List<string>();
            foreach (var (simc, label) in new[] { (false, "Lambda (Dahlin)"), (true, "SIMC") })
            {
                var result = await _workspace.ConvertToBlockAsync(_model, _tuning, simc, block, installation, token);
                token.ThrowIfCancellationRequested();
                sections.Add(result.Settings is { } settings ? settings.Describe(label) : $"{label} : {result.RefusalText}");
            }
            Values.Text = string.Join("\n──────────\n\n", sections);
            Status.Text = "Valeurs affichées. Non qualifiées.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
    }
}
