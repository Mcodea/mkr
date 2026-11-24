using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Mkr.Core;
using Mkr.Infrastructure.Plc;

// Read-only acquisition checks (COM-002 to COM-004). Runs on Linux and Windows.
// Set MKR_INTEROP_PYTHON to an interpreter with tests/interop/requirements.txt installed
// to also read values encoded by pymodbus and served by the Snap7 library.
var failures = new List<string>();
int passed = 0;

async Task Check(string name, Func<Task> body)
{
    try
    {
        await body();
        passed++;
        Console.WriteLine($"ok    {name}");
    }
    catch (Exception error)
    {
        failures.Add(name);
        Console.WriteLine($"FAIL  {name}: {error.Message}");
    }
}

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static AcquisitionTarget Modbus(int port, ByteOrder order = ByteOrder.CDAB, int sp = 0, int pv = 2, int mv = 4,
    PlcDataType type = PlcDataType.Real32, ModbusTable table = ModbusTable.HoldingRegisters, byte unit = 1) =>
    new(PlcProtocol.ModbusTcp, "127.0.0.1", port, new(sp, type), new(pv, type), new(mv, type), "bar", "%", order, unit, table);

static AcquisitionTarget S7(int port, int db = 10, S7Area area = S7Area.DataBlock) =>
    new(PlcProtocol.S7, "127.0.0.1", port, new(0, PlcDataType.Real32), new(4, PlcDataType.Real32),
        new(8, PlcDataType.Real32), "bar", "%", Rack: 0, Slot: 1, Area: area, DataBlock: db);

static async Task<(AcquisitionResult Result, List<AcquiredSample> Samples)> Capture(AcquisitionTarget target,
    int samples, int periodMs = 100, double outage = 10, CancellationToken token = default)
{
    var list = new List<AcquiredSample>();
    var result = await new PlcAcquisition().CaptureAsync(target, new AcquisitionPlan(samples, periodMs, outage),
        list.Add, null, token);
    return (result, list);
}

static void AssertReadOnlyModbus(ModbusStub stub)
{
    foreach (var frame in stub.Frames)
        if (!ModbusTcpReader.EmittedFunctionCodes.Contains(frame[7]) || frame[7] is not (3 or 4))
            throw new Exception($"Fonction Modbus émise non autorisée : {frame[7]:X2}.");
}

// ---------- Decoding against IEEE 754 / two's-complement constants ----------
await Check("decode REAL 1.0 in the four byte orders", () =>
{
    var cases = new (ByteOrder Order, byte[] Wire)[]
    {
        (ByteOrder.ABCD, [0x3F, 0x80, 0x00, 0x00]), (ByteOrder.CDAB, [0x00, 0x00, 0x3F, 0x80]),
        (ByteOrder.BADC, [0x80, 0x3F, 0x00, 0x00]), (ByteOrder.DCBA, [0x00, 0x00, 0x80, 0x3F]),
    };
    foreach (var (order, wire) in cases)
    {
        Assert(PlcDecoding.Decode(wire, PlcDataType.Real32, order) == 1.0, $"{order} mal décodé");
        Assert(PlcDecoding.Encode(1.0, PlcDataType.Real32, order).SequenceEqual(wire), $"{order} mal encodé");
    }
    return Task.CompletedTask;
});

