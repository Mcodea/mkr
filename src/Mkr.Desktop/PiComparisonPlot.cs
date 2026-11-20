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
    private const double Left = 90;
    private const double Right = 25;
    private const double Top = 28;
    private const double Gap = 44;

    public static void Draw(Canvas canvas, PiSimulationStudy? study, string pvUnit, string mvUnit)
    {
        canvas.Children.Clear();
        if (study is null || canvas.ActualWidth < 260 || canvas.ActualHeight < 400)
            return;

        var imc = study.Result.Imc.Points;
        var simc = study.Result.Simc.Points;
        if (imc.Length < 3 || simc.Length != imc.Length)
            return;

        var width = canvas.ActualWidth - Left - Right;
        var trackHeight = Math.Max(80.0, (canvas.ActualHeight - Top - 2 * Gap - 55) / 3);
        var t0 = imc[0].Seconds;
        var t1 = imc[^1].Seconds;
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
                Stroke = Brushes.SlateGray,
                StrokeThickness = 1,
                IsHitTestVisible = false,
                Points = new PointCollection
                {
                    new(Left, top), new(Left, top + trackHeight),
                    new(Left + width, top + trackHeight)
                }
            });
            Label(title, Left, top - 21, Brushes.DimGray);
            Label(maximum.ToString("G5", CultureInfo.InvariantCulture), 2, top - 8, Brushes.DimGray);
            Label(minimum.ToString("G5", CultureInfo.InvariantCulture), 2,
                  top + trackHeight - 15, Brushes.DimGray);
            draw(Y);
        }

        var pvValues = imc.Select(p => p.Pv).Concat(simc.Select(p => p.Pv))
            .Append(study.Scenario.Setpoint);
        Track($"ΔPV et ΔSP ({pvUnit})", Top, pvValues, y =>
        {
            var reference = y(study.Scenario.Setpoint);
            canvas.Children.Add(new Line
            {
                X1 = Left, X2 = Left + width, Y1 = reference, Y2 = reference,
                Stroke = Brushes.SeaGreen, StrokeThickness = 1.4,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                IsHitTestVisible = false
            });
            Curve(imc.Select(p => (p.Seconds, p.Pv)), y, Brushes.SteelBlue);
            Curve(simc.Select(p => (p.Seconds, p.Pv)), y, Brushes.DarkOrange);
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
                Curve(points.Select(p => (p.Seconds, p.Mv!.Value)), y, Brushes.MediumSeaGreen);
                foreach (var limit in new[] { study.Scenario.MinimumMv, study.Scenario.MaximumMv })
                {
                    var limitY = y(limit);
                    canvas.Children.Add(new Line
                    {
                        X1 = Left, X2 = Left + width, Y1 = limitY, Y2 = limitY,
                        Stroke = Brushes.Gray, StrokeThickness = 0.7,
                        StrokeDashArray = new DoubleCollection { 2, 4 },
                        IsHitTestVisible = false
                    });
                }
            });
        }

        Commands("IMC/Lambda", secondTop, commandImc, Brushes.SteelBlue);
        Commands("SIMC", thirdTop, commandSimc, Brushes.DarkOrange);

        if (study.Scenario.Disturbance != 0)
        {
            var time = study.Scenario.DisturbanceAtSeconds;
            if (time >= t0 && time <= t1)
            {
                var eventLine = new Line
                {
                    X1 = X(time), X2 = X(time), Y1 = Top, Y2 = thirdTop + trackHeight,
                    Stroke = Brushes.MediumVioletRed, StrokeThickness = 1.1,
                    StrokeDashArray = new DoubleCollection { 3, 4 },
                    IsHitTestVisible = false
                };
                canvas.Children.Add(eventLine);
                Label("Perturbation", Math.Min(X(time) + 5, Left + width - 100),
                      thirdTop + trackHeight + 9, Brushes.MediumVioletRed);
            }
        }

        Label($"{t0:G5} s", Left, thirdTop + trackHeight + 9, Brushes.DimGray);
        Label($"{t1:G5} s", Math.Max(Left, Left + width - 80),
              thirdTop + trackHeight + 9, Brushes.DimGray);
        Label("Temps depuis le début du scénario (s)", Left,
              thirdTop + trackHeight + 30, Brushes.DimGray);
    }
}
