using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Mkr.Desktop;
using Mkr.Core;
using Mkr.Infrastructure;
using Mkr.Infrastructure.Plc;

internal static class AcquisitionChecks
{
    internal static async Task Run(string db, Guid project, PythonDiagnostics engine)
    {
        await CheckInterruptedSession();
        await CheckHistorian(db, project, engine);
        await CheckIdentification(db, project, engine);
        foreach (bool swapped in new[] { false, true })
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var target = Target(((IPEndPoint)listener.LocalEndpoint).Port, 7, 42, swapped);
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            async Task Serve()
            {
                using var peer = await listener.AcceptTcpClientAsync(limit.Token);
                using var stream = peer.GetStream();
                for (int i=1; i<=3; i++)
                {
                    byte[] request = new byte[12];
                    await stream.ReadExactlyAsync(request, limit.Token);
                    byte[] expected = [0,(byte)i,0,0,0,6,7,3,0,42,0,6];
                    if (!request.SequenceEqual(expected)) throw new Exception("Unexpected Modbus request.");
                    byte[] response = new byte[21];
                    Array.Copy(request, response, 8);
                    response[5]=15;
                    response[8]=12;
                    float[] values = [2.5f, -1.25f, i*0.5f];
                    for (int k=0; k<values.Length; k++)
                    {
                        uint bits = BitConverter.SingleToUInt32Bits(values[k]);
                        if (swapped) bits = (bits << 16) | (bits >> 16);
                        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(9+4*k), bits);
                    }
                    await stream.WriteAsync(response, limit.Token);
                }
            }
            var service = Serve();
            var samples = new List<AcquiredSample>();
            var capture = await new PlcAcquisition().CaptureAsync(target, new AcquisitionPlan(3, 100), samples.Add, null, limit.Token);
            await service;
            if (capture.StopReason != "sample_limit")
                throw new Exception("Unexpected capture stop " + capture.StopReason);
            if (samples.Count != 3 || samples[0].Sp != 2.5f || samples[0].Pv != -1.25f || samples[2].Mv != 1.5f)
                throw new Exception("Unexpected decoded values.");
            var store = new SqliteProjectStore(db);
            var origin = new AcquisitionOrigin(null, capture.StopReason, "receiver_utc_midpoint", target,
                new AcquisitionPlan(3, 100), capture.Statistics);
            var saved = await AcquisitionArchive.SaveAsync(project, store, engine, samples, origin, limit.Token);
            var reopened = new SqliteProjectStore(db).ReadImport(project, saved.Summary.Id);
            if (System.Text.Json.JsonSerializer.Serialize(reopened.Acquisition) != System.Text.Json.JsonSerializer.Serialize(origin) ||
                reopened.Acquisition?.Target != target || reopened.Result.Quality?.ValidValues != 3 ||
                reopened.Result.Quality.InvalidTimestamps != 0 || !saved.Original.SequenceEqual(reopened.Original))
                throw new Exception("Capture did not survive the study archive round trip.");
        }
    }

    private static async Task CheckIdentification(string db, Guid project, PythonDiagnostics engine)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var target = Target(((IPEndPoint)listener.LocalEndpoint).Port, 1, 0, false);
        var store = new SqliteProjectStore(db);
        async Task<(CsvImport Live, CsvImport File)> Trial(float amplitude)
        {
            const int count = 44;
            var samples = new List<AcquiredSample>();
            var reading = new PlcAcquisition().CaptureAsync(target, new AcquisitionPlan(count, 100), samples.Add, null, limit.Token);
            using var peer = await listener.AcceptTcpClientAsync(limit.Token);
            using var stream = peer.GetStream();
            var transmitted = new List<(float Mv, float Pv)>();
            var elapsed = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < count; i++)
            {
                var request = new byte[12];
                await stream.ReadExactlyAsync(request, limit.Token);
                if (i == 6) elapsed.Start();
                float mv = i < 6 ? 0 : amplitude;
                float pv = (float)(4 - 2 * mv * (1 - Math.Exp(-Math.Max(0, elapsed.Elapsed.TotalSeconds - 0.25) / 0.55)));
                transmitted.Add((mv, pv));
                byte[] reply = new byte[21];
                Array.Copy(request, reply, 8);
                reply[5] = 15;
                reply[8] = 12;
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(9), 4);
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(13), pv);
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(17), mv);
                await stream.WriteAsync(reply, limit.Token);
            }
            var captured = await reading;
            if (captured.StopReason != "sample_limit" || samples.Count != count)
                throw new Exception("Simulated trial capture incomplete: " + captured.StopReason);
            var saved = await AcquisitionArchive.SaveAsync(project, store, engine, samples,
                new(null, captured.StopReason, "receiver_utc_midpoint", target, new AcquisitionPlan(count, 100), captured.Statistics), limit.Token);
            var live = new SqliteProjectStore(db).ReadImport(project, saved.Summary.Id);
            // Build an independent historian file from values sent by the server.
            // Only the receiver timestamps are shared with the connected path.
            var csv = new System.Text.StringBuilder("MV;timestamp;SP;PV\n");
            for (int i = 0; i < count; i++)
            {
                var timestamp = samples[i].ReceivedAt.UtcDateTime;
                var (mv, pv) = transmitted[i];
                csv.Append(FormattableString.Invariant($"{(double)mv:R};{timestamp:yyyy-MM-ddTHH:mm:ss.ffffffZ};4;{(double)pv:R}\n"));
            }
            var csvPath = Path.Combine(Path.GetDirectoryName(db)!, $"historian-{saved.Summary.Id}.csv");
            await File.WriteAllTextAsync(csvPath, csv.ToString(), limit.Token);
            var workspace = new CsvWorkspace(store.List().Single(p => p.Id == project), store, engine);
            await workspace.InspectAsync(csvPath, ";", limit.Token);
            var imported = await workspace.AnalyzeAndSaveAsync(new("timestamp", "PV", "PV", "bar"), limit.Token);
            var file = new SqliteProjectStore(db).ReadImport(project, imported.Summary.Id);
            if (file.Acquisition is not null || live.Acquisition is null ||
                file.Summary.Sha256 == live.Summary.Sha256 || file.Summary.FileName != Path.GetFileName(csvPath) ||
                file.Result.RowCount != count || live.Result.RowCount != count ||
                file.Result.Quality is null || file.Result.Quality != live.Result.Quality)
                throw new Exception("CSV and Modbus must preserve distinct origins and identical PV quality.");
            return (live, file);
        }
        var train = await Trial(1);
        var check = await Trial(-1);
        var columns = new StepColumns("timestamp", "MV", "PV");
        var (fit, _) = await engine.IdentifyAsync(ModelFamily.FOPDT, new(train.Live, columns), new(check.Live, columns),
            0.005, 0.01, limit.Token);
        if (Math.Abs(fit.Gain + 2) > 0.1 || fit.TauSeconds is not double tau || Math.Abs(tau - 0.55) > 0.12 ||
            Math.Abs(fit.DelaySeconds - 0.25) > 0.12 || fit.Validation.Rmse > 0.12)
            throw new Exception("Independent simulated acquisition trials failed the FOPDT reference case.");
        var (settings, _) = await engine.CalculatePiAsync(fit.Gain, tau, fit.DelaySeconds, 1, limit.Token);
        if (settings.Simc.Kp >= 0 || settings.Simc.TiSeconds <= 0)
            throw new Exception("Live trial tuning has invalid controller action.");
        var (offline, _) = await engine.IdentifyAsync(ModelFamily.FOPDT,
            new(train.File, columns), new(check.File, columns), 0.005, 0.01, limit.Token);
        if (offline.TauSeconds is not double offlineTau || Math.Abs(offlineTau - tau) > 1e-9 ||
            Math.Abs(offline.Gain - fit.Gain) > 1e-9 || Math.Abs(offline.DelaySeconds - fit.DelaySeconds) > 1e-9 ||
            Math.Abs(offline.Validation.Rmse - fit.Validation.Rmse) > 1e-9)
            throw new Exception("CSV and Modbus returned different FOPDT models or validation errors.");
        var (offlineSettings, _) = await engine.CalculatePiAsync(offline.Gain, offlineTau, offline.DelaySeconds, 1, limit.Token);
        if (offlineSettings.Simc != settings.Simc || offlineSettings.Imc != settings.Imc)
            throw new Exception("Identical timestamped signals must produce identical PI coefficients.");
    }

    /// <summary>Process simulated per reading count: MV +2 at reading 30, back to 0 at 90; first order tau 8 readings, delay 2.</summary>
    private sealed class SimulatedProcessReader(int[] counter) : IPlcReader
    {
        public Task ConnectAsync(CancellationToken token) => Task.CompletedTask;

        public Task<(double Sp, double Pv, double Mv)> ReadAsync(CancellationToken token)
        {
            int n = ++counter[0];
            double mv = n >= 30 && n < 90 ? 2 : 0, level = 0;
            if (n >= 32) level += 2 * (1 - Math.Exp(-(n - 32) / 8.0));
            if (n >= 92) level -= 2 * (1 - Math.Exp(-(n - 92) / 8.0));
            return Task.FromResult((8.0, 8 - 1.5 * level, mv));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>History mode end to end: recorder → archive in segments → reopen → find steps → identify across segments.</summary>
    private static async Task CheckHistorian(string db, Guid project, PythonDiagnostics engine)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var target = Target(1, 1, 0, false);
        var store = new SqliteProjectStore(db);
        var archived = new List<CsvImport>();
        var recorder = new HistorianRecorder(_ => new SimulatedProcessReader(new int[1]));
        var result = await recorder.RunAsync(target, new HistorianPlan(100, 140, SegmentSamples: 100, OverlapSamples: 20),
            async segment => archived.Add(await AcquisitionArchive.SaveAsync(project, store, engine, segment.Samples.ToList(), segment.Origin, limit.Token)),
            null, limit.Token);
        if (result.StopReason != "sample_limit" || archived.Count != 2 || result.SegmentsSaved != 2)
            throw new Exception($"History recording incomplete: {result.StopReason} {archived.Count}");
        var first = new SqliteProjectStore(db).ReadImport(project, archived[0].Summary.Id);
        var second = new SqliteProjectStore(db).ReadImport(project, archived[1].Summary.Id);
        if (!first.Summary.FileName.StartsWith("historique-") || !first.Summary.FileName.EndsWith("-001.csv") ||
            !second.Summary.FileName.EndsWith("-002.csv") || first.Acquisition?.Session is not { SegmentIndex: 1, OverlapSamples: 0 } ||
            second.Acquisition?.Session is not { SegmentIndex: 2, OverlapSamples: 20 } ||
            first.Acquisition.Session.SessionId != second.Acquisition.Session.SessionId ||
            first.Result.RowCount != 100 || second.Result.RowCount != 60)
            throw new Exception("History segments lost their session, overlap or size after reopening.");
        var firstLines = System.Text.Encoding.UTF8.GetString(first.Original).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var secondLines = System.Text.Encoding.UTF8.GetString(second.Original).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (!firstLines.TakeLast(20).SequenceEqual(secondLines.Skip(1).Take(20)))
            throw new Exception("The overlap must repeat the previous segment's last 20 readings exactly.");

        var columns = new StepColumns("timestamp", "MV", "PV");
        var upSearch = await engine.FindStepsAsync(new(first, columns), 0.01, 0.01, limit.Token);
        var downSearch = await engine.FindStepsAsync(new(second, columns), 0.01, 0.01, limit.Token);
        // Reading 90 (MV back to 0) falls near the end of segment 1 (10 readings after it: too short) but whole in segment 2.
        if (upSearch.Candidates.Select(c => c.StepRow).SequenceEqual(new[] { 29 }) is false ||
            upSearch.Rejected.Single(r => r.StepRow == 89).Reason != "response_window_too_short" ||
            downSearch.Candidates.Select(c => c.StepRow).SequenceEqual(new[] { 9 }) is false)
            throw new Exception("The overlap must keep each step whole in one segment.");
        var up = upSearch.Candidates[0];
        var down = downSearch.Candidates[0];
        // The two segments overlap by design (readings 81-100): a first range that reaches into the overlap
        // coincides in time with the start of the second one and must be refused as not independent.
        try
        {
            await engine.AssessAsync(ModelFamily.FOPDT, new(first, columns, new StepWindow(up.RowStart, up.RowEnd)),
                new(second, columns, new StepWindow(down.RowStart, down.RowEnd)), 0.01, 0.01, new ComparisonCriteria(0.05, 0.9), limit.Token);
            throw new Exception("Trials that coincide in time inside the overlap must be refused.");
        }
        catch (CsvInputException error) when (error.Code == "fit_independence") { }
        // Ending the first range before the overlap (reading 80) restores two disjoint trials.
        var (fit, assessment, _) = await engine.AssessAsync(ModelFamily.FOPDT,
            new(first, columns, new StepWindow(up.RowStart, 79)), new(second, columns, new StepWindow(down.RowStart, down.RowEnd)),
            0.01, 0.01, new ComparisonCriteria(0.05, 0.9), limit.Token);
        if (!assessment.Accepted || Math.Abs(fit.Gain + 1.5) > 0.05 || fit.TauSeconds is not double tau || Math.Abs(tau - 0.8) > 0.15)
            throw new Exception($"History steps from two segments must identify the process: {assessment.Accepted} {fit.Gain} {fit.TauSeconds}");
    }

    private static AcquisitionTarget Target(int port, byte unit, int first, bool lowWordFirst) =>
        new(PlcProtocol.ModbusTcp, "127.0.0.1", port, new(first, PlcDataType.Real32), new(first + 2, PlcDataType.Real32),
            new(first + 4, PlcDataType.Real32), "bar", "%", lowWordFirst ? ByteOrder.CDAB : ByteOrder.ABCD, unit);

    private static async Task CheckInterruptedSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var target = Target(((IPEndPoint)listener.LocalEndpoint).Port, 1, 0, false);
        var samples = new List<AcquiredSample>();
        var capture = new PlcAcquisition().CaptureAsync(target, new AcquisitionPlan(2, 100), samples.Add, null, stop.Token);
        using var peer = await listener.AcceptTcpClientAsync(stop.Token);
        using var stream = peer.GetStream();
        byte[] request = new byte[12];
        await stream.ReadExactlyAsync(request, stop.Token);
        byte[] good = new byte[21];
        Array.Copy(request, good, 8);
        good[5] = 15;
        good[8] = 12;
        await stream.WriteAsync(good, stop.Token);
        await stream.ReadExactlyAsync(request, stop.Token);
        good[1] = 77;
        await stream.WriteAsync(good, stop.Token);
        var result = await capture;
        // A mismatched transaction is never decoded: the valid prefix is kept and the slot is a recorded gap.
        if (samples.Count != 1 || result.Statistics.Gaps.Length != 1 || result.Statistics.Gaps[0].Reason != "modbus_frame")
            throw new Exception("Mismatched transaction must be rejected as a gap and preserve the valid prefix.");
    }
}