await Check("decode integers and 16-bit byte swap", () =>
{
    Assert(PlcDecoding.Decode([0xFF, 0xFE], PlcDataType.Int16, ByteOrder.ABCD) == -2, "INT16");
    Assert(PlcDecoding.Decode([0xFE, 0xFF], PlcDataType.Int16, ByteOrder.BADC) == -2, "INT16 permuté");
    Assert(PlcDecoding.Decode([0xFF, 0xFE], PlcDataType.UInt16, ByteOrder.CDAB) == 65534, "UINT16 (CDAB sans effet sur 16 bits)");
    Assert(PlcDecoding.Decode([0x12, 0x34, 0x56, 0x78], PlcDataType.Int32, ByteOrder.ABCD) == 0x12345678, "INT32 ABCD");
    Assert(PlcDecoding.Decode([0x56, 0x78, 0x12, 0x34], PlcDataType.Int32, ByteOrder.CDAB) == 0x12345678, "INT32 CDAB");
    Assert(PlcDecoding.Decode([0xFF, 0xFF, 0xFF, 0xFF], PlcDataType.UInt32, ByteOrder.ABCD) == uint.MaxValue, "UINT32");
    foreach (var type in Enum.GetValues<PlcDataType>())
        foreach (var order in Enum.GetValues<ByteOrder>())
        {
            double value = type is PlcDataType.UInt16 or PlcDataType.UInt32 ? 513 : -513.0;
            Assert(PlcDecoding.Decode(PlcDecoding.Encode(value, type, order), type, order) == value, $"{type} {order}");
        }
    return Task.CompletedTask;
});

await Check("linear scaling and target validation", () =>
{
    Assert(PlcDecoding.Scale(13824, new SignalAddress(0, PlcDataType.Int16, 0, 27648, 0, 100)) == 50, "0..27648 → 50 %");
    Assert(PlcDecoding.Scale(-1234, new SignalAddress(0, PlcDataType.Int16, 0, 10000, 0, 100)) == -12.34, "0..10000");
    Assert(PlcDecoding.Scale(4.5, new SignalAddress(0, PlcDataType.Real32)) == 4.5, "identité");
    var plan = new AcquisitionPlan(10, 100);
    Assert(PlcDecoding.Validate(Modbus(502), plan) is null, "cible valide refusée");
    Assert(PlcDecoding.Validate(Modbus(502) with { Pv = new(2, PlcDataType.Real32, 5, 5, 0, 1) }, plan) is not null, "échelle dégénérée acceptée");
    Assert(PlcDecoding.Validate(Modbus(502, sp: 65535), plan) is not null, "REAL au-delà du registre 65535 accepté");
    Assert(PlcDecoding.Validate(S7(102) with { Order = ByteOrder.CDAB }, plan) is not null, "S7 non ABCD accepté");
    Assert(PlcDecoding.Validate(Modbus(502), plan with { PeriodMs = 50 }) is not null, "période < 100 ms acceptée");
    Assert(PlcDecoding.Validate(Modbus(502) with { ProcessUnit = " " }, plan) is not null, "unité vide acceptée");
    Assert(S7Reader.RemoteTsap(0, 1) == 0x0101 && S7Reader.RemoteTsap(0, 2) == 0x0102 && S7Reader.RemoteTsap(1, 3) == 0x0123, "TSAP");
    return Task.CompletedTask;
});

// ---------- Modbus TCP ----------
await Check("modbus nominal: one FC03 request per reading, CDAB values, raw kept", async () =>
{
    await using var stub = new ModbusStub();
    stub.Place(0, 45.25, PlcDataType.Real32, ByteOrder.CDAB);
    stub.Place(2, -3.5, PlcDataType.Real32, ByteOrder.CDAB);
    stub.Place(4, 61.875, PlcDataType.Real32, ByteOrder.CDAB);
    var (result, samples) = await Capture(Modbus(stub.Port), 5);
    Assert(result.StopReason == "sample_limit" && samples.Count == 5, $"{result.StopReason} {samples.Count}");
    Assert(samples.All(s => s.Sp == 45.25 && s.Pv == -3.5 && s.Mv == 61.875 && s.RawPv == -3.5), "valeurs");
    Assert(samples.All(s => s.RoundTripMs >= 0), "aller-retour");
    var frames = stub.Frames.ToArray();
    Assert(frames.Length == 5 && frames.All(f => f[7] == 3 && f[9] == 0 && f[11] == 6 && f[6] == 1), "trames");
    Assert(frames.Select(f => (int)BinaryPrimitives.ReadUInt16BigEndian(f)).SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "transactions");
    AssertReadOnlyModbus(stub);
});

