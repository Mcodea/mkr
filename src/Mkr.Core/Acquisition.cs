namespace Mkr.Core;

public sealed record RegisterSource(string Host, int Port, byte UnitId, ushort FirstRegister,
    bool LowWordFirst, string ProcessUnit, string CommandUnit, int IntervalMs);

public sealed record AcquiredSample(DateTimeOffset ReceivedAt, float Sp, float Pv, float Mv);

public interface IReadOnlyAcquisition
{
    Task CaptureAsync(RegisterSource source, int maximumSamples, Action<AcquiredSample> received,
        CancellationToken cancellationToken);
}

public sealed record AcquisitionOrigin(RegisterSource Source, string StopReason, string TimestampOrigin);
