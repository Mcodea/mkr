using System.Text.Json.Serialization;

namespace Mkr.Core;

/// <summary>Legacy Modbus source (captures recorded before COM-005). Kept so archived studies stay readable.</summary>
public sealed record RegisterSource(string Host, int Port, byte UnitId, ushort FirstRegister,
    bool LowWordFirst, string ProcessUnit, string CommandUnit, int IntervalMs);

/// <summary>Read-only protocols supported for bench acquisition.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlcProtocol>))]
public enum PlcProtocol
{
    /// <summary>Schneider Electric (M340, M580, Premium…) over Modbus TCP, function codes 03/04 only.</summary>
    ModbusTcp,
    /// <summary>Siemens S7-1200/1500 (and S7-300/400) over S7comm on ISO-on-TCP port 102, read job only.</summary>
    S7
}

[JsonConverter(typeof(JsonStringEnumConverter<PlcDataType>))]
public enum PlcDataType { Int16, UInt16, Int32, UInt32, Real32 }

/// <summary>
/// Byte order of a value as transmitted, A being the most significant byte.
/// ABCD is the protocol order (big-endian). CDAB swaps the two 16-bit words,
/// BADC swaps the bytes inside each word, DCBA reverses all bytes.
/// For 16-bit types only the byte swap matters (ABCD/CDAB = AB, BADC/DCBA = BA).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ByteOrder>))]
public enum ByteOrder { ABCD, CDAB, BADC, DCBA }

/// <summary>Modbus table read: holding registers (FC03, %MW on Schneider) or input registers (FC04).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModbusTable>))]
public enum ModbusTable { HoldingRegisters, InputRegisters }

/// <summary>S7 memory area. Only absolute addressing in non-optimized blocks is reachable over S7comm.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<S7Area>))]
public enum S7Area { DataBlock, Merker }

/// <summary>
/// Address and linear scaling of one signal.
/// Modbus: Offset is the register offset, base 0 (%MW100 → 100, Modicon 400101 → 100).
/// S7: Offset is the byte offset in the area (DB10.DBD4 → 4, MW20 → 20).
/// Engineering value = EngineeringLow + (raw − RawLow) × (EngineeringHigh − EngineeringLow) / (RawHigh − RawLow).
/// </summary>
public sealed record SignalAddress(int Offset, PlcDataType Type,
    double RawLow = 0, double RawHigh = 1, double EngineeringLow = 0, double EngineeringHigh = 1)
{
    [JsonIgnore] public int ByteLength => Type is PlcDataType.Int16 or PlcDataType.UInt16 ? 2 : 4;
    [JsonIgnore] public bool IsIdentity => RawLow == 0 && RawHigh == 1 && EngineeringLow == 0 && EngineeringHigh == 1;
}

/// <summary>Complete, explicit description of what is read and how it is decoded.</summary>
public sealed record AcquisitionTarget(
    PlcProtocol Protocol, string Host, int Port,
    SignalAddress Sp, SignalAddress Pv, SignalAddress Mv,
    string ProcessUnit, string CommandUnit,
    ByteOrder Order = ByteOrder.ABCD,
    byte UnitId = 1, ModbusTable Table = ModbusTable.HoldingRegisters,
    int Rack = 0, int Slot = 1, S7Area Area = S7Area.DataBlock, int DataBlock = 1,
    SignalAddress? Mode = null, double? ManualModeValue = null)
{
    /// <summary>
    /// SP, PV, MV and, when the user supplied its address, the controller mode (read raw, never scaled).
    /// <see cref="ManualModeValue"/> is the raw value that the user declares to mean "manual": MKR assumes no convention.
    /// </summary>
    [JsonIgnore] public IEnumerable<(string Role, SignalAddress Address)> Signals =>
        Mode is null ? [("SP", Sp), ("PV", Pv), ("MV", Mv)] : [("SP", Sp), ("PV", Pv), ("MV", Mv), ("MODE", Mode)];

    public string Describe()
    {
        string Location(SignalAddress address) => Protocol == PlcProtocol.ModbusTcp
            ? $"{(Table == ModbusTable.HoldingRegisters ? "%MW" : "IR")}{address.Offset} {address.Type}"
            : Area == S7Area.DataBlock
                ? $"DB{DataBlock}.{S7Suffix(address)}{address.Offset} {address.Type}"
                : $"M{S7Suffix(address)[2..]}{address.Offset} {address.Type}";
        var head = Protocol == PlcProtocol.ModbusTcp
            ? $"Modbus TCP {Host}:{Port} · unité {UnitId} · {(Table == ModbusTable.HoldingRegisters ? "FC03" : "FC04")} · ordre {Order}"
            : $"S7 {Host}:{Port} · rack {Rack} · emplacement {Slot}";
        return $"{head} · SP {Location(Sp)} · PV {Location(Pv)} · MV {Location(Mv)}" +
            (Mode is null ? "" : $" · MODE {Location(Mode)} (manuel = {ManualModeValue:G9})");
    }

    private static string S7Suffix(SignalAddress address) => address.ByteLength == 2 ? "DBW" : "DBD";
}

/// <summary>Sampling plan. The period is a schedule on a monotonic clock, not a pause after each reply.</summary>
public sealed record AcquisitionPlan(int Samples, int PeriodMs, double MaximumOutageSeconds = 10)
{
    [JsonIgnore] public TimeSpan PlannedDuration => TimeSpan.FromMilliseconds((double)Math.Max(0, Samples - 1) * PeriodMs);
}