await Check("modbus input registers use FC04 only", async () =>
{
    await using var stub = new ModbusStub();
    stub.Place(10, 7.5, PlcDataType.Real32, ByteOrder.ABCD, input: true);
    var (result, samples) = await Capture(Modbus(stub.Port, ByteOrder.ABCD, 10, 12, 14, table: ModbusTable.InputRegisters), 2);
    Assert(result.StopReason == "sample_limit" && samples[0].Sp == 7.5, result.StopReason);
    Assert(stub.Frames.All(f => f[7] == 4), "FC04 attendu");
});

await Check("modbus signals > 125 registers apart: one request each", async () =>
{
    await using var stub = new ModbusStub();
    stub.Place(10, 1.5, PlcDataType.Real32, ByteOrder.ABCD);
    stub.Place(600, 2.5, PlcDataType.Real32, ByteOrder.ABCD);
    stub.Place(990, 3.5, PlcDataType.Real32, ByteOrder.ABCD);
    var target = Modbus(stub.Port, ByteOrder.ABCD, 10, 600, 990);
    Assert(new ModbusTcpReader(target).RequestsPerReading == 3, "découpage");
    var (_, samples) = await Capture(target, 1);
    Assert(samples.Single() is { Sp: 1.5, Pv: 2.5, Mv: 3.5 }, "valeurs dispersées");
    Assert(stub.Frames.Count == 3, "trois requêtes");
});

await Check("modbus exception 02 stops capture, keeps prefix, no retry", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) => Task.FromResult<byte[]?>(n <= 2 ? stub.Normal(request) : ModbusStub.Exception(request, 2));
    var (result, samples) = await Capture(Modbus(stub.Port), 10);
    Assert(result.StopReason == "modbus_exception_02" && samples.Count == 2, $"{result.StopReason} {samples.Count}");
    Assert(result.Message!.Contains("base 0"), "message d’aide");
    Assert(stub.Connections == 1 && stub.Frames.Count == 3, "nouvelle tentative interdite");
});

await Check("modbus exception 06 (busy) is a gap, then reading resumes", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) => Task.FromResult<byte[]?>(n == 3 ? ModbusStub.Exception(request, 6) : stub.Normal(request));
    var (result, samples) = await Capture(Modbus(stub.Port), 6);
    Assert(result.StopReason == "sample_limit" && samples.Count == 5, $"{result.StopReason} {samples.Count}");
    var s = result.Statistics;
    Assert(s.MissedSamples == 1 && s.Gaps.Length == 1 && s.Gaps[0].Reason == "modbus_exception_06" && s.Gaps[0].MissedSamples == 1, "lacune");
    Assert(s.Reconnections == 1 && stub.Connections == 2, "reconnexion");
});

await Check("modbus peer closes the connection: reconnection and recorded gap", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) => Task.FromResult<byte[]?>(n == 4 ? [] : stub.Normal(request));
    var (result, samples) = await Capture(Modbus(stub.Port), 8);
    Assert(result.StopReason == "sample_limit" && samples.Count == 7, $"{result.StopReason} {samples.Count}");
    Assert(result.Statistics.Reconnections == 1 && result.Statistics.Gaps.Single().Reason == "connection_closed", "trace");
    Assert(samples.Zip(samples.Skip(1)).All(p => p.Second.ReceivedAt > p.First.ReceivedAt), "horodatages croissants");
});

await Check("modbus silent peer: 5 s timeout, gap, reconnection", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) => Task.FromResult<byte[]?>(n == 2 ? null : stub.Normal(request));
    var (result, samples) = await Capture(Modbus(stub.Port), 60, outage: 30);
    Assert(result.StopReason == "sample_limit", result.StopReason);
    var gap = result.Statistics.Gaps.Single();
    Assert(gap.Reason == "timeout" && result.Statistics.Reconnections == 1, $"{gap.Reason}");
    // The slots that elapsed during the 5 s wait are missed (one gap), never read late.
    Assert(gap.MissedSamples is >= 49 and <= 53 && samples.Count + result.Statistics.MissedSamples == 60, $"{gap.MissedSamples} {samples.Count}");
    Assert(samples.Skip(1).First().ReceivedAt - samples[0].ReceivedAt > TimeSpan.FromSeconds(4.9), "lacune temporelle visible");
});

