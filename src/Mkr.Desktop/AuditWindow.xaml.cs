using Mkr.Core;
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
            .Select(e => new Row(e.Sequence, e.At.UtcDateTime.ToString(Loc.T("cs.yyyy_mm_dd_hh_mm"), System.Globalization.CultureInfo.InvariantCulture) + "Z",
                e.Actor, e.Action, e.Subject, e.Detail))
            .ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var needle = Filter.Text.Trim();
        var shown = needle.Length == 0 ? _rows : _rows.Where(r => r.Matches(needle)).ToList();
        Entries.ItemsSource = shown;
        Count.Text = _rows.Count == 0 ? Loc.T("cs.aucune_entree_pour_ce_projet")
            : needle.Length == 0 ? Loc.T("cs.entree_s", _rows.Count) : Loc.T("cs.entree_s_sur", shown.Count, _rows.Count);
    }

    private void FilterChanged(object sender, TextChangedEventArgs args) => ApplyFilter();

    private void Export(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.T("cs.exporter_le_journal_d_audit"), Filter = Loc.T("cs.journal_d_audit_jsonl_jsonl"), DefaultExt = ".jsonl",
            FileName = $"journal-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var result = _workspace.ExportAudit(dialog.FileName);
            VerifyStatus.Text = Loc.T("cs.journal_exporte_entree_s_empreinte", result.Entries, result.HeadHash) +
                (result.Intact ? "" : Loc.T("cs.attention_la_chaine_etait_rompue"));
            Load();
        }
        catch (Exception error) { VerifyStatus.Text = Loc.T("cs.export_impossible") + error.Message; }
    }

    private void Verify(object sender, RoutedEventArgs args)
    {
        try
        {
            var result = _workspace.VerifyAudit();
            VerifyStatus.Text = result switch
            {
                null => Loc.T("cs.journal_indisponible_pour_ce_stockage"),
                { Intact: true } r => Loc.T("cs.chaine_intacte_entree_s_empreinte", r.Entries, r.HeadHash),
                var r => Loc.T("cs.chaine_rompue_a_l_entree", r!.FirstBrokenSequence, r.Entries)
            };
        }
        catch (Exception error) { VerifyStatus.Text = Loc.T("cs.verification_impossible") + error.Message; }
    }
}
