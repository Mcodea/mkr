using System.Windows;
using System.Windows.Input;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class TrialPreviewWindow : Window
{
    private readonly TrialPreview _data;
    private readonly string _mvUnit;
    private readonly string _pvUnit;
    private double? _selectedFrom;
    private double? _selectedTo;
    private StepWindow? _pendingWindow;

    internal StepWindow? SelectedWindow { get; private set; }

    internal TrialPreviewWindow(TrialPreview data, string importName, string mvUnit, string pvUnit)
    {
        _data = data;
        _mvUnit = mvUnit;
        _pvUnit = pvUnit;
        InitializeComponent();
        SourceInfo.Text = $"{importName} · {data.SourceRows} lignes source · " +
            $"{data.DisplayedRows} points affichés · UTC [{data.FirstUtc:u} – {data.LastUtc:u}]" +
            (data.DisplayDecimated
                ? " · Affichage réduit : extrema et fortes variations prioritaires ; certains événements restent potentiellement invisibles."
                : " · Aucun point écarté de l'affichage.");
        SelectionText.Text = "Aucune période sélectionnée. Les plages validées utilisent les indices des lignes originales.";
        var seconds = data.Points.Select(p => p.Seconds).ToArray();
        Chart.XLabel = "Temps écoulé depuis le premier échantillon (s)";
        Chart.SelectionEnabled = true;
        Chart.SetTracks(new[]
        {
            new ChartTrack("Sortie de commande", mvUnit, new[] { new ChartSeries("Appliquée", "MkrSignal1", seconds, data.Points.Select(p => p.Mv).ToArray()) }),
            new ChartTrack("Mesure", pvUnit, new[] { new ChartSeries("Mesure", "MkrSignal2", seconds, data.Points.Select(p => p.Pv).ToArray()) })
        });
        Chart.RangeSelected += Selected;
        Chart.CursorTextChanged += text => CursorText.Text = text.Length == 0 ? "Survolez le graphique pour lire les valeurs au curseur." : text;
    }

    private void Draw()
    {
        Chart.SetHighlight(_selectedFrom, _selectedTo);
    }

    private void Selected(double start, double end)
    {
        // One full pair of original display samples is the minimum inspectable interval.
        var retained = _data.Points.Where(p => p.Seconds >= start && p.Seconds <= end).ToArray();
        if (retained.Length < 2)
        {
            SelectionText.Text = "Période trop courte : sélectionnez au moins deux points affichés.";
            return;
        }

        _selectedFrom = retained[0].Seconds;
        _selectedTo = retained[^1].Seconds;
        _pendingWindow = new StepWindow(retained[0].RowIndex, retained[^1].RowIndex);
        ApplySelection.IsEnabled = true;
        SelectionText.Text = $"Sélection : {_pendingWindow.RowEnd - _pendingWindow.RowStart + 1} lignes originales " +
            $"(dont {retained.Length} affichées) · [{_selectedFrom:G6} s – {_selectedTo:G6} s]. " +
            "Confirmez pour utiliser cette plage lors de la prochaine identification.";
        Draw();
    }

    private void ResetZoom(object sender, RoutedEventArgs args) => Chart.ResetView();

    private void ExportImage(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Image PNG (*.png)|*.png", FileName = "courbes-sources.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportPng(dialog.FileName); SelectionText.Text = "Image exportée."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { SelectionText.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
    }

    private void ExportData(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "courbes-sources.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportCsv(dialog.FileName); SelectionText.Text = "Données affichées exportées (points d'affichage, pas les lignes sources)."; }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { SelectionText.Text = "Export impossible : vérifiez le dossier choisi et vos droits d'écriture."; }
    }

    private void ApplyWindow(object sender, RoutedEventArgs args)
    {
        if (_pendingWindow is not { IsValid: true } window)
            return;
        SelectedWindow = window;
        DialogResult = true;
    }

    private void ClearSelection(object sender, RoutedEventArgs args)
    {
        _selectedFrom = _selectedTo = null;
        _pendingWindow = null;
        ApplySelection.IsEnabled = false;
        SelectionText.Text = "Aucune période sélectionnée. Affichage en lecture seule.";
        Draw();
    }
}
