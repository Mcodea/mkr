namespace Mkr.Core;

/// <summary>Pure numeric helpers of the common signal chart (no UI types, so they are unit-tested on any platform).</summary>
public static class ChartMath
{
    /// <summary>Round tick values covering [min, max] with a step of 1, 2 or 5 times a power of ten.</summary>
    public static (double[] Ticks, double Step) NiceTicks(double min, double max, int target)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || target < 1) return (Array.Empty<double>(), 0);
        if (max < min) (min, max) = (max, min);
        var range = max - min;
        if (range <= 0) return (new[] { min }, 0);
        var raw = range / target;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        var first = Math.Ceiling(min / step - 1e-9) * step;
        var ticks = new List<double>();
        for (var tick = first; tick <= max + step * 1e-9 && ticks.Count < 1000; tick += step)
            ticks.Add(Math.Abs(tick) < step * 1e-9 ? 0 : tick);
        return (ticks.ToArray(), step);
    }

    /// <summary>Index of the first sample whose x is not less than <paramref name="value"/> (x must be ascending).</summary>
    public static int LowerBound(double[] x, double value)
    {
        int lo = 0, hi = x.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (x[mid] < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>Index of the sample closest to <paramref name="value"/>, or -1 for an empty series.</summary>
    public static int NearestIndex(double[] x, double value)
    {
        if (x.Length == 0) return -1;
        var i = LowerBound(x, value);
        if (i == 0) return 0;
        if (i == x.Length) return x.Length - 1;
        return value - x[i - 1] <= x[i] - value ? i - 1 : i;
    }

    /// <summary>
    /// Indices to draw for the window [x0, x1]: when more samples than <paramref name="buckets"/> × 2 fall in the window, each bucket keeps its first,
    /// minimum, maximum and last sample, so extrema and spikes are never lost. Otherwise every sample of the window (plus its neighbours) is kept.
    /// </summary>
    public static int[] Decimate(double[] x, double[] y, double x0, double x1, int buckets)
    {
        if (x.Length != y.Length) throw new ArgumentException("x and y must have the same length.");
        if (x.Length == 0 || buckets < 1) return Array.Empty<int>();
        var start = Math.Max(0, LowerBound(x, x0) - 1);
        var end = Math.Min(x.Length - 1, LowerBound(x, x1));
        if (end < start) return Array.Empty<int>();
        var count = end - start + 1;
        if (count <= buckets * 2) return Enumerable.Range(start, count).ToArray();
        var picked = new SortedSet<int>();
        for (var b = 0; b < buckets; b++)
        {
            var from = start + (int)((long)count * b / buckets);
            var to = start + (int)((long)count * (b + 1) / buckets) - 1;
            if (to < from) continue;
            int min = from, max = from;
            for (var i = from; i <= to; i++)
            {
                if (!double.IsFinite(y[i])) continue;
                if (!double.IsFinite(y[min]) || y[i] < y[min]) min = i;
                if (!double.IsFinite(y[max]) || y[i] > y[max]) max = i;
            }
            picked.Add(from); picked.Add(min); picked.Add(max); picked.Add(to);
        }
        return picked.ToArray();
    }

    /// <summary>Zooms the view [lo, hi] by <paramref name="factor"/> (&lt; 1 zooms in) around <paramref name="center"/>, clamped to [fullLo, fullHi].</summary>
    public static (double Lo, double Hi) Zoom(double lo, double hi, double center, double factor, double fullLo, double fullHi)
    {
        var span = hi - lo;
        var minimum = (fullHi - fullLo) * 1e-6;
        var newSpan = Math.Clamp(span * factor, minimum, fullHi - fullLo);
        var ratio = span > 0 ? (center - lo) / span : 0.5;
        return Pan(center - ratio * newSpan, center - ratio * newSpan + newSpan, 0, fullLo, fullHi);
    }

    /// <summary>Shifts the view by <paramref name="delta"/> without leaving [fullLo, fullHi]; the span is preserved.</summary>
    public static (double Lo, double Hi) Pan(double lo, double hi, double delta, double fullLo, double fullHi)
    {
        var span = hi - lo;
        // the whole range is shown: return the exact bounds (no floating-point residue such as 5.7E-14 at the left edge)
        if (span >= (fullHi - fullLo) * (1 - 1e-9)) return (fullLo, fullHi);
        var newLo = lo + delta;
        if (newLo < fullLo) newLo = fullLo;
        if (newLo + span > fullHi) newLo = fullHi - span;
        if (newLo < fullLo) newLo = fullLo;
        return (newLo, newLo + span);
    }
}
