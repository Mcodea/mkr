using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Scientific display of existing PI simulation series. No recalculation or resampling.</summary>
internal static class PiComparisonPlot
{
    internal const double Left = 90;
    internal const double Right = 25;
    private const double Top = 28;
    private const double Gap = 44;

    internal static bool TryTimeAt(double x, double width, double start, double end, out double seconds)
    {
        seconds = 0;
        if (!double.IsFinite(x) || !double.IsFinite(width) ||
            !double.IsFinite(start) || !double.IsFinite(end) || end <= start ||
            width - Left - Right <= 1 || x < Left || x > width - Right)
            return false;
        seconds = start + (x - Left) / (width - Left - Right) * (end - start);
        return double.IsFinite(seconds);
    }

    public static void Draw(Canvas canvas, PiSimulationStudy? study, string pvUnit, string mvUnit,
                            double? fromSeconds = null, double? toSeconds = null, double? cursorSeconds = null)
    {
        canvas.Children.Clear();
        if (study is null || canvas.ActualWidth < 260 || canvas.ActualHeight < 400)
            return;

        var allImc = study.Result.Imc.Points;
        var allSimc = study.Result.Simc.Points;
        if (allImc.Length < 3 || allSimc.Length != allImc.Length)
            return;

        var t0 = fromSeconds ?? allImc[0].Seconds;
        var t1 = toSeconds ?? allImc[^1].Seconds;
        var imc = allImc.Where(p => p.Seconds >= t0 && p.Seconds <= t1).ToArray();
        var simc = allSimc.Where(p => p.Seconds >= t0 && p.Seconds <= t1).ToArray();
        if (imc.Length < 2 || simc.Length != imc.Length ||
            imc.Where((p, i) => p.Seconds != simc[i].Seconds).Any())
            return;

        var width = canvas.ActualWidth - Left - Right;
        var trackHeight = Math.Max(80.0, (canvas.ActualHeight - Top - 2 * Gap - 55) / 3);
        if (!double.IsFinite(t0) || !double.IsFinite(t1) || t1 <= t0)
            return;
        double X(double time) => Left + width * (time - t0) / (t1 - t0);

        void Label(string content, double x, double y, Brush color)
        {
            var text = new TextBlock
            {
                Text = content, FontSize = 11, Foreground = color, IsHitTestVisible = false
            };
            Canvas.SetLeft(text, x);
            Canvas.SetTop(text, y);
            canvas.Children.Add(text);
        }

        void Curve(IEnumerable<(double Time, double Value)> points,
                   Func<double, double> y, Brush brush, bool dashed = false)
        {
            var series = new Polyline { Stroke = brush, StrokeThickness = 2, IsHitTestVisible = false };
            if (dashed)
                series.StrokeDashArray = new DoubleCollection { 5, 3 };
            foreach (var (time, value) in points)
                if (double.IsFinite(value) && double.IsFinite(time))
                    series.Points.Add(new Point(X(time), y(value)));
            canvas.Children.Add(series);
        }

        void Track(string title, double top, IEnumerable<double> all,
                   Action<Func<double, double>> draw)
        {
            var values = all.Where(double.IsFinite).ToArray();
            if (values.Length == 0)
                return;
            var minimum = values.Min();
            var maximum = values.Max();
            var span = maximum - minimum;
            var padding = span > 0 ? span * 0.08 : Math.Max(0.1, Math.Abs(minimum) * 0.1);
            minimum -= padding;
            maximum += padding;
            double Y(double value) => top + trackHeight * (maximum - value) / (maximum - minimum);
            canvas.Children.Add(new Polyline
            {
                Stroke = PlotColors.Axis,
                StrokeThickness = 1,
                IsHitTestVisible = false,
                Points = new PointCollection
                {
                    new(Left, top), new(Left, top + trackHeight),
                    new(Left + width, top + trackHeight)
                }
            });
            Label(title, Left, top - 21, PlotColors.Label);
            Label(maximum.ToString("G5", CultureInfo.InvariantCulture), 2, top - 8, PlotColors.Label);
            Label(minimum.ToString("G5", CultureInfo.InvariantCulture), 2,
                  top + trackHeight - 15, PlotColors.Label);
            draw(Y);
        }

        var pvValues = imc.Select(p => p.Pv).Concat(simc.Select(p => p.Pv))
            .Append(study.Scenario.Setpoint);
        if (study.Robustness?.Envelope is { } plotEnvelope &&
            plotEnvelope.Seconds.Length == allImc.Length)
        {
            var visible = Enumerable.Range(0, plotEnvelope.Seconds.Length)
                .Where(i => plotEnvelope.Seconds[i] >= t0 && plotEnvelope.Seconds[i] <= t1).ToArray();
            pvValues = pvValues.Concat(visible.SelectMany(i => new[]
            {
                plotEnvelope.Imc.MinPv[i], plotEnvelope.Imc.MaxPv[i],
                plotEnvelope.Simc.MinPv[i], plotEnvelope.Simc.MaxPv[i]
            }));
        }
        Track($"ΔPV et ΔSP ({pvUnit})", Top, pvValues, y =>
        {
            var reference = y(study.Scenario.Setpoint);
            canvas.Children.Add(new Line
            {
                X1 = Left, X2 = Left + width, Y1 = reference, Y2 = reference,
                Stroke = PlotColors.Signal3, StrokeThickness = 1.4,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                IsHitTestVisible = false
            });
            if (study.Robustness?.Envelope is { } envelope &&
                envelope.Seconds.Length == allImc.Length &&
                envelope.Imc.MinPv.Length == allImc.Length &&
                envelope.Imc.MaxPv.Length == allImc.Length &&
                envelope.Simc.MinPv.Length == allImc.Length &&
                envelope.Simc.MaxPv.Length == allImc.Length)
            {
                void Band(PiPvBounds values, Color color)
                {
                    var selected = Enumerable.Range(0, envelope.Seconds.Length)
                        .Where(i => envelope.Seconds[i] >= t0 && envelope.Seconds[i] <= t1)
                        .ToArray();
                    if (selected.Length < 2)
                        return;
                    var region = new Polygon
                    {
                        Fill = new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B)),
                        StrokeThickness = 0,
                        IsHitTestVisible = false
                    };
                    foreach (var i in selected)
                        region.Points.Add(new Point(X(envelope.Seconds[i]), y(values.MaxPv[i])));
                    foreach (var i in selected.Reverse())
                        region.Points.Add(new Point(X(envelope.Seconds[i]), y(values.MinPv[i])));
                    canvas.Children.Add(region);
                }
                Band(envelope.Imc, Colors.SteelBlue);
                Band(envelope.Simc, Colors.DarkOrange);
            }
            Curve(imc.Select(p => (p.Seconds, p.Pv)), y, PlotColors.Signal1);
            Curve(simc.Select(p => (p.Seconds, p.Pv)), y, PlotColors.Signal2);
        });

        var commandImc = imc.Where(p => p.Cv.HasValue && p.Mv.HasValue).ToArray();
        var commandSimc = simc.Where(p => p.Cv.HasValue && p.Mv.HasValue).ToArray();
        var secondTop = Top + trackHeight + Gap;
        var thirdTop = secondTop + trackHeight + Gap;

        void Commands(string title, double top, SimulationPoint[] points, Brush command)
        {
            var all = points.SelectMany(p => new[] { p.Cv!.Value, p.Mv!.Value })
                .Concat(new[] { study.Scenario.MinimumMv, study.Scenario.MaximumMv });
            Track($"{title} · ΔCV calculée / ΔMV appliquée ({mvUnit})", top, all, y =>
            {
                Curve(points.Select(p => (p.Seconds, p.Cv!.Value)), y, command, dashed: true);
                Curve(points.Select(p => (p.Seconds, p.Mv!.Value)), y, PlotColors.Signal3);
                foreach (var limit in new[] { study.Scenario.MinimumMv, study.Scenario.MaximumMv })
                {
                    var limitY = y(limit);
                    canvas.Children.Add(new Line
                    {
                        X1 = Left, X2 = Left + width, Y1 = limitY, Y2 = limitY,
                        Stroke = PlotColors.Axis, StrokeThickness = 0.7,
                        StrokeDashArray = new DoubleCollection { 2, 4 },
                        IsHitTestVisible = false
                    });
                }
            });
        }

        Commands("IMC/Lambda", secondTop, commandImc, PlotColors.Signal1);
        Commands("SIMC", thirdTop, commandSimc, PlotColors.Signal2);

        if (study.Scenario.Disturbance != 0)
        {
            var time = study.Scenario.DisturbanceAtSeconds;
            if (time >= t0 && time <= t1)
            {
                var eventLine = new Line
                {
                    X1 = X(time), X2 = X(time), Y1 = Top, Y2 = thirdTop + trackHeight,
                    Stroke = PlotColors.Signal4, StrokeThickness = 1.1,
                    StrokeDashArray = new DoubleCollection { 3, 4 },
                    IsHitTestVisible = false
                };
                canvas.Children.Add(eventLine);
                Label("Perturbation", Math.Min(X(time) + 5, Left + width - 100),
                      thirdTop + trackHeight + 9, PlotColors.Signal4);
            }
        }

        Label($"{t0:G5} s", Left, thirdTop + trackHeight + 9, PlotColors.Label);
        Label($"{t1:G5} s", Math.Max(Left, Left + width - 80),
              thirdTop + trackHeight + 9, PlotColors.Label);
        if (cursorSeconds is { } cursor && cursor >= t0 && cursor <= t1)
        {
            var cursorX = X(cursor);
            canvas.Children.Add(new Line
            {
                X1 = cursorX, X2 = cursorX, Y1 = Top, Y2 = thirdTop + trackHeight,
                Stroke = PlotColors.Axis, StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 2, 3 },
                IsHitTestVisible = false
            });
        }

        Label("Temps depuis le début du scénario (s)", Left,
              thirdTop + trackHeight + 30, PlotColors.Label);
    }
}