await Check("modbus fragmented reply is reassembled", async () =>
{
    await using var stub = new ModbusStub { SplitReplies = true };
    stub.Place(2, 12.5, PlcDataType.Real32, ByteOrder.CDAB);
    var (result, samples) = await Capture(Modbus(stub.Port), 3);
    Assert(result.StopReason == "sample_limit" && samples.All(s => s.Pv == 12.5), "valeur");
});

await Check("modbus non-finite REAL is a quality gap without reconnection", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) =>
    {
        var reply = stub.Normal(request);
        // PV (registers 2–3) = NaN 0x7FC00000, transmitted CDAB: low word 0x0000 then high word 0x7FC0.
        if (n == 2) { reply[13] = 0; reply[14] = 0; reply[15] = 0x7F; reply[16] = 0xC0; }
        return Task.FromResult<byte[]?>(reply);
    };
    var (result, samples) = await Capture(Modbus(stub.Port), 4);
    Assert(samples.Count == 3 && result.Statistics.Gaps.Single().Reason == "non_finite_value", $"{samples.Count}");
    Assert(stub.Connections == 1 && result.Statistics.Reconnections == 0, "pas de reconnexion pour une valeur");
});

await Check("modbus connection refused: immediate connection_failed", async () =>
{
    int port;
    await using (var stub = new ModbusStub()) port = stub.Port;
    var (result, samples) = await Capture(Modbus(port), 5);
    Assert(result.StopReason == "connection_failed" && samples.Count == 0 && result.Message is not null, result.StopReason);
});

await Check("modbus outage longer than tolerated stops with connection_lost", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) =>
    {
        if (n == 3) stub.Accepting = false;
        return Task.FromResult<byte[]?>(n >= 3 ? [] : stub.Normal(request));
    };
    var (result, samples) = await Capture(Modbus(stub.Port), 50, outage: 0.5);
    Assert(result.StopReason == "connection_lost" && samples.Count == 2, $"{result.StopReason} {samples.Count}");
    Assert(result.Statistics.MissedSamples >= 2, "lacunes");
});

await Check("modbus schedule keeps the period despite a 40 ms round trip", async () =>
{
    await using var stub = new ModbusStub { Delay = TimeSpan.FromMilliseconds(40) };
    var (result, samples) = await Capture(Modbus(stub.Port), 11, periodMs: 100);
    Assert(samples.Count == 11, $"{samples.Count}");
    var intervals = samples.Zip(samples.Skip(1), (a, b) => (b.ReceivedAt - a.ReceivedAt).TotalMilliseconds).Order().ToArray();
    double median = intervals[intervals.Length / 2];
    Assert(Math.Abs(median - 100) < 20, $"période médiane {median:0.0} ms (une pause donnerait ≈ 140 ms)");
    Assert(result.Statistics.MeanRoundTripMs >= 35, $"aller-retour {result.Statistics.MeanRoundTripMs}");
});

await Check("modbus operator stop keeps the received prefix", async () =>
{
    await using var stub = new ModbusStub();
    using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
    var (result, samples) = await Capture(Modbus(stub.Port), 100, token: stop.Token);
    Assert(result.StopReason == "operator_stop" && samples.Count is >= 2 and <= 5, $"{result.StopReason} {samples.Count}");
});

await Check("progress reports state, counts and remaining time", async () =>
{
    await using var stub = new ModbusStub();
    var reports = new List<AcquisitionProgress>();
    var progress = new SynchronousProgress(reports.Add);
    var result = await new PlcAcquisition().CaptureAsync(Modbus(stub.Port), new AcquisitionPlan(4, 100), _ => { }, progress, default);
    Assert(reports.First().State == AcquisitionState.Connecting && reports.Last().State == AcquisitionState.Stopped, "états");
    Assert(reports.Any(r => r.State == AcquisitionState.Connected), "connecté");
    var reading = reports.Where(r => r.State == AcquisitionState.Reading).ToArray();
    Assert(reading.Select(r => r.Received).SequenceEqual(new[] { 1, 2, 3, 4 }), "compteur");
    Assert(reading.First().Remaining > reading.Last().Remaining && reading.Last().Remaining == TimeSpan.Zero, "temps restant");
    Assert(reading.All(r => r.LastRoundTripMs is >= 0), "latence");
});

