using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>
/// Non-destructive, two-track scientific display of already recorded identification data.
/// Deliberately no resampling or modification of the stored FitPoint values.
/// </summary>
internal static class IdentificationDiagnosticsPlot
{
    public const double Left = 90;
    public const double Right = 24;
    private const double Top = 24;
    private const double TrackHeight = 122;
    private const double Gap = 50;
    private const double Bottom = Top + 2 * TrackHeight + Gap;

    public static bool TryTimeAt(double x, double canvasWidth, double start, double end, out double time)
    {
        time = 0;
        var width = canvasWidth - Left - Right;
        if (width <= 1 || x < Left || x > Left + width || end <= start)
            return false;
        time = start + (x - Left) / width * (end - start);
        return double.IsFinite(time);
    }

    public static void Draw(Canvas canvas, FitPoint[]? original, string pvUnit,
                            double? from = null, double? to = null)
    {
        canvas.Children.Clear();
        if (original is not { Length: >= 2 } || canvas.ActualWidth < 260)
            return;

        var valid = original.Where(p => double.IsFinite(p.Seconds) &&
                      double.IsFinite(p.Observed) && double.IsFinite(p.Predicted)).ToArray();
        if (valid.Length < 2)
            return;

        var start = from ?? valid[0].Seconds;
        var end = to ?? valid[^1].Seconds;
        if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start)
            return;
        var visible = valid.Where(p => p.Seconds >= start && p.Seconds <= end).ToArray();
        if (visible.Length < 2)
        {
            Label(canvas, "Aucun intervalle exploitable dans la sélection.", Left, Top, Brushes.DimGray);
            return;
        }

        var width = canvas.ActualWidth - Left - Right;
        var pvMin = visible.Min(p => Math.Min(p.Observed, p.Predicted));
        var pvMax = visible.Max(p => Math.Max(p.Observed, p.Predicted));
        var residualMin = visible.Min(p => p.Observed - p.Predicted);
        var residualMax = visible.Max(p => p.Observed - p.Predicted);
        // The zero line remains visible for residual interpretation.
        residualMin = Math.Min(0, residualMin);
        residualMax = Math.Max(0, residualMax);
        var pvSpan = pvMax > pvMin ? pvMax - pvMin : Math.Max(1.0, Math.Abs(pvMax) * 0.1);
        var residualSpan = residualMax > residualMin ? residualMax - residualMin
            : Math.Max(1.0, Math.Abs(residualMax) * 0.1);
        var pvMargin = pvSpan * 0.08;
        var residualMargin = residualSpan * 0.08;
        pvMin -= pvMargin;
        pvMax += pvMargin;
        residualMin -= residualMargin;
        residualMax += residualMargin;

        double X(double seconds) => Left + (seconds - start) / (end - start) * width;
        double Y(double value, double minimum, double maximum, double top) =>
            top + (maximum - value) / (maximum - minimum) * TrackHeight;

        void Track(double top, double min, double max, string label)
        {
            var axis = new Polyline
            {
                Stroke = Brushes.SlateGray, StrokeThickness = 1,
                Points = new PointCollection
                {
                    new(Left, top), new(Left, top + TrackHeight),
                    new(Left + width, top + TrackHeight)
                }
            };
            canvas.Children.Add(axis);
            Label(canvas, label, Left, top - 22, Brushes.DimGray);
            Label(canvas, max.ToString("G5", CultureInfo.InvariantCulture), 4, top - 6, Brushes.DimGray);
            Label(canvas, min.ToString("G5", CultureInfo.InvariantCulture), 4,
                  top + TrackHeight - 16, Brushes.DimGray);
        }

        Track(Top, pvMin, pvMax, $"PV ({pvUnit}) — mesurée / prédite");
        var residualTop = Top + TrackHeight + Gap;
        Track(residualTop, residualMin, residualMax, $"Résidu ({pvUnit}) — mesurée − prédite");

        var observed = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2.3 };
        var predicted = new Polyline
        {
            Stroke = Brushes.DarkOrange, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 }
        };
        var residual = new Polyline { Stroke = Brushes.MediumVioletRed, StrokeThickness = 1.7 };
        foreach (var point in visible)
        {
            var x = X(point.Seconds);
            observed.Points.Add(new Point(x, Y(point.Observed, pvMin, pvMax, Top)));
            predicted.Points.Add(new Point(x, Y(point.Predicted, pvMin, pvMax, Top)));
            residual.Points.Add(new Point(x, Y(point.Observed - point.Predicted,
                                              residualMin, residualMax, residualTop)));
        }
        canvas.Children.Add(observed);
        canvas.Children.Add(predicted);
        var zero = Y(0, residualMin, residualMax, residualTop);
        var reference = new Line
        {
            X1 = Left, X2 = Left + width, Y1 = zero, Y2 = zero,
            Stroke = Brushes.DarkGray, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 3 }
        };
        canvas.Children.Add(reference);
        canvas.Children.Add(residual);
        Label(canvas, $"{start:G5} s", Left, Bottom + 8, Brushes.DimGray);
        Label(canvas, $"{end:G5} s", Math.Max(Left, Left + width - 88), Bottom + 8, Brushes.DimGray);
        Label(canvas, $"Temps après échelon (s) · {visible.Length} points affichés",
              Left, Bottom + 32, Brushes.DimGray);
    }

    private static void Label(Canvas canvas, string text, double x, double y, Brush brush)
    {
        var label = new TextBlock
        {
            Text = text, Foreground = brush, FontSize = 11,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        canvas.Children.Add(label);
    }
}
