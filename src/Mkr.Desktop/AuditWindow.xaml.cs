using System.Text;
using System.Windows;

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

    private void Load()
    {
        var text = new StringBuilder();
        foreach (var entry in _workspace.AuditEntries())
            text.AppendLine($"{entry.Sequence,5}  {entry.At.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z  {entry.Actor}  {entry.Action}  {entry.Subject}  {entry.Detail}");
        Entries.Text = text.Length == 0 ? "Aucune entrée pour ce projet." : text.ToString();
    }

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
