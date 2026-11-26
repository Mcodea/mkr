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
        SourceInfo.Text = Loc.T("cs.lignes_source_points_affiches_utc", importName, data.SourceRows, data.DisplayedRows, data.FirstUtc, data.LastUtc) +
            (data.DisplayDecimated
                ? Loc.T("cs.affichage_reduit_extrema_et_fortes")
                : Loc.T("cs.aucun_point_ecarte_de_l"));
        SelectionText.Text = Loc.T("cs.aucune_periode_selectionnee_les_plages");
        var seconds = data.Points.Select(p => p.Seconds).ToArray();
        Chart.XLabel = Loc.T("cs.temps_ecoule_depuis_le_premier");
        Chart.SelectionEnabled = true;
        Chart.SetTracks(new[]
        {
            new ChartTrack(Loc.T("cs.sortie_de_commande"), mvUnit, new[] { new ChartSeries(Loc.T("cs.appliquee"), "MkrSignal1", seconds, data.Points.Select(p => p.Mv).ToArray()) }),
            new ChartTrack("Mesure", pvUnit, new[] { new ChartSeries("Mesure", "MkrSignal2", seconds, data.Points.Select(p => p.Pv).ToArray()) })
        });
        Chart.RangeSelected += Selected;
        Chart.CursorTextChanged += text => CursorText.Text = text.Length == 0 ? Loc.T("cs.survolez_le_graphique_pour_lire") : text;
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
            SelectionText.Text = Loc.T("cs.periode_trop_courte_selectionnez_au");
            return;
        }

        _selectedFrom = retained[0].Seconds;
        _selectedTo = retained[^1].Seconds;
        _pendingWindow = new StepWindow(retained[0].RowIndex, retained[^1].RowIndex);
        ApplySelection.IsEnabled = true;
        SelectionText.Text = Loc.T("cs.selection_lignes_originales_dont_affichees", _pendingWindow.RowEnd - _pendingWindow.RowStart + 1, retained.Length, _selectedFrom, _selectedTo);
        Draw();
    }

    private void ResetZoom(object sender, RoutedEventArgs args) => Chart.ResetView();

    private void ExportImage(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("cs.image_png_png_png"), FileName = "courbes-sources.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportPng(dialog.FileName); SelectionText.Text = Loc.T("cs.image_exportee"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { SelectionText.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
    }

    private void ExportData(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = Loc.T("cs.csv_csv_csv"), FileName = "courbes-sources.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.ExportCsv(dialog.FileName); SelectionText.Text = Loc.T("cs.donnees_affichees_exportees_points_d"); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        { SelectionText.Text = Loc.T("cs.export_impossible_verifiez_le_dossier"); }
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
        SelectionText.Text = Loc.T("cs.aucune_periode_selectionnee_affichage_en");
        Draw();
    }
}
