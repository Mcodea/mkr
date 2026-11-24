using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Mkr.Desktop;

/// <summary>Read-only view of the project's audit log with a chain verification. Nothing here can alter an entry.</summary>
public partial class AuditWindow : Window
{
    private readonly CsvWorkspace _workspace;

    internal AuditWindow(CsvWorkspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        Load();
    }

    /// <summary>One displayed row. ToString carries every field so that assistive technology (and UI tests) can read a whole row.</summary>
    private sealed record Row(long Sequence, string At, string Actor, string Action, string Subject, string Detail)
    {
        public bool Matches(string needle) =>
            Actor.Contains(needle, StringComparison.OrdinalIgnoreCase) || Action.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
            Subject.Contains(needle, StringComparison.OrdinalIgnoreCase) || Detail.Contains(needle, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => $"{Sequence} {At} {Actor} {Action} {Subject} {Detail}";
    }

    private List<Row> _rows = [];

    private void Load()
    {
        _rows = _workspace.AuditEntries()
            .Select(e => new Row(e.Sequence, e.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "Z",
                e.Actor, e.Action, e.Subject, e.Detail))
            .ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var needle = Filter.Text.Trim();
        var shown = needle.Length == 0 ? _rows : _rows.Where(r => r.Matches(needle)).ToList();
        Entries.ItemsSource = shown;
        Count.Text = _rows.Count == 0 ? "Aucune entrée pour ce projet."
            : needle.Length == 0 ? $"{_rows.Count} entrée(s)" : $"{shown.Count} entrée(s) sur {_rows.Count}";
    }

    private void FilterChanged(object sender, TextChangedEventArgs args) => ApplyFilter();

    private void Export(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exporter le journal d'audit", Filter = "Journal d'audit (*.jsonl)|*.jsonl", DefaultExt = ".jsonl",
            FileName = $"journal-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var result = _workspace.ExportAudit(dialog.FileName);
            VerifyStatus.Text = $"Journal exporté : {result.Entries} entrée(s). Empreinte de tête à noter : {result.HeadHash}" +
                (result.Intact ? "" : " — ATTENTION : la chaîne était rompue au moment de l'export.");
            Load();
        }
        catch (Exception error) { VerifyStatus.Text = "Export impossible : " + error.Message; }
    }

    private void Verify(object sender, RoutedEventArgs args)
    {
        try
        {
            var result = _workspace.VerifyAudit();
            VerifyStatus.Text = result switch
            {
                null => "Journal indisponible pour ce stockage.",
                { Intact: true } r => $"Chaîne intacte : {r.Entries} entrée(s). Empreinte de tête : {r.HeadHash}",
                var r => $"CHAÎNE ROMPUE à l'entrée {r!.FirstBrokenSequence} sur {r.Entries} : une entrée a été modifiée ou retirée."
            };
        }
        catch (Exception error) { VerifyStatus.Text = "Vérification impossible : " + error.Message; }
    }
}
