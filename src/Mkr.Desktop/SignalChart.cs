using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>One curve: ascending x (seconds), y in the unit of its track. Colours come from theme tokens (never literal).</summary>
internal sealed class ChartSeries(string name, string colorKey, double[] x, double[] y)
{
    public string Name { get; } = name;
    public string ColorKey { get; } = colorKey;
    public double[] X { get; } = x;
    public double[] Y { get; } = y;
    public bool Visible { get; set; } = true;
}

/// <summary>A shaded min/max envelope between two curves sharing the same x (for example the sensitivity envelope of a simulation).</summary>
internal sealed class ChartBand(string name, string colorKey, double[] x, double[] low, double[] high)
{
    public string Name { get; } = name;
    public string ColorKey { get; } = colorKey;
    public double[] X { get; } = x;
    public double[] Low { get; } = low;
    public double[] High { get; } = high;
}

/// <summary>A band with its own vertical axis. All tracks of a chart share one time axis, zoom and cursor.</summary>
internal sealed class ChartTrack(string title, string unit, IEnumerable<ChartSeries> series)
{
    public string Title { get; } = title;
    public string Unit { get; } = unit;
    public List<ChartSeries> Series { get; } = series.ToList();
    public List<ChartBand> Bands { get; } = new();
    /// <summary>Share of the chart height (default 1; a main signal track can use 2).</summary>
    public double Weight { get; init; } = 1;
    /// <summary>Rotated label of the vertical axis in the reference layout, for example « Pression (bar) ». Defaults to the unit.</summary>
    public string YLabel { get; init; } = "";
    /// <summary>Boxed legend inside the plot (reference layout) even for a single curve.</summary>
    public bool ShowLegend { get; init; }
}

/// <summary>
/// Common scientific chart (in-house, no third-party licence): synchronized tracks on one time axis, wheel zoom around the pointer, pan
/// (right-drag, or left-drag when range selection is off), range selection, synchronized cursor with readout, interactive legend (click to
/// hide a curve; curves also differ by dash pattern, never by colour alone), min/max decimation of big histories, PNG and CSV export.
/// Keyboard: arrows pan, + / - zoom, Home resets, so the chart is usable without a mouse.
/// </summary>
internal sealed class SignalChart : FrameworkElement
{
    private const double RightMargin = 14, TopMargin = 6, TrackGap = 30;
    // The reference layout (see docs/ui) puts the title above the plot, a rotated axis label on the left and a boxed legend inside the plot.
    private double LeftMargin => ReferenceStyle ? 70 : 74;
    private double BottomMargin => ReferenceStyle ? 38 : 40;
    private double LegendHeight => ReferenceStyle ? 22 : 22;
    private static readonly DashStyle[] Dashes = { DashStyles.Solid, new DashStyle(new double[] { 6, 3 }, 0), new DashStyle(new double[] { 1.5, 3 }, 0), new DashStyle(new double[] { 6, 3, 1.5, 3 }, 0) };

    private IReadOnlyList<ChartTrack> _tracks = Array.Empty<ChartTrack>();
    private double _fullLo, _fullHi, _lo, _hi;
    private double? _cursor, _highlightFrom, _highlightTo;
    private Point? _panOrigin; private (double Lo, double Hi) _panView;
    private double? _selectOrigin, _selectCurrent;
    private readonly List<(Rect Box, ChartSeries Series)> _legend = new();
    private IReadOnlyList<(double X, string Label)> _events = Array.Empty<(double, string)>();

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new SignalChartPeer(this);

    /// <summary>Makes the chart visible to assistive technology and to UI tests: a named graphic whose item status carries the time window and the cursor.</summary>
    private sealed class SignalChartPeer(SignalChart chart) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(chart)
    {
        protected override string GetClassNameCore() => nameof(SignalChart);
        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => System.Windows.Automation.Peers.AutomationControlType.Image;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public SignalChart()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        ThemeService.Changed += () => InvalidateVisual();
    }