// ---------- S7comm (stub for refusals and frame audit) ----------
await Check("s7 nominal: only setup (F0) and read (04), TSAP 0x0101, REAL ABCD", async () =>
{
    await using var stub = new S7Stub();
    var db = new byte[16];
    BinaryPrimitives.WriteSingleBigEndian(db.AsSpan(0), 52.5f);
    BinaryPrimitives.WriteSingleBigEndian(db.AsSpan(4), 48.25f);
    BinaryPrimitives.WriteSingleBigEndian(db.AsSpan(8), -12.75f);
    stub.Memory[(0x84, 10)] = db;
    var (result, samples) = await Capture(S7(stub.Port), 3);
    Assert(result.StopReason == "sample_limit" && samples.All(s => s is { Sp: 52.5, Pv: 48.25, Mv: -12.75 }), result.StopReason);
    Assert(stub.Functions.All(f => S7Reader.EmittedFunctions.Contains(f) && f is 0xF0 or 0x04), "fonctions S7");
    Assert(stub.Functions.Count(f => f == 0x04) == 3 && stub.CalledTsaps.Single() == 0x0101, "lecture et TSAP");
});

await Check("s7 merker area and odd-length padding", async () =>
{
    await using var stub = new S7Stub();
    var memory = new byte[40];
    BinaryPrimitives.WriteInt16BigEndian(memory.AsSpan(20), 13824);
    BinaryPrimitives.WriteInt16BigEndian(memory.AsSpan(22), -5);
    BinaryPrimitives.WriteInt32BigEndian(memory.AsSpan(24), -123456);
    stub.Memory[(0x83, 0)] = memory;
    var target = S7(stub.Port, area: S7Area.Merker) with
    {
        Sp = new(20, PlcDataType.Int16, 0, 27648, 0, 100), Pv = new(22, PlcDataType.Int16), Mv = new(24, PlcDataType.Int32)
    };
    var (_, samples) = await Capture(target, 1);
    Assert(samples.Single() is { Sp: 50, Pv: -5, Mv: -123456 }, $"{samples.Single()}");
});

await Check("s7 missing DB (0x0A) stops with an explicit reason", async () =>
{
    await using var stub = new S7Stub();
    var (result, samples) = await Capture(S7(stub.Port, db: 99), 3);
    Assert(result.StopReason == "s7_item_0A" && samples.Count == 0 && result.Message!.Contains("DB"), result.StopReason);
});

await Check("s7 PUT/GET disabled (0x8104) explains the CPU setting", async () =>
{
    await using var stub = new S7Stub { HeaderError = (0x81, 0x04) };
    var (result, _) = await Capture(S7(stub.Port), 3);
    Assert(result.StopReason == "s7_error_8104" && result.Message!.Contains("PUT/GET"), result.StopReason);
});

await Check("s7 optimized block (0x03) is refused, not retried", async () =>
{
    await using var stub = new S7Stub { ItemCode = 0x03 };
    stub.Memory[(0x84, 10)] = new byte[16];
    var (result, _) = await Capture(S7(stub.Port), 3);
    Assert(result.StopReason == "s7_item_03" && result.Message!.Contains("optimisé"), result.StopReason);
    Assert(stub.Functions.Count(f => f == 0x04) == 1, "une seule tentative");
});

// ---------- Historian: long recording in segments ----------
static async Task<(HistorianResult Result, List<HistorianSegment> Segments)> Record(AcquisitionTarget target, HistorianPlan plan,
    Func<HistorianSegment, Task>? failing = null, CancellationToken token = default)
{
    var segments = new List<HistorianSegment>();
    var result = await new HistorianRecorder().RunAsync(target, plan, failing ?? (segment => { segments.Add(segment); return Task.CompletedTask; }),
        null, token);
    return (result, segments);
}

