using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mkr.Desktop;

internal static class ComparisonPlot
{
    public static void Draw(Canvas canvas, (double Time, double First, double Second)[]? values)
    {
        canvas.Children.Clear();
        if (values is not { Length: >= 2 } || canvas.ActualWidth <= 120)
            return;
        var minimum = values.Min(p => Math.Min(p.First, p.Second));
        var maximum = values.Max(p => Math.Max(p.First, p.Second));
        var span = maximum > minimum ? maximum - minimum : 1;
        const double left = 82, top = 18, height = 160;
        var width = canvas.ActualWidth - left - 24;
        var seconds = values[^1].Time;
        if (seconds <= 0)
            return;
        var first = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2.5 };
        var second = new Polyline { Stroke = Brushes.DarkOrange, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 } };
        canvas.Children.Add(new System.Windows.Shapes.Path
        {
            Stroke = Brushes.Gray,
            Data = Geometry.Parse(FormattableString.Invariant($"M {left},{top} L {left},{top + height} L {left + width},{top + height}"))
        });
        foreach (var point in values)
        {
            var x = left + point.Time / seconds * width;
            first.Points.Add(new Point(x, top + (maximum - point.First) / span * height));
            second.Points.Add(new Point(x, top + (maximum - point.Second) / span * height));
        }
        canvas.Children.Add(first);
        canvas.Children.Add(second);

        void Label(string text, double x, double y)
        {
            var label = new TextBlock { Text = text };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        Label($"{maximum:G4}", 0, top - 10);
        if (maximum != minimum)
            Label($"{minimum:G4}", 0, top + height - 14);
        Label("0 s", left, top + height + 3);
        Label($"{seconds:G4} s", Math.Max(left, left + width - 68), top + height + 3);
    }
}
