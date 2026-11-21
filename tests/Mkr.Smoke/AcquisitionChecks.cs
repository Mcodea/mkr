using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Mkr.Core;
using Mkr.Infrastructure;

internal static class AcquisitionChecks
{
    internal static async Task Run(string db, Guid project, PythonDiagnostics engine)
    {
        await CheckInterruptedSession();
        await CheckIdentification(db, project, engine);
        foreach (bool swapped in new[] { false, true })
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var source = new RegisterSource("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                7, 42, swapped, "bar", "%", 100);
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
            await new ModbusAcquisition().CaptureAsync(source, 3, samples.Add, limit.Token);
            await service;
            if (samples.Count != 3 || samples[0].Sp != 2.5f || samples[0].Pv != -1.25f || samples[2].Mv != 1.5f)
                throw new Exception("Unexpected decoded values.");
            var store = new SqliteProjectStore(db);
            var origin = new AcquisitionOrigin(source, "sample_limit", "receiver_utc");
            var saved = await AcquisitionArchive.SaveAsync(project, store, engine, samples, origin, limit.Token);
            var reopened = new SqliteProjectStore(db).ReadImport(project, saved.Summary.Id);
            if (reopened.Acquisition != origin || reopened.Result.Quality?.ValidValues != 3 ||
                reopened.Result.Quality.InvalidTimestamps != 0 || !saved.Original.SequenceEqual(reopened.Original))
                throw new Exception("Capture did not survive the study archive round trip.");
        }
    }

    private static async Task CheckIdentification(string db, Guid project, PythonDiagnostics engine)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var source = new RegisterSource("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            1, 0, false, "bar", "%", 100);
        var store = new SqliteProjectStore(db);
        async Task<CsvImport> Trial(float amplitude)
        {
            const int count = 44;
            var samples = new List<AcquiredSample>();
            var reading = new ModbusAcquisition().CaptureAsync(source, count, samples.Add, limit.Token);
            using var peer = await listener.AcceptTcpClientAsync(limit.Token);
            using var stream = peer.GetStream();
            var elapsed = new System.Diagnostics.Stopwatch();
            for (int i = 0; i < count; i++)
            {
                var request = new byte[12];
                await stream.ReadExactlyAsync(request, limit.Token);
                if (i == 6) elapsed.Start();
                float mv = i < 6 ? 0 : amplitude;
                float pv = (float)(4 - 2 * mv * (1 - Math.Exp(-Math.Max(0, elapsed.Elapsed.TotalSeconds - 0.25) / 0.55)));
                byte[] reply = new byte[21];
                Array.Copy(request, reply, 8);
                reply[5] = 15;
                reply[8] = 12;
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(9), 4);
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(13), pv);
                BinaryPrimitives.WriteSingleBigEndian(reply.AsSpan(17), mv);
                await stream.WriteAsync(reply, limit.Token);
            }
            await reading;
            var saved = await AcquisitionArchive.SaveAsync(project, store, engine, samples,
                new(source, "sample_limit", "receiver_utc"), limit.Token);
            return new SqliteProjectStore(db).ReadImport(project, saved.Summary.Id);
        }
        var train = await Trial(1);
        var check = await Trial(-1);
        var columns = new StepColumns("timestamp", "MV", "PV");
        var (fit, _) = await engine.IdentifyAsync(ModelFamily.FOPDT, new(train, columns), new(check, columns),
            0.005, 0.01, limit.Token);
        if (Math.Abs(fit.Gain + 2) > 0.1 || fit.TauSeconds is not double tau || Math.Abs(tau - 0.55) > 0.12 ||
            Math.Abs(fit.DelaySeconds - 0.25) > 0.12 || fit.Validation.Rmse > 0.12)
            throw new Exception("Independent simulated acquisition trials failed the FOPDT reference case.");
        var (settings, _) = await engine.CalculatePiAsync(fit.Gain, tau, fit.DelaySeconds, 1, limit.Token);
        if (settings.Simc.Kp >= 0 || settings.Simc.TiSeconds <= 0)
            throw new Exception("Live trial tuning has invalid controller action.");
    }

    private static async Task CheckInterruptedSession()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var source = new RegisterSource("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            1, 0, false, "bar", "%", 100);
        var samples = new List<AcquiredSample>();
        var capture = new ModbusAcquisition().CaptureAsync(source, 4, samples.Add, stop.Token);
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
        try { await capture; }
        catch (InvalidDataException) when (samples.Count == 1) { return; }
        throw new Exception("Mismatched transaction must terminate capture and preserve its valid prefix.");
    }
}