await Check("historian cuts a long recording into overlapping segments without losing a reading", async () =>
{
    await using var stub = new ModbusStub();
    stub.Place(0, 1.5, PlcDataType.Real32, ByteOrder.CDAB);
    var plan = new HistorianPlan(100, 35, SegmentSamples: 10, OverlapSamples: 2);
    Assert(plan.ExpectedSegments == 5, $"segments attendus {plan.ExpectedSegments}");
    var (result, segments) = await Record(Modbus(stub.Port), plan);
    Assert(result.StopReason == "sample_limit" && result.SegmentsSaved == 5 && segments.Count == 5, $"{result.StopReason} {segments.Count}");
    Assert(segments.Select(s => s.Samples.Count).SequenceEqual(new[] { 10, 10, 10, 10, 3 }), "tailles des segments");
    Assert(segments.Select(s => s.Origin.Session!.SegmentIndex).SequenceEqual(new[] { 1, 2, 3, 4, 5 }) &&
           segments.Select(s => s.Origin.Session!.SessionId).Distinct().Count() == 1, "session et ordre");
    Assert(segments.Select(s => s.Origin.Session!.OverlapSamples).SequenceEqual(new[] { 0, 2, 2, 2, 2 }), "recouvrement");
    Assert(segments.Take(4).All(s => s.Origin.StopReason == "segment_boundary") && segments[4].Origin.StopReason == "sample_limit", "motifs");
    // The overlap repeats the previous tail exactly; removing it gives the complete, ordered, gap-free timeline.
    for (int i = 1; i < segments.Count; i++)
        Assert(segments[i].Samples.Take(2).SequenceEqual(segments[i - 1].Samples.TakeLast(2)), $"recouvrement du segment {i + 1}");
    var timeline = segments[0].Samples.Concat(segments.Skip(1).SelectMany(s => s.Samples.Skip(2))).ToList();
    Assert(timeline.Count == 35 && timeline.Zip(timeline.Skip(1)).All(p => p.Second.ReceivedAt > p.First.ReceivedAt), "chronologie complète");
    Assert(segments.All(s => s.Samples.All(x => x.Sp == 1.5)) && segments.All(s => s.Origin.Target == Modbus(stub.Port)), "valeurs et cible");
    AssertReadOnlyModbus(stub);
});

await Check("historian attaches a gap to the segment that follows it, never interpolated", async () =>
{
    await using var stub = new ModbusStub();
    stub.Behaviour = (n, request) => Task.FromResult<byte[]?>(n == 14 ? [] : stub.Normal(request));
    var (result, segments) = await Record(Modbus(stub.Port), new HistorianPlan(100, 30, SegmentSamples: 10, OverlapSamples: 2));
    Assert(result.StopReason == "sample_limit" && result.Statistics.MissedSamples == 1, $"{result.StopReason} {result.Statistics.MissedSamples}");
    var holders = segments.Where(s => s.Origin.Statistics!.Gaps.Length > 0).ToArray();
    Assert(holders.Length == 1 && holders[0].Origin.Statistics!.MissedSamples == 1 &&
           holders[0].Origin.Statistics!.Gaps[0].Reason == "connection_closed", "une seule lacune, dans un seul segment");
    var inside = holders[0].Samples.Skip(holders[0].Origin.Session!.OverlapSamples).ToArray();
    var largest = inside.Zip(inside.Skip(1), (a, b) => (b.ReceivedAt - a.ReceivedAt).TotalMilliseconds).Max();
    Assert(largest > 150, $"la lacune est visible dans les horodatages ({largest:0} ms)");
    Assert(segments.Where(s => s != holders[0]).All(s => s.Origin.Statistics!.MissedSamples == 0), "les autres segments sont sans lacune");
});

await Check("historian stops on a storage failure and keeps what was saved", async () =>
{
    await using var stub = new ModbusStub();
    int calls = 0;
    var (result, _) = await Record(Modbus(stub.Port), new HistorianPlan(100, 200, SegmentSamples: 10, OverlapSamples: 2),
        _ => ++calls == 2 ? throw new IOException("disque plein") : Task.CompletedTask);
    Assert(result.StopReason == "storage_error" && result.Message!.Contains("disque plein") && result.SegmentsSaved == 1, $"{result.StopReason} {result.SegmentsSaved}");
    Assert(stub.Frames.Count < 60, $"l'acquisition s'arrête ({stub.Frames.Count} requêtes sur 200 prévues)");
});

