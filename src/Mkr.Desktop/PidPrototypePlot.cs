using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Read-only scientific comparison: no resampling, interpolation or stored results.</summary>
internal static class PidPrototypePlot
{
    public static void Draw(Canvas canvas, PidPrototypeResult? pid, SimulationRun? pi,
        PiScenario scenario, string pvUnit, string mvUnit)
    {
        canvas.Children.Clear();
        if (pid is null || pi is null || canvas.ActualWidth < 310 || canvas.ActualHeight < 390)
            return;
        var pidPoints = pid.Points;
        var piPoints = pi.Points;
        if (pidPoints.Length != piPoints.Length || pidPoints.Length < 3 ||
            pidPoints.Where((p, index) => p.Seconds != piPoints[index].Seconds).Any())
            return;
        double left = 80, right = 20, top = 28, gap = 36;
        var width = canvas.ActualWidth - left - right;
        var h = (canvas.ActualHeight - top - 2 * gap - 54) / 3;
        if (h < 65) return;
        var end = pidPoints[^1].Seconds;
        if (end <= 0) return;
        double X(double seconds) => left + width * seconds / end;

        void Label(string content, double x, double y, Brush brush)
        {
            var label = new TextBlock
            {
                Text = content, FontSize = 11, Foreground = brush,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        void Track(string title, double y0, IEnumerable<double> series,
            Action<Func<double, double>> render)
        {
            var values = series.Where(double.IsFinite).ToArray();
            if (values.Length == 0) return;
            var minimum = values.Min();
            var maximum = values.Max();
            var padding = maximum > minimum
                ? 0.08 * (maximum - minimum)
                : Math.Max(0.1, Math.Abs(maximum) * 0.1);
            minimum -= padding;
            maximum += padding;
            double Y(double value) => y0 + h * (maximum - value) / (maximum - minimum);
            canvas.Children.Add(new Polyline
            {
                Stroke = PlotColors.Axis, StrokeThickness = 1,
                Points = new PointCollection
                {
                    new(left, y0), new(left, y0 + h), new(left + width, y0 + h)
                }
            });
            Label(title, left, y0 - 21, PlotColors.Label);
            Label(maximum.ToString("G5", CultureInfo.InvariantCulture), 2, y0 - 6, PlotColors.Label);
            Label(minimum.ToString("G5", CultureInfo.InvariantCulture), 2, y0 + h - 16, PlotColors.Label);
            render(Y);
        }

        void Curve(IEnumerable<(double Seconds, double Value)> values,
            Func<double, double> y, Brush color, bool dashed = false)
        {
            var curve = new Polyline { Stroke = color, StrokeThickness = 2, IsHitTestVisible = false };
            if (dashed)
                curve.StrokeDashArray = new DoubleCollection { 4, 3 };
            foreach (var (seconds, value) in values)
                if (double.IsFinite(seconds) && double.IsFinite(value))
                    curve.Points.Add(new Point(X(seconds), y(value)));
            canvas.Children.Add(curve);
        }

        Track($"PV / SP ({pvUnit})", top,
            piPoints.Select(p => p.Pv).Concat(pidPoints.Select(p => p.Pv))
                .Append(scenario.Setpoint),
            y =>
            {
                var reference = y(scenario.Setpoint);
                canvas.Children.Add(new Line
                {
                    X1 = left, X2 = left + width, Y1 = reference, Y2 = reference,
                    Stroke = PlotColors.Signal3, StrokeThickness = 1.4,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    IsHitTestVisible = false
                });
                Curve(piPoints.Select(p => (p.Seconds, p.Pv)), y, PlotColors.Signal1);
                Curve(pidPoints.Select(p => (p.Seconds, p.Pv)), y, PlotColors.Signal2);
            });

        var secondTop = top + h + gap;
        Track($"MV appliquée ({mvUnit})", secondTop,
            piPoints.Where(p => p.Mv.HasValue).Select(p => p.Mv!.Value)
                .Concat(pidPoints.Where(p => p.Mv.HasValue).Select(p => p.Mv!.Value))
                .Append(scenario.MinimumMv).Append(scenario.MaximumMv),
            y =>
            {
                Curve(piPoints.Where(p => p.Mv.HasValue)
                    .Select(p => (p.Seconds, p.Mv!.Value)), y, PlotColors.Signal1);
                Curve(pidPoints.Where(p => p.Mv.HasValue)
                    .Select(p => (p.Seconds, p.Mv!.Value)), y, PlotColors.Signal2);
            });

        var thirdTop = secondTop + h + gap;
        Track($"Action dérivée PID ({mvUnit})", thirdTop,
            pidPoints.Where(p => p.Derivative.HasValue).Select(p => p.Derivative!.Value)
                .Append(0),
            y =>
            {
                var zero = y(0);
                canvas.Children.Add(new Line
                {
                    X1 = left, X2 = left + width, Y1 = zero, Y2 = zero,
                    Stroke = PlotColors.Axis, StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 4 },
                    IsHitTestVisible = false
                });
                Curve(pidPoints.Where(p => p.Derivative.HasValue)
                    .Select(p => (p.Seconds, p.Derivative!.Value)), y, PlotColors.Signal4);
            });
        Label("0 s", left, thirdTop + h + 7, PlotColors.Label);
        Label($"{end:G6} s", left + width - 82, thirdTop + h + 7, PlotColors.Label);
        Label("Temps depuis l'origine du scénario (s)", left,
              thirdTop + h + 26, PlotColors.Label);
    }
}