    public string XLabel { get; set; } = Loc.T("cs.temps_s");
    public bool SelectionEnabled { get; set; }
    /// <summary>Reference layout: centred title, rotated axis label, boxed legend inside the plot, lettered range markers.</summary>
    public bool ReferenceStyle { get; set; }
    /// <summary>Per-track value boxes next to the cursor. Off where a compact readout elsewhere on screen already gives the values (no stacked boxes).</summary>
    public bool ShowValueBoxes { get; set; } = true;
    public bool HasData => _tracks.Any(t => t.Series.Any(s => s.X.Length > 1));
    public (double From, double To) View => (_lo, _hi);

    /// <summary>Raised when the user drags a range (time axis units).</summary>
    public event Action<double, double>? RangeSelected;
    /// <summary>Raised with a readable description of the sample under the cursor ("" when the pointer leaves).</summary>
    public event Action<string>? CursorTextChanged;
    public event Action? ViewChanged;

    public void SetTracks(IEnumerable<ChartTrack> tracks)
    {
        _tracks = tracks.ToList();
        var all = _tracks.SelectMany(t => t.Series).Where(s => s.X.Length > 1).ToList();
        if (all.Count == 0) { _fullLo = _fullHi = _lo = _hi = 0; InvalidateVisual(); return; }
        _fullLo = all.Min(s => s.X[0]);
        _fullHi = all.Max(s => s.X[^1]);
        if (_fullHi <= _fullLo) _fullHi = _fullLo + 1;
        _lo = _fullLo; _hi = _fullHi;
        InvalidateVisual();
    }

    /// <summary>Vertical markers on every track (for example the instant a disturbance is applied).</summary>
    public void SetEvents(IEnumerable<(double X, string Label)> events) { _events = events.ToList(); InvalidateVisual(); }

    public void SetHighlight(double? from, double? to) { _highlightFrom = from; _highlightTo = to; InvalidateVisual(); }

    /// <summary>Sets the displayed time window without raising <see cref="ViewChanged"/> (used to keep several charts on the same time axis).</summary>
    public void SetView(double from, double to)
    {
        if (!HasData || !(to > from)) return;
        _lo = Math.Max(_fullLo, from); _hi = Math.Min(_fullHi, to);
        if (!(_hi > _lo)) { _lo = _fullLo; _hi = _fullHi; }
        InvalidateVisual();
    }