await Check("historian operator stop saves the open segment", async () =>
{
    await using var stub = new ModbusStub();
    using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(520));
    var (result, segments) = await Record(Modbus(stub.Port), new HistorianPlan(100, 1000, SegmentSamples: 50, OverlapSamples: 5), token: stop.Token);
    Assert(result.StopReason == "operator_stop" && segments.Count == 1 && segments[0].Origin.StopReason == "operator_stop", $"{result.StopReason} {segments.Count}");
    Assert(segments[0].Samples.Count is >= 4 and <= 7, $"{segments[0].Samples.Count} mesures conservées");
});

await Check("historian plan is validated", () =>
{
    foreach (var bad in new[]
    {
        new HistorianPlan(50, 100), new HistorianPlan(1000, 0), new HistorianPlan(1000, HistorianPlan.MaximumTotalSamples + 1),
        new HistorianPlan(1000, 100, SegmentSamples: 5), new HistorianPlan(1000, 100, SegmentSamples: 6000),
        new HistorianPlan(1000, 100, SegmentSamples: 100, OverlapSamples: 60), new HistorianPlan(1000, 100, MaximumOutageSeconds: -1)
    })
        Assert(bad.Validate() is not null, $"plan accepté : {bad}");
    Assert(new HistorianPlan(1000, 86400).Validate() is null && new HistorianPlan(1000, 86400).ExpectedSegments == 32, "plan d'un jour à 1 s");
    return Task.CompletedTask;
});

// ---------- Persistence ----------
await Check("origin JSON round trip and legacy record compatibility", () =>
{
    var target = Modbus(502, ByteOrder.CDAB) with { Pv = new(2, PlcDataType.Int16, 0, 10000, 0, 100) };
    var origin = new AcquisitionOrigin(null, "sample_limit", "receiver_utc_midpoint", target, new AcquisitionPlan(10, 200),
        new AcquisitionStatistics(10, 9, 1, 1, 3.2, 8.1, 1.5,
            [new AcquisitionGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), "timeout", 1)]));
    var json = JsonSerializer.Serialize(origin);
    Assert(json.Contains("\"Protocol\":\"ModbusTcp\"") && json.Contains("\"Order\":\"CDAB\"") && !json.Contains("Signals"), json);
    var back = JsonSerializer.Deserialize<AcquisitionOrigin>(json)!;
    Assert(JsonSerializer.Serialize(back) == json && back.Target == target && back.ProcessUnit == "bar", "aller-retour");
    var legacy = JsonSerializer.Deserialize<AcquisitionOrigin>(
        """{"Source":{"Host":"127.0.0.1","Port":502,"UnitId":7,"FirstRegister":42,"LowWordFirst":false,"ProcessUnit":"bar","CommandUnit":"%","IntervalMs":100},"StopReason":"sample_limit","TimestampOrigin":"receiver_utc"}""")!;
    Assert(legacy.Target is null && legacy.ProcessUnit == "bar" && legacy.CommandUnit == "%" && legacy.Describe().Contains("offset 42"), "ancien format");
    return Task.CompletedTask;
});

