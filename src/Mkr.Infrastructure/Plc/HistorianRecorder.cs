using System.Threading.Channels;
using Mkr.Core;

namespace Mkr.Infrastructure.Plc;

/// <summary>One stored piece of a long recording, ready to archive.</summary>
public sealed record HistorianSegment(IReadOnlyList<AcquiredSample> Samples, AcquisitionOrigin Origin);

public sealed record HistorianResult(string StopReason, string? Message, int SegmentsSaved, AcquisitionStatistics Statistics);

/// <summary>
/// Builds a history from the PLC: one continuous read-only acquisition cut into segments that are
/// saved as soon as they are complete, so a crash or power loss loses at most the open segment.
/// Gaps are attached to the segment that follows them, never interpolated.
/// </summary>
public sealed class HistorianRecorder(Func<AcquisitionTarget, IPlcReader>? factory = null)
{
    public async Task<HistorianResult> RunAsync(AcquisitionTarget target, HistorianPlan plan,
        Func<HistorianSegment, Task> save, IProgress<AcquisitionProgress>? progress, CancellationToken token)
    {
        if (plan.Validate() is { } invalidPlan) throw new ArgumentException(invalidPlan);
        var acquisitionPlan = new AcquisitionPlan((int)plan.TotalSamples, plan.PeriodMs, plan.MaximumOutageSeconds);
        if (PlcDecoding.Validate(target, acquisitionPlan, HistorianPlan.MaximumTotalSamples) is { } invalid)
            throw new ArgumentException(invalid);

        var session = Guid.NewGuid();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var queue = Channel.CreateUnbounded<HistorianSegment>();
        Exception? storageFailure = null;
        int saved = 0;
        // Segments are archived one at a time, in order, while the acquisition keeps its schedule.
        var writer = Task.Run(async () =>
        {
            await foreach (var segment in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (storageFailure is not null) continue;
                try
                {
                    await save(segment).ConfigureAwait(false);
                    Interlocked.Increment(ref saved);
                }
                catch (Exception error)
                {
                    storageFailure = error;
                    stop.Cancel();
                }
            }
        });

        var buffer = new List<AcquiredSample>();
        var segmentGaps = new List<AcquisitionGap>();
        var pendingGaps = new List<AcquisitionGap>();
        int carried = 0, index = 0;

        HistorianSegment Close(string stopReason)
        {
            index++;
            var samples = buffer.ToArray();
            var gaps = segmentGaps.ToArray();
            var missed = gaps.Sum(g => g.MissedSamples);
            var trips = samples.Skip(carried).Select(s => s.RoundTripMs).Where(double.IsFinite).ToArray();
            var statistics = new AcquisitionStatistics(samples.Length - carried + missed, samples.Length - carried, missed,
                gaps.Count(g => g.Reason != "overrun"), trips.Length > 0 ? trips.Average() : 0, trips.Length > 0 ? trips.Max() : 0, 0, gaps,
                samples.Skip(carried).Count(s => s.InManual == false));
            var segmentPlan = new AcquisitionPlan(samples.Length, plan.PeriodMs, plan.MaximumOutageSeconds);
            return new HistorianSegment(samples, new AcquisitionOrigin(null, stopReason, "receiver_utc_midpoint", target,
                segmentPlan, statistics, new HistorianSession(session, index, index == 1 ? 0 : carried)));
        }

        void Received(AcquiredSample sample)
        {
            buffer.Add(sample);
            segmentGaps.AddRange(pendingGaps);
            pendingGaps.Clear();
            if (buffer.Count < plan.SegmentSamples) return;
            queue.Writer.TryWrite(Close("segment_boundary"));
            var tail = buffer.Skip(buffer.Count - plan.OverlapSamples).ToList();
            buffer.Clear();
            buffer.AddRange(tail);
            carried = tail.Count;
            segmentGaps.Clear();
        }

        AcquisitionResult? result = null;
        try
        {
            result = await new PlcAcquisition(factory, HistorianPlan.MaximumTotalSamples).CaptureAsync(target, acquisitionPlan,
                Received, progress, stop.Token, pendingGaps.Add).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            // The open segment is kept as soon as it holds at least one new reading.
            if (buffer.Count > carried)
            {
                segmentGaps.AddRange(pendingGaps);
                queue.Writer.TryWrite(Close(result?.StopReason ?? (storageFailure is null ? "operator_stop" : "storage_error")));
            }
            queue.Writer.Complete();
            await writer.ConfigureAwait(false);
        }

        var reason = storageFailure is not null ? "storage_error" : result?.StopReason ?? "operator_stop";
        var message = storageFailure is not null ? $"Enregistrement d’un segment impossible : {storageFailure.Message}" : result?.Message;
        return new HistorianResult(reason, message, saved, result?.Statistics ?? new AcquisitionStatistics(0, 0, 0, 0, 0, 0, 0, []));
    }
}