    public void ZoomBy(double factor)
    {
        if (!HasData) return;
        (_lo, _hi) = ChartMath.Zoom(_lo, _hi, (_lo + _hi) / 2, factor, _fullLo, _fullHi);
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Mirrors the cursor of another chart (time axis units) without raising <see cref="CursorMoved"/>.</summary>
    public void SetExternalCursor(double? x) { _cursor = x; InvalidateVisual(); }

    public event Action<double?>? CursorMoved;

    public void ResetView() { _lo = _fullLo; _hi = _fullHi; InvalidateVisual(); ViewChanged?.Invoke(); }

    // ---------- geometry ----------
    private double PlotWidth => Math.Max(1, ActualWidth - LeftMargin - RightMargin);
    // Tracks share the height by weight (the main signal can get twice the room of a command track).
    private double TrackHeightAt(int index)
    {
        var total = _tracks.Sum(t => t.Weight);
        var room = ActualHeight - TopMargin - BottomMargin - TrackGap * _tracks.Count;
        return Math.Max(40, room * _tracks[index].Weight / total);
    }

    private double TrackTop(int index)
    {
        var top = TopMargin + LegendHeight;
        for (var i = 0; i < index; i++) top += TrackHeightAt(i) + TrackGap;
        return top;
    }
    private double XToPixel(double x) => LeftMargin + (x - _lo) / (_hi - _lo) * PlotWidth;
    private double PixelToX(double px) => _lo + (px - LeftMargin) / PlotWidth * (_hi - _lo);

    private (double Min, double Max) YRange(ChartTrack track)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var s in track.Series.Where(s => s.Visible))
        {
            var from = Math.Max(0, ChartMath.LowerBound(s.X, _lo) - 1);
            var to = Math.Min(s.X.Length - 1, ChartMath.LowerBound(s.X, _hi));
            for (var i = from; i <= to; i++)
                if (double.IsFinite(s.Y[i])) { min = Math.Min(min, s.Y[i]); max = Math.Max(max, s.Y[i]); }
        }
        foreach (var band in track.Bands)
        {
            var from = Math.Max(0, ChartMath.LowerBound(band.X, _lo) - 1);
            var to = Math.Min(band.X.Length - 1, ChartMath.LowerBound(band.X, _hi));
            for (var i = from; i <= to; i++)
            {
                if (double.IsFinite(band.Low[i])) min = Math.Min(min, band.Low[i]);
                if (double.IsFinite(band.High[i])) max = Math.Max(max, band.High[i]);
            }
        }
        if (!double.IsFinite(min)) return (0, 1);
        var pad = max > min ? (max - min) * 0.08 : Math.Max(0.1, Math.Abs(min) * 0.1);
        return (min - pad, max + pad);
    }

    /// <summary>Exposes the current time window and cursor to assistive technology and to the UI tests (« vue a–b s ; curseur t s »).</summary>
    private void PublishState()
    {
        var text = $"vue {_lo.ToString("G6", CultureInfo.InvariantCulture)}–{_hi.ToString("G6", CultureInfo.InvariantCulture)} s" +
                   (_cursor is { } c ? $" ; curseur {c.ToString("G6", CultureInfo.InvariantCulture)} s" : "");
        System.Windows.Automation.AutomationProperties.SetItemStatus(this, text);
    }

    private static Brush Faded(Brush brush, double opacity) { var copy = brush.Clone(); copy.Opacity = opacity; return copy; }

    private static Brush Theme(string key) => (Brush)Application.Current.FindResource(key);

    private FormattedText Text(string text, double size, Brush brush, bool bold = false) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height);

    // ---------- drawing ----------
    protected override void OnRender(DrawingContext dc)
    {
        PublishState();
        _legend.Clear();
        dc.DrawRectangle(Theme("MkrSurface"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (!HasData || ActualWidth < 200 || ActualHeight < (ReferenceStyle ? 96 : 160))
        {
            var empty = Text(HasData ? Loc.T("cs.zone_trop_petite_pour_tracer") : Loc.T("cs.aucune_donnee_a_afficher"), 13, Theme("MkrPlotLabel"));
            dc.DrawText(empty, new Point(LeftMargin, TopMargin + 8));
            return;
        }
        var axis = new Pen(Theme("MkrPlotAxis"), 1);
        var grid = new Pen(Theme("MkrPlotGrid"), 1);
        var labelBrush = Theme("MkrPlotLabel");
        var buckets = Math.Max(50, (int)PlotWidth);
        var (xTicks, _) = ChartMath.NiceTicks(_lo, _hi, Math.Max(ReferenceStyle ? 3 : 2, (int)(PlotWidth / (ReferenceStyle ? 64 : 90))));

        for (var t = 0; t < _tracks.Count; t++)
        {
            var track = _tracks[t];
            var top = TrackTop(t);
            var height = TrackHeightAt(t);
            var (ymin, ymax) = YRange(track);
            double Y(double v) => top + (ymax - v) / (ymax - ymin) * height;

            if (ReferenceStyle)
            {
                var refTitle = Text(track.Title, 13, Theme("MkrText"), true);
                dc.DrawText(refTitle, new Point(LeftMargin + PlotWidth / 2 - refTitle.Width / 2, top - LegendHeight + 2));
            }
            else
            {
            // legend: swatch with the curve's dash pattern + name; click toggles the curve
            var legendX = LeftMargin;
            var title = Text($"{track.Title}{(track.Unit.Length > 0 ? $" ({track.Unit})" : "")}", 13, Theme("MkrText"), true);
            dc.DrawText(title, new Point(legendX, top - LegendHeight));
            legendX += title.Width + 18;
            for (var si = 0; si < track.Series.Count; si++)
            {
                var series = track.Series[si];
                var color = Theme(series.ColorKey);
                var label = Text(series.Name, 13, series.Visible ? Theme("MkrText") : Theme("MkrPlotLabel"));
                var box = new Rect(legendX - 4, top - LegendHeight - 1, 44 + label.Width, LegendHeight);
                dc.DrawRectangle(Brushes.Transparent, null, box);
                dc.DrawLine(new Pen(series.Visible ? color : Faded(color, 0.35), 2) { DashStyle = Dashes[si % Dashes.Length] }, new Point(legendX, top - LegendHeight / 2 - 1), new Point(legendX + 30, top - LegendHeight / 2 - 1));
                dc.DrawText(label, new Point(legendX + 36, top - LegendHeight));
                if (!series.Visible) dc.DrawLine(new Pen(labelBrush, 1), new Point(legendX + 36, top - LegendHeight / 2 - 1), new Point(legendX + 36 + label.Width, top - LegendHeight / 2 - 1));
                _legend.Add((box, series));
                legendX += 44 + label.Width + 8;
            }
            }

            // grid + y labels
            var (yTicks, _) = ChartMath.NiceTicks(ymin, ymax, Math.Max(ReferenceStyle ? 3 : 2, (int)(height / (ReferenceStyle ? 28 : 38))));
            foreach (var v in yTicks)
            {
                var py = Y(v);
                dc.DrawLine(grid, new Point(LeftMargin, py), new Point(LeftMargin + PlotWidth, py));
                var l = Text(v.ToString("G5", CultureInfo.CurrentCulture), 12, labelBrush);
                dc.DrawText(l, new Point(LeftMargin - 6 - l.Width, py - l.Height / 2));
            }
            foreach (var v in xTicks)
                dc.DrawLine(grid, new Point(XToPixel(v), top), new Point(XToPixel(v), top + height));
            dc.DrawRectangle(null, axis, new Rect(LeftMargin, top, PlotWidth, height));

            // highlight of a chosen range
            if (_highlightFrom is { } hf && _highlightTo is { } ht && ht > hf)
            {
                var x0 = Math.Max(LeftMargin, XToPixel(hf));
                var x1 = Math.Min(LeftMargin + PlotWidth, XToPixel(ht));
                if (x1 > x0) dc.DrawRectangle(new SolidColorBrush(((SolidColorBrush)Theme("MkrAccent")).Color) { Opacity = ReferenceStyle ? 0.10 : 0.16 }, null, new Rect(x0, top, x1 - x0, height));
            }

            // curves (clipped, decimated)
            dc.PushClip(new RectangleGeometry(new Rect(LeftMargin, top, PlotWidth, height)));
            foreach (var band in track.Bands)
            {
                var from = Math.Max(0, ChartMath.LowerBound(band.X, _lo) - 1);
                var to = Math.Min(band.X.Length - 1, ChartMath.LowerBound(band.X, _hi));
                if (to - from < 1) continue;
                var shape = new StreamGeometry();
                using (var context = shape.Open())
                {
                    context.BeginFigure(new Point(XToPixel(band.X[from]), Y(band.High[from])), true, true);
                    for (var i = from + 1; i <= to; i++) context.LineTo(new Point(XToPixel(band.X[i]), Y(band.High[i])), true, false);
                    for (var i = to; i >= from; i--) context.LineTo(new Point(XToPixel(band.X[i]), Y(band.Low[i])), true, false);
                }
                shape.Freeze();
                dc.DrawGeometry(Faded(Theme(band.ColorKey), 0.16), null, shape);
            }
            for (var si = 0; si < track.Series.Count; si++)
            {
                var series = track.Series[si];
                if (!series.Visible) continue;
                var pen = new Pen(Theme(series.ColorKey), 2.4) { DashStyle = Dashes[si % Dashes.Length], LineJoin = PenLineJoin.Round };
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    var started = false;
                    foreach (var i in ChartMath.Decimate(series.X, series.Y, _lo, _hi, buckets))
                    {
                        if (!double.IsFinite(series.Y[i])) { started = false; continue; }   // a gap is a gap, never bridged
                        var p = new Point(XToPixel(series.X[i]), Y(series.Y[i]));
                        if (!started) { context.BeginFigure(p, false, false); started = true; } else context.LineTo(p, true, false);
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
            dc.Pop();
            if (ReferenceStyle)
            {
                var yText = track.YLabel.Length > 0 ? track.YLabel : (track.Unit.Length > 0 ? track.Unit : track.Title);
                var yl = Text(yText, 12, labelBrush);
                dc.PushTransform(new RotateTransform(-90, 12, top + height / 2));
                dc.DrawText(yl, new Point(12 - yl.Width / 2, top + height / 2 - yl.Height / 2));
                dc.Pop();
                if (track.Series.Count > 1 || track.ShowLegend)
                {
                    var rows = track.Series.Select(si => Text(si.Name, 12, si.Visible ? Theme("MkrText") : Theme("MkrPlotLabel"))).ToList();
                    var boxWidth = 20 + 34 + rows.Max(r => r.Width) + 12;
                    var rowHeight = 18.0;
                    var boxHeight = rows.Count * rowHeight + 10;
                    if (boxHeight < height - 8 && boxWidth < PlotWidth - 8)
                    {
                        var legendBox = new Rect(LeftMargin + PlotWidth - boxWidth - 8, top + height - boxHeight - 8, boxWidth, boxHeight);
                        dc.DrawRectangle(Faded(Theme("MkrSurface"), 0.94), new Pen(Theme("MkrBorderStrong"), 1), legendBox);
                        for (var si = 0; si < track.Series.Count; si++)
                        {
                            var series = track.Series[si];
                            var color = Theme(series.ColorKey);
                            var ly = legendBox.Y + 5 + si * rowHeight;
                            dc.DrawLine(new Pen(series.Visible ? color : Faded(color, 0.35), 2.4) { DashStyle = Dashes[si % Dashes.Length] }, new Point(legendBox.X + 10, ly + rowHeight / 2), new Point(legendBox.X + 40, ly + rowHeight / 2));
                            dc.DrawText(rows[si], new Point(legendBox.X + 48, ly + (rowHeight - rows[si].Height) / 2));
                            _legend.Add((new Rect(legendBox.X, ly, boxWidth, rowHeight), series));
                        }
                    }
                }
            }
        }

        // shared time axis
        var bottom = TrackTop(_tracks.Count - 1) + TrackHeightAt(_tracks.Count - 1);
        foreach (var (ex, label) in _events)
        {
            if (ex < _lo || ex > _hi) continue;
            var px = XToPixel(ex);
            if (ReferenceStyle)
            {
                var accent = Theme("MkrAccent");
                dc.DrawLine(new Pen(accent, 1.2) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }, new Point(px, TrackTop(0)), new Point(px, bottom));
                var letter = Text(label, 12, Theme("MkrOnAccent"), true);
                var square = new Rect(px - 9, TrackTop(0) + 2, 18, 18);
                dc.DrawRectangle(accent, null, square);
                dc.DrawText(letter, new Point(square.X + (18 - letter.Width) / 2, square.Y + (18 - letter.Height) / 2));
                continue;
            }
            dc.DrawLine(new Pen(Theme("MkrSignal4"), 1.4) { DashStyle = new DashStyle(new double[] { 1.5, 3 }, 0) }, new Point(px, TrackTop(0)), new Point(px, bottom));
            var text = Text(label, 12, Theme("MkrSignal4"), true);
            dc.DrawText(text, new Point(Math.Min(px + 4, LeftMargin + PlotWidth - text.Width), TrackTop(0) + 3));
        }
        foreach (var v in xTicks)
        {
            var l = Text(v.ToString("G6", CultureInfo.CurrentCulture), 12, labelBrush);
            dc.DrawText(l, new Point(XToPixel(v) - l.Width / 2, bottom + 4));
        }
        var xl = Text(XLabel, 12, labelBrush);
        dc.DrawText(xl, new Point(LeftMargin + PlotWidth / 2 - xl.Width / 2, bottom + (ReferenceStyle ? 18 : 22)));

        // range being selected
        if (_selectOrigin is { } so && _selectCurrent is { } sc)
        {
            var x0 = XToPixel(Math.Min(so, sc)); var x1 = XToPixel(Math.Max(so, sc));
            dc.DrawRectangle(new SolidColorBrush(((SolidColorBrush)Theme("MkrAccent")).Color) { Opacity = 0.25 }, new Pen(Theme("MkrAccent"), 1), new Rect(x0, TrackTop(0), x1 - x0, bottom - TrackTop(0)));
        }

        // synchronized cursor and readout
        if (_cursor is { } c && c >= _lo && c <= _hi)
        {
            var px = XToPixel(c);
            dc.DrawLine(new Pen(Theme("MkrText"), 1) { DashStyle = DashStyles.Dash }, new Point(px, TrackTop(0)), new Point(px, bottom));
            for (var t = 0; t < _tracks.Count; t++)
            {
                var lines = new List<string>();
                foreach (var s in _tracks[t].Series.Where(s => s.Visible && s.X.Length > 0))
                {
                    var i = ChartMath.NearestIndex(s.X, c);
                    if (i >= 0) lines.Add($"{s.Name} : {s.Y[i].ToString("G6", CultureInfo.CurrentCulture)} {_tracks[t].Unit}".TrimEnd());
                }
                if (lines.Count == 0 || !ShowValueBoxes) continue;
                var text = Text($"t = {c.ToString("G6", CultureInfo.CurrentCulture)} s\n" + string.Join("\n", lines), 11, Theme("MkrText"));
                var left = px + 10 + text.Width + 12 > LeftMargin + PlotWidth ? px - 10 - text.Width - 12 : px + 10;
                var box = new Rect(left, TrackTop(t) + 4, text.Width + 12, text.Height + 8);
                dc.DrawRectangle(Theme("MkrSurfaceAlt"), new Pen(Theme("MkrBorderStrong"), 1), box);
                dc.DrawText(text, new Point(box.X + 6, box.Y + 4));
            }
        }
    }

    // ---------- interaction ----------
    private string DescribeCursor()
    {
        if (_cursor is not { } c || !HasData) return "";
        var parts = new List<string> { $"t = {c.ToString("G6", CultureInfo.CurrentCulture)} s" };
        foreach (var track in _tracks)
            foreach (var s in track.Series.Where(s => s.Visible && s.X.Length > 0))
            {
                var i = ChartMath.NearestIndex(s.X, c);
                if (i >= 0) parts.Add($"{s.Name} {s.Y[i].ToString("G6", CultureInfo.CurrentCulture)} {track.Unit}".TrimEnd());
            }
        return string.Join(" · ", parts);
    }

    private bool InPlot(Point p) => p.X >= LeftMargin && p.X <= LeftMargin + PlotWidth && p.Y >= TopMargin && p.Y <= ActualHeight - BottomMargin + 8;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        if (_panOrigin is { } origin)
        {
            var dx = -(p.X - origin.X) / PlotWidth * (_panView.Hi - _panView.Lo);
            (_lo, _hi) = ChartMath.Pan(_panView.Lo, _panView.Hi, dx, _fullLo, _fullHi);
            ViewChanged?.Invoke();
        }
        if (_selectOrigin is not null) _selectCurrent = Math.Clamp(PixelToX(p.X), _fullLo, _fullHi);
        _cursor = InPlot(p) && HasData ? PixelToX(p.X) : null;
        CursorTextChanged?.Invoke(DescribeCursor());
        CursorMoved?.Invoke(_cursor);
        Cursor = _legend.Any(l => l.Box.Contains(p)) ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_panOrigin is not null || _selectOrigin is not null) return;
        _cursor = null;
        CursorTextChanged?.Invoke("");
        CursorMoved?.Invoke(null);
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var p = e.GetPosition(this);
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) { ResetView(); return; }
        if (e.ChangedButton == MouseButton.Left && _legend.FirstOrDefault(l => l.Box.Contains(p)) is { Series: not null } hit)
        {
            hit.Series.Visible = !hit.Series.Visible;
            InvalidateVisual();
            return;
        }
        if (!InPlot(p) || !HasData) return;
        if (e.ChangedButton == MouseButton.Left && SelectionEnabled) { _selectOrigin = _selectCurrent = Math.Clamp(PixelToX(p.X), _fullLo, _fullHi); CaptureMouse(); }
        else if (e.ChangedButton is MouseButton.Right or MouseButton.Left or MouseButton.Middle) { _panOrigin = p; _panView = (_lo, _hi); CaptureMouse(); }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_selectOrigin is { } so && _selectCurrent is { } sc)
        {
            var from = Math.Min(so, sc); var to = Math.Max(so, sc);
            _selectOrigin = _selectCurrent = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            if (to - from > (_hi - _lo) / PlotWidth * 3) RangeSelected?.Invoke(from, to);   // ignore accidental clicks (< 3 px)
        }
        if (_panOrigin is not null) { _panOrigin = null; ReleaseMouseCapture(); }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!HasData) return;
        var center = PixelToX(Math.Clamp(e.GetPosition(this).X, LeftMargin, LeftMargin + PlotWidth));
        (_lo, _hi) = ChartMath.Zoom(_lo, _hi, center, e.Delta > 0 ? 0.8 : 1.25, _fullLo, _fullHi);
        ViewChanged?.Invoke();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HasData) return;
        var step = (_hi - _lo) * 0.1;
        switch (e.Key)
        {
            case Key.Left: (_lo, _hi) = ChartMath.Pan(_lo, _hi, -step, _fullLo, _fullHi); break;
            case Key.Right: (_lo, _hi) = ChartMath.Pan(_lo, _hi, step, _fullLo, _fullHi); break;
            case Key.Add or Key.OemPlus: (_lo, _hi) = ChartMath.Zoom(_lo, _hi, (_lo + _hi) / 2, 0.8, _fullLo, _fullHi); break;
            case Key.Subtract or Key.OemMinus: (_lo, _hi) = ChartMath.Zoom(_lo, _hi, (_lo + _hi) / 2, 1.25, _fullLo, _fullHi); break;
            case Key.Home: _lo = _fullLo; _hi = _fullHi; break;
            default: return;
        }
        e.Handled = true;
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    // ---------- export ----------
    public void ExportPng(string path)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>One CSV for several charts that share a time axis (long format, same columns as <see cref="ExportCsv"/>).</summary>
    public static void ExportCsv(string path, params SignalChart[] charts)
    {
        var text = new StringBuilder("piste;courbe;unite;temps_s;valeur\n");
        foreach (var chart in charts)
            foreach (var track in chart._tracks)
                foreach (var s in track.Series)
                    for (var i = 0; i < s.X.Length; i++)
                        text.Append(track.Title).Append(';').Append(s.Name).Append(';').Append(track.Unit).Append(';')
                            .Append(s.X[i].ToString("R", CultureInfo.InvariantCulture)).Append(';').Append(s.Y[i].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
    }

    /// <summary>All samples of every series (not only the visible window), long format with units, invariant culture.</summary>
    public void ExportCsv(string path)
    {
        var text = new StringBuilder("piste;courbe;unite;temps_s;valeur\n");
        foreach (var track in _tracks)
            foreach (var s in track.Series)
                for (var i = 0; i < s.X.Length; i++)
                    text.Append(track.Title).Append(';').Append(s.Name).Append(';').Append(track.Unit).Append(';')
                        .Append(s.X[i].ToString("R", CultureInfo.InvariantCulture)).Append(';').Append(s.Y[i].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
    }
}
