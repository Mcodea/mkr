using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Display-only MV and PV tracks. No model inputs or source rows are modified.</summary>
internal static class TrialPreviewPlot
{
    public const double Left = 90;
    public const double Right = 24;

    public static bool TimeAt(double x, double width, TrialPreview data, out double seconds)
    {
        seconds = 0;
        if (data.Points.Length < 2 || width - Left - Right <= 1 ||
            x < Left || x > width - Right)
            return false;
        var span = data.Points[^1].Seconds - data.Points[0].Seconds;
        if (span <= 0)
            return false;
        seconds = data.Points[0].Seconds +
                  (x - Left) / (width - Left - Right) * span;
        return double.IsFinite(seconds);
    }

    public static void Draw(Canvas canvas, TrialPreview data, double? selectedStart = null,
                            double? selectedEnd = null)
    {
        canvas.Children.Clear();
        if (canvas.ActualWidth < 240 || canvas.ActualHeight < 300 || data.Points.Length < 2)
            return;
        var points = data.Points;
        var t0 = points[0].Seconds;
        var t1 = points[^1].Seconds;
        var seconds = t1 - t0;
        if (seconds <= 0)
            return;

        var width = canvas.ActualWidth - Left - Right;
        var trackHeight = Math.Max(74, (canvas.ActualHeight - 110) / 2);
        var mvTop = 31.0;
        var pvTop = mvTop + trackHeight + 45;
        double X(double t) => Left + (t - t0) / seconds * width;

        void Label(string title, double x, double y, Brush brush)
        {
            var label = new TextBlock { Text = title, Foreground = brush, FontSize = 11 };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        void Track(string title, Func<TrialPreviewPoint, double> value, double top, Brush brush)
        {
            var minimum = points.Min(value);
            var maximum = points.Max(value);
            var span = maximum - minimum;
            var pad = span > 0 ? span * 0.08 : Math.Max(0.1, Math.Abs(minimum) * 0.1);
            minimum -= pad;
            maximum += pad;
            double Y(double v) => top + (maximum - v) / (maximum - minimum) * trackHeight;

            Label(title, Left, top - 20, brush);
            Label(maximum.ToString("G5", CultureInfo.InvariantCulture), 2, top - 7, Brushes.DimGray);
            Label(minimum.ToString("G5", CultureInfo.InvariantCulture), 2,
                  top + trackHeight - 14, Brushes.DimGray);
            canvas.Children.Add(new Polyline
            {
                Stroke = Brushes.SlateGray, StrokeThickness = 1,
                Points = new PointCollection { new(Left, top), new(Left, top + trackHeight),
                    new(Left + width, top + trackHeight) }
            });
            var curve = new Polyline { Stroke = brush, StrokeThickness = 2 };
            foreach (var point in points)
                curve.Points.Add(new Point(X(point.Seconds), Y(value(point))));
            canvas.Children.Add(curve);
        }

        Track("MV — échelle propre (unité déclarée dans l'étude)", p => p.Mv, mvTop, Brushes.SteelBlue);
        Track("PV — échelle propre (unité déclarée dans l'étude)", p => p.Pv, pvTop, Brushes.DarkOrange);

        if (selectedStart is { } a && selectedEnd is { } b && b > a)
        {
            var x0 = X(a);
            var x1 = X(b);
            var overlay = new Rectangle
            {
                Width = Math.Max(0, x1 - x0),
                Height = pvTop + trackHeight - mvTop,
                Fill = new SolidColorBrush(Color.FromArgb(28, 40, 108, 170)),
                Stroke = Brushes.SteelBlue,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(overlay, x0);
            Canvas.SetTop(overlay, mvTop);
            canvas.Children.Add(overlay);
        }

        Label($"{t0:G5} s", Left, pvTop + trackHeight + 8, Brushes.DimGray);
        Label($"{t1:G5} s", Math.Max(Left, Left + width - 80),
              pvTop + trackHeight + 8, Brushes.DimGray);
        Label("Temps écoulé depuis le premier échantillon (s)", Left,
              pvTop + trackHeight + 28, Brushes.DimGray);
    }
}
