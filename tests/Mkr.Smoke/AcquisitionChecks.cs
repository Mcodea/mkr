using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Mkr.Core;
using Mkr.Infrastructure;

internal static class AcquisitionChecks
{
    internal static async Task Run(string db, Guid project, ICsvEngine engine)
    {
        await CheckInterruptedSession();
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
