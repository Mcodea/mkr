using System.Windows;
using System.Windows.Input;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class TrialPreviewWindow : Window
{
    private readonly TrialPreview _data;
    private readonly string _mvUnit;
    private readonly string _pvUnit;
    private double? _dragOrigin;
    private double? _selectedFrom;
    private double? _selectedTo;

    internal TrialPreviewWindow(TrialPreview data, string importName, string mvUnit, string pvUnit)
    {
        _data = data;
        _mvUnit = mvUnit;
        _pvUnit = pvUnit;
        InitializeComponent();
        SourceInfo.Text = $"{importName} · {data.SourceRows} lignes source · " +
            $"{data.DisplayedRows} points affichés · UTC [{data.FirstUtc:u} – {data.LastUtc:u}]" +
            (data.DisplayDecimated
                ? " · Affichage décimé : certaines variations rapides peuvent être masquées."
                : " · Aucun point écarté de l'affichage.");
        SelectionText.Text = "Aucune période sélectionnée. Affichage en lecture seule.";
    }

    private void PlotResize(object sender, SizeChangedEventArgs args) => Draw();

    private void Draw() => TrialPreviewPlot.Draw(Plot, _data, _mvUnit, _pvUnit, _selectedFrom, _selectedTo);

    private void StartSelection(object sender, MouseButtonEventArgs args)
    {
        if (TrialPreviewPlot.TimeAt(args.GetPosition(Plot).X, Plot.ActualWidth, _data, out var time))
        {
            _dragOrigin = time;
            Plot.CaptureMouse();
        }
    }

    private void EndSelection(object sender, MouseButtonEventArgs args)
    {
        if (_dragOrigin is not { } origin)
            return;
        _dragOrigin = null;
        Plot.ReleaseMouseCapture();
        if (!TrialPreviewPlot.TimeAt(args.GetPosition(Plot).X, Plot.ActualWidth, _data, out var time))
            return;
        var start = Math.Min(time, origin);
        var end = Math.Max(time, origin);
        // One full pair of original display samples is the minimum inspectable interval.
        var retained = _data.Points.Where(p => p.Seconds >= start && p.Seconds <= end).ToArray();
        if (retained.Length < 2)
        {
            SelectionText.Text = "Période trop courte : sélectionnez au moins deux points affichés.";
            return;
        }

        _selectedFrom = retained[0].Seconds;
        _selectedTo = retained[^1].Seconds;
        SelectionText.Text = $"Inspection uniquement · {retained.Length} points affichés · " +
            $"[{_selectedFrom:G6} s – {_selectedTo:G6} s] depuis l'origine UTC. " +
            "La sélection n'est pas appliquée à l'identification.";
        Draw();
    }

    private void CancelSelection(object sender, MouseEventArgs args)
    {
        if (_dragOrigin is null || args.LeftButton == MouseButtonState.Pressed)
            return;
        _dragOrigin = null;
        Plot.ReleaseMouseCapture();
    }

    private void ClearSelection(object sender, RoutedEventArgs args)
    {
        _selectedFrom = _selectedTo = _dragOrigin = null;
        Plot.ReleaseMouseCapture();
        SelectionText.Text = "Aucune période sélectionnée. Affichage en lecture seule.";
        Draw();
    }
}
