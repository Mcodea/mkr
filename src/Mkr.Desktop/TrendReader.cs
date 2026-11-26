using System.Globalization;
using System.Text;

namespace Mkr.Desktop;

/// <summary>Raw columns of a CSV read for display only: nothing is interpreted, filled or smoothed (the scientific import stays in the engine).</summary>
internal sealed record Trend(string[] Names, double[] X, double[][] Values, bool HasTime, DateTimeOffset? Start, DateTimeOffset? End, int Rows)
{
    public double? MedianStepSeconds
    {
        get
        {
            if (!HasTime || X.Length < 3) return null;
            var steps = new List<double>();
            for (var i = 1; i < X.Length; i++) if (X[i] > X[i - 1]) steps.Add(X[i] - X[i - 1]);
            if (steps.Count == 0) return null;
            steps.Sort();
            return steps[steps.Count / 2];
        }
    }
}

internal static class TrendReader
{
    private const int MaxRows = 200_000;

    /// <summary>Reads the numeric columns of a delimited text. Time comes from <paramref name="timeColumn"/> (or the first column): an ISO date, or a number of seconds.</summary>
    public static Trend? Read(byte[] bytes, char delimiter, char decimalSeparator, string? timeColumn)
    {
        try
        {
            var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
            var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            if (lines.Count < 3) return null;
            var headers = Split(lines[0], delimiter);
            var timeIndex = timeColumn is null ? 0 : Math.Max(0, Array.IndexOf(headers, timeColumn));
            var rows = Math.Min(lines.Count - 1, MaxRows);
            var times = new DateTimeOffset?[rows];
            var numbers = new double[headers.Length][];
            for (var c = 0; c < headers.Length; c++) numbers[c] = new double[rows];
            var timeOk = 0;
            for (var r = 0; r < rows; r++)
            {
                var fields = Split(lines[r + 1], delimiter);
                for (var c = 0; c < headers.Length; c++)
                {
                    var raw = c < fields.Length ? fields[c] : "";
                    if (c == timeIndex)
                    {
                        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) { times[r] = t; timeOk++; }
                        numbers[c][r] = double.NaN;
                        continue;
                    }
                    var normal = decimalSeparator == ',' ? raw.Replace(',', '.') : raw;
                    numbers[c][r] = double.TryParse(normal, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : double.NaN;
                }
            }
            var hasTime = timeOk >= rows * 0.9;
            double[] x;
            DateTimeOffset? start = null, end = null;
            if (hasTime)
            {
                var first = times.First(t => t is not null)!.Value;
                x = times.Select((t, i) => t is null ? double.NaN : (t.Value - first).TotalSeconds).ToArray();
                start = first; end = times.Last(t => t is not null);
            }
            else x = Enumerable.Range(0, rows).Select(i => (double)i).ToArray();
            var names = new List<string>(); var values = new List<double[]>();
            for (var c = 0; c < headers.Length; c++)
            {
                if (c == timeIndex) continue;
                if (numbers[c].Count(v => !double.IsNaN(v)) < rows * 0.5) continue;
                names.Add(headers[c]); values.Add(numbers[c]);
            }
            if (names.Count == 0) return null;
            return new Trend(names.ToArray(), x, values.ToArray(), hasTime, start, end, rows);
        }
        catch (Exception error) when (error is FormatException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    private static string[] Split(string line, char delimiter)
    {
        var parts = new List<string>(); var sb = new StringBuilder(); var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"') quoted = !quoted;
            else if (ch == delimiter && !quoted) { parts.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(ch);
        }
        parts.Add(sb.ToString().Trim());
        return parts.ToArray();
    }
}
