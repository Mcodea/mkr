using System.Diagnostics;
using Mkr.Core;

namespace Mkr.Infrastructure.Plc;

/// <summary>
/// Scheduled, read-only acquisition with explicit gaps.
/// Readings are scheduled every PeriodMs on a monotonic clock. A reading that cannot be taken
/// (timeout, lost connection, non-finite value, overrun) is a missed sample recorded in a gap,
/// never interpolated. A lost connection is reopened at the next scheduled reading until the
/// tolerated outage is exceeded. Configuration refusals (address, access) stop the capture.
/// </summary>
public sealed class PlcAcquisition(Func<AcquisitionTarget, IPlcReader>? factory = null,
    long maximumSamples = PlcDecoding.MaximumOneShotSamples) : IReadOnlyAcquisition
{
    private readonly Func<AcquisitionTarget, IPlcReader> _factory = factory ?? PlcReaders.Create;

    public async Task<AcquisitionResult> CaptureAsync(AcquisitionTarget target, AcquisitionPlan plan,
        Action<AcquiredSample> received, IProgress<AcquisitionProgress>? progress, CancellationToken cancellationToken,
        Action<AcquisitionGap>? gapClosed = null)
    {
        if (PlcDecoding.Validate(target, plan, maximumSamples) is { } invalid) throw new ArgumentException(invalid);
        var clock = Stopwatch.StartNew();
        var startUtc = DateTimeOffset.UtcNow;
        var period = TimeSpan.FromMilliseconds(plan.PeriodMs);
        var gaps = new List<AcquisitionGap>();
        var roundTrips = new List<double>();
        int receivedCount = 0, missed = 0, reconnections = 0, notManual = 0;
        double maximumLateness = 0;
        (DateTimeOffset From, string Reason, int Count, TimeSpan Since)? gap = null;
        bool everConnected = false;
        string? lastError = null;
        double? lastRoundTrip = null;
        string reason = "sample_limit";
        IPlcReader? reader = null;

        void Report(AcquisitionState state, int scheduled) => progress?.Report(new(state, receivedCount, plan.Samples, missed,
            clock.Elapsed, TimeSpan.FromMilliseconds(Math.Max(0, plan.Samples - scheduled) * (double)plan.PeriodMs),
            lastRoundTrip, lastError));

        void Miss(int index, string why)
        {
            missed++;
            gap ??= (startUtc + index * period, why, 0, clock.Elapsed);
            gap = gap.Value with { Count = gap.Value.Count + 1 };
        }

        void CloseGap()
        {
            if (gap is { } open)
            {
                var closed = new AcquisitionGap(open.From, startUtc + clock.Elapsed, open.Reason, open.Count);
                gaps.Add(closed);
                gapClosed?.Invoke(closed);
            }
            gap = null;
        }

        try
        {
            Report(AcquisitionState.Connecting, 0);
            for (int index = 0; index < plan.Samples; index++)
            {
                var due = index * period;
                var wait = due - clock.Elapsed;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var lateness = clock.Elapsed - due;
                if (lateness >= period)
                {
                    // The previous exchange overran this slot: record it as missed instead of reading late.
                    Miss(index, "overrun");
                    continue;
                }
                maximumLateness = Math.Max(maximumLateness, lateness.TotalMilliseconds);
                try
                {
                    if (reader is null)
                    {
                        if (everConnected) Report(AcquisitionState.Reconnecting, index);
                        var candidate = _factory(target);
                        try { await candidate.ConnectAsync(cancellationToken).ConfigureAwait(false); }
                        catch { await candidate.DisposeAsync().ConfigureAwait(false); throw; }
                        reader = candidate;
                        if (everConnected) reconnections++;
                        everConnected = true;
                        Report(AcquisitionState.Connected, index);
                    }
                    var sentUtc = DateTimeOffset.UtcNow;
                    var sent = clock.Elapsed;
                    var reading = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    var roundTrip = clock.Elapsed - sent;
                    CloseGap();
                    lastRoundTrip = roundTrip.TotalMilliseconds;
                    roundTrips.Add(lastRoundTrip.Value);
                    receivedCount++;
                    bool? inManual = reading.Mode is { } rawMode ? rawMode == target.ManualModeValue : null;
                    if (inManual == false) notManual++;
                    received(new AcquiredSample(sentUtc + roundTrip / 2,
                        PlcDecoding.Scale(reading.Sp, target.Sp), PlcDecoding.Scale(reading.Pv, target.Pv), PlcDecoding.Scale(reading.Mv, target.Mv),
                        lastRoundTrip.Value, reading.Sp, reading.Pv, reading.Mv, reading.Mode, inManual));
                    lastError = null;
                    Report(AcquisitionState.Reading, index + 1);
                }
                catch (PlcReadException error) when (error.Recoverable)
                {
                    lastError = error.Message;
                    Miss(index, error.Code);
                    if (!error.QualityOnly && reader is not null)
                    {
                        await reader.DisposeAsync().ConfigureAwait(false);
                        reader = null;
                    }
                    if (!everConnected)
                    {
                        reason = "connection_failed";
                        break;
                    }
                    if (gap is { } open && clock.Elapsed - open.Since > TimeSpan.FromSeconds(plan.MaximumOutageSeconds))
                    {
                        reason = "connection_lost";
                        break;
                    }
                    Report(error.QualityOnly ? AcquisitionState.Reading : AcquisitionState.Reconnecting, index + 1);
                }
                catch (PlcReadException error)
                {
                    lastError = error.Message;
                    reason = error.Code;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            reason = "operator_stop";
        }
        finally
        {
            if (reader is not null) await reader.DisposeAsync().ConfigureAwait(false);
        }
        CloseGap();
        var statistics = new AcquisitionStatistics(plan.Samples, receivedCount, missed, reconnections,
            roundTrips.Count > 0 ? roundTrips.Average() : 0, roundTrips.Count > 0 ? roundTrips.Max() : 0,
            maximumLateness, gaps.ToArray(), notManual);
        progress?.Report(new(AcquisitionState.Stopped, receivedCount, plan.Samples, missed, clock.Elapsed,
            TimeSpan.Zero, lastRoundTrip, lastError));
        return new AcquisitionResult(reason, statistics, lastError);
    }
}