// ---------- Interoperability with independent implementations ----------
var python = Environment.GetEnvironmentVariable("MKR_INTEROP_PYTHON");
if (string.IsNullOrWhiteSpace(python))
{
    Console.WriteLine("skip  interopérabilité pymodbus/Snap7 (MKR_INTEROP_PYTHON non défini)");
}
else
{
    var directory = Directory.CreateTempSubdirectory("mkr-interop");
    var manifestPath = Path.Combine(directory.FullName, "manifest.json");
    int modbusPort = FreePort(), s7Port = FreePort();
    var script = Path.Combine(FindRoot(), "tests", "interop", "plc_servers.py");
    var start = new ProcessStartInfo(python) { RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var argument in new[] { "-I", script, manifestPath, modbusPort.ToString(), s7Port.ToString() }) start.ArgumentList.Add(argument);
    using var server = Process.Start(start)!;
    _ = server.StandardOutput.ReadToEndAsync();
    var errors = server.StandardError.ReadToEndAsync();
    try
    {
        var deadline = Stopwatch.StartNew();
        while (!File.Exists(manifestPath) && deadline.Elapsed < TimeSpan.FromSeconds(30) && !server.HasExited) await Task.Delay(100);
        await Task.Delay(500);
        if (!File.Exists(manifestPath)) throw new Exception("Serveurs de référence non démarrés : " + (server.HasExited ? await errors : "délai"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        SignalAddress Signal(JsonElement s) => new(s.GetProperty("offset").GetInt32(), Enum.Parse<PlcDataType>(s.GetProperty("type").GetString()!),
            s.TryGetProperty("rawLow", out var a) ? a.GetDouble() : 0, s.TryGetProperty("rawHigh", out var b) ? b.GetDouble() : 1,
            s.TryGetProperty("engLow", out var c) ? c.GetDouble() : 0, s.TryGetProperty("engHigh", out var d) ? d.GetDouble() : 1);

        async Task Verify(string name, AcquisitionTarget target, JsonElement @case)
        {
            var (result, samples) = await Capture(target, 3);
            if (@case.TryGetProperty("expectedError", out var expectedError))
            {
                Assert(result.StopReason == expectedError.GetString() && samples.Count == 0, $"{name}: {result.StopReason}");
                return;
            }
            Assert(result.StopReason == "sample_limit" && samples.Count == 3, $"{name}: {result.StopReason} {result.Message}");
            var expected = @case.GetProperty("expected").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            foreach (var sample in samples)
                foreach (var (actual, wanted, role) in new[] { (sample.Sp, expected[0], "SP"), (sample.Pv, expected[1], "PV"), (sample.Mv, expected[2], "MV") })
                    Assert(Math.Abs(actual - wanted) <= 1e-6 * Math.Max(1, Math.Abs(wanted)), $"{name} {role}: {actual} ≠ {wanted}");
        }

        foreach (var @case in root.GetProperty("modbus").EnumerateArray())
        {
            var s = @case.GetProperty("signals").EnumerateArray().Select(Signal).ToArray();
            var target = new AcquisitionTarget(PlcProtocol.ModbusTcp, "127.0.0.1", modbusPort, s[0], s[1], s[2], "bar", "%",
                Enum.Parse<ByteOrder>(@case.GetProperty("order").GetString()!), (byte)root.GetProperty("modbusUnit").GetInt32(),
                Enum.Parse<ModbusTable>(@case.GetProperty("table").GetString()!));
            await Check("interop pymodbus · " + @case.GetProperty("name").GetString(), () => Verify(@case.GetProperty("name").GetString()!, target, @case));
        }
        foreach (var @case in root.GetProperty("s7").EnumerateArray())
        {
            var s = @case.GetProperty("signals").EnumerateArray().Select(Signal).ToArray();
            var target = new AcquisitionTarget(PlcProtocol.S7, "127.0.0.1", s7Port, s[0], s[1], s[2], "bar", "%",
                Area: Enum.Parse<S7Area>(@case.GetProperty("area").GetString()!), DataBlock: Math.Max(1, @case.GetProperty("db").GetInt32()));
            await Check("interop Snap7 · " + @case.GetProperty("name").GetString(), () => Verify(@case.GetProperty("name").GetString()!, target, @case));
        }
    }
    finally
    {
        if (!server.HasExited) server.Kill(entireProcessTree: true);
        try { directory.Delete(true); } catch (IOException) { }
    }
}

Console.WriteLine($"{passed} réussite(s), {failures.Count} échec(s).");
return failures.Count == 0 ? 0 : 1;

static int FreePort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MKR.slnx"))) directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
}

internal sealed class SynchronousProgress(Action<AcquisitionProgress> report) : IProgress<AcquisitionProgress>
{
    public void Report(AcquisitionProgress value) => report(value);
}