/// <summary>
/// One coherent reading. The timestamp is the UTC midpoint between request and reply on this computer.
/// Raw values are the decoded numbers before scaling, kept to check the mapping.
/// </summary>
public sealed record AcquiredSample(DateTimeOffset ReceivedAt, double Sp, double Pv, double Mv,
    double RoundTripMs = double.NaN, double RawSp = double.NaN, double RawPv = double.NaN, double RawMv = double.NaN,
    double? RawMode = null, bool? InManual = null);

/// <summary>Interval without valid data. Gaps are recorded, never filled.</summary>
public sealed record AcquisitionGap(DateTimeOffset From, DateTimeOffset To, string Reason, int MissedSamples);

public sealed record AcquisitionStatistics(int Requested, int Received, int MissedSamples, int Reconnections,
    double MeanRoundTripMs, double MaximumRoundTripMs, double MaximumLatenessMs, AcquisitionGap[] Gaps,
    int SamplesNotInManual = 0);

[JsonConverter(typeof(JsonStringEnumConverter<AcquisitionState>))]
public enum AcquisitionState { Connecting, Connected, Reading, Reconnecting, Stopped }

public sealed record AcquisitionProgress(AcquisitionState State, int Received, int Requested, int MissedSamples,
    TimeSpan Elapsed, TimeSpan Remaining, double? LastRoundTripMs, string? LastError);

public sealed record AcquisitionResult(string StopReason, AcquisitionStatistics Statistics, string? Message = null);

public interface IReadOnlyAcquisition
{
    /// <summary>
    /// Reads only. Never issues write, force, control or program operations.
    /// <paramref name="gapClosed"/> is called, when set, just before the first reading that follows a gap.
    /// </summary>
    Task<AcquisitionResult> CaptureAsync(AcquisitionTarget target, AcquisitionPlan plan,
        Action<AcquiredSample> received, IProgress<AcquisitionProgress>? progress, CancellationToken cancellationToken,
        Action<AcquisitionGap>? gapClosed = null);
}

/// <summary>
/// Long recording cut into segments saved as they complete (crash-safe). Each segment starts with the
/// last <see cref="OverlapSamples"/> readings of the previous one so a step near a cut stays whole in one segment.
/// A step test longer than the overlap can still be cut: plan a one-shot acquisition for it.
/// </summary>
public sealed record HistorianPlan(int PeriodMs, long TotalSamples, int SegmentSamples = 3000,
    int OverlapSamples = 300, double MaximumOutageSeconds = 30)
{
    public const long MaximumTotalSamples = 2_000_000;
    public const int MinimumSegmentSamples = 10;
    public const int MaximumSegmentSamples = 5000;

    [JsonIgnore] public TimeSpan PlannedDuration => TimeSpan.FromMilliseconds((double)Math.Max(0, TotalSamples - 1) * PeriodMs);
    [JsonIgnore] public int NewSamplesPerSegment => SegmentSamples - OverlapSamples;
    [JsonIgnore] public long ExpectedSegments => (TotalSamples + NewSamplesPerSegment - 1 - OverlapSamples) / NewSamplesPerSegment;

    public string? Validate() =>
        PeriodMs is < 100 or > 60000 ? "La période doit être comprise entre 100 ms et 60 s."
        : TotalSamples is < 1 or > MaximumTotalSamples ? $"La durée doit représenter entre 1 et {MaximumTotalSamples:N0} mesures."
        : SegmentSamples is < MinimumSegmentSamples or > MaximumSegmentSamples
            ? $"Un segment contient entre {MinimumSegmentSamples} et {MaximumSegmentSamples} mesures."
        : OverlapSamples < 0 || OverlapSamples > SegmentSamples / 2 ? "Le recouvrement ne peut dépasser la moitié d’un segment."
        : !double.IsFinite(MaximumOutageSeconds) || MaximumOutageSeconds is < 0 or > 3600 ? "La coupure tolérée doit être comprise entre 0 et 3600 s."
        : null;
}

/// <summary>Position of one stored segment inside a long recording.</summary>
public sealed record HistorianSession(Guid SessionId, int SegmentIndex, int OverlapSamples);

/// <summary>
/// Provenance of a capture. Source is set only on legacy records; new captures set Target, Plan and Statistics.
/// </summary>
public sealed record AcquisitionOrigin(RegisterSource? Source, string StopReason, string TimestampOrigin,
    AcquisitionTarget? Target = null, AcquisitionPlan? Plan = null, AcquisitionStatistics? Statistics = null,
    HistorianSession? Session = null)
{
    [JsonIgnore] public string ProcessUnit => Target?.ProcessUnit ?? Source?.ProcessUnit ?? "";
    [JsonIgnore] public string CommandUnit => Target?.CommandUnit ?? Source?.CommandUnit ?? "";

    public string Describe()
    {
        if (Target is { } target)
        {
            var text = $"{target.Describe()} · période {Plan?.PeriodMs} ms · horodatage UTC milieu requête/réponse · fin : {StopReason}";
            if (Statistics is { } s)
                text += $" · {s.Received}/{s.Requested} mesures, {s.MissedSamples} manquées, {s.Reconnections} reconnexion(s)," +
                        $" aller-retour moyen {s.MeanRoundTripMs:G3} ms, max {s.MaximumRoundTripMs:G3} ms";
            if (Session is { } session)
                text += $" · historique {session.SessionId.ToString()[..8]} segment {session.SegmentIndex}" +
                        (session.OverlapSamples > 0 ? $" (recouvre les {session.OverlapSamples} mesures précédentes)" : "");
            return text;
        }
        return Source is { } legacy
            ? $"Modbus TCP · unité {legacy.UnitId} · offset {legacy.FirstRegister} · pause {legacy.IntervalMs} ms · horodatage UTC de réception · fin : {StopReason}"
            : $"Acquisition · fin : {StopReason}";
    }
}
