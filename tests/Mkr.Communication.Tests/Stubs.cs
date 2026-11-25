using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Mkr.Core;

/// <summary>
/// Scriptable Modbus TCP server. Every received frame is recorded so tests can prove that only
/// read functions are emitted. The behaviour callback receives the request number (1-based, all
/// connections) and returns the reply bytes, null to stay silent, or an empty array to close.
/// </summary>
internal sealed class ModbusStub : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private int _requests;
    public ConcurrentQueue<byte[]> Frames { get; } = new();
    public int Connections;
    public Dictionary<int, ushort> Holding { get; } = new();
    public Dictionary<int, ushort> Input { get; } = new();
    public Func<int, byte[], Task<byte[]?>>? Behaviour { get; set; }
    public bool Accepting { get; set; } = true;
    /// <summary>Set by a Behaviour callback: close the connection once the current reply has been written (truncation tests).</summary>
    public bool CloseAfterReply { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    /// <summary>Sends each reply in two TCP segments to exercise frame reassembly.</summary>
    public bool SplitReplies { get; set; }

    public ModbusStub()
    {
        _listener.Start();
        _loop = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Place(int offset, double value, PlcDataType type, ByteOrder order, bool input = false)
    {
        var wire = PlcDecoding.Encode(value, type, order);
        var table = input ? Input : Holding;
        for (int i = 0; i < wire.Length / 2; i++)
            table[offset + i] = BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(2 * i));
    }

    public byte[] Normal(byte[] request)
    {
        byte function = request[7];
        int start = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(8));
        int count = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(10));
        var table = function == 4 ? Input : Holding;
        var reply = new byte[9 + 2 * count];
        Array.Copy(request, reply, 8);
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(4), (ushort)(3 + 2 * count));
        reply[8] = (byte)(2 * count);
        for (int i = 0; i < count; i++)
            BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(9 + 2 * i), table.GetValueOrDefault(start + i));
        return reply;
    }

    public static byte[] Exception(byte[] request, byte code)
    {
        var reply = new byte[9];
        Array.Copy(request, reply, 8);
        BinaryPrimitives.WriteUInt16BigEndian(reply.AsSpan(4), 3);
        reply[7] = (byte)(request[7] | 0x80);
        reply[8] = code;
        return reply;
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient peer;
            try { peer = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch { return; }
            if (!Accepting) { peer.Dispose(); continue; }
            Interlocked.Increment(ref Connections);
            _ = Task.Run(() => ServeAsync(peer));
        }
    }

    private async Task ServeAsync(TcpClient peer)
    {
        using var _ = peer;
        var stream = peer.GetStream();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var request = new byte[12];
                await stream.ReadExactlyAsync(request, _stop.Token);
                Frames.Enqueue(request);
                int number = Interlocked.Increment(ref _requests);
                if (Delay > TimeSpan.Zero) await Task.Delay(Delay, _stop.Token);
                var reply = Behaviour is null ? Normal(request) : await Behaviour(number, request);
                if (reply is null) continue;
                if (reply.Length == 0) return;
                if (SplitReplies)
                {
                    await stream.WriteAsync(reply.AsMemory(0, 5), _stop.Token);
                    await stream.FlushAsync(_stop.Token);
                    await Task.Delay(20, _stop.Token);
                    await stream.WriteAsync(reply.AsMemory(5), _stop.Token);
                }
                else await stream.WriteAsync(reply, _stop.Token);
                if (CloseAfterReply) { CloseAfterReply = false; return; }
            }
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try { await _loop; } catch { }
    }
}

/// <summary>
/// Minimal scriptable S7comm server (COTP + setup communication + read var) used to inject
/// refusals and audit every S7 function received. Interoperability is tested separately against Snap7.
/// </summary>
internal sealed class S7Stub : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    public ConcurrentQueue<byte> Functions { get; } = new();
    public ConcurrentQueue<ushort> CalledTsaps { get; } = new();
    public Dictionary<(byte Area, int Db), byte[]> Memory { get; } = new();
    public byte ItemCode { get; set; } = 0xFF;
    public (byte Class, byte Code) HeaderError { get; set; }
    /// <summary>Hostile-frame hook: receives every outgoing TPKT frame and returns what to send and whether to close afterwards.</summary>
    public Func<byte[], (byte[] Bytes, bool Close)>? CorruptFrame { get; set; }

    public S7Stub()
    {
        _listener.Start();
        _loop = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient peer;
            try { peer = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch { return; }
            _ = Task.Run(() => ServeAsync(peer));
        }
    }

    private async Task<byte[]> ReadTpkt(NetworkStream stream)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, _stop.Token);
        var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2)) - 4];
        await stream.ReadExactlyAsync(body, _stop.Token);
        return body;
    }

    private async Task WriteTpkt(NetworkStream stream, byte[] body)
    {
        var frame = new byte[4 + body.Length];
        frame[0] = 3;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)frame.Length);
        body.CopyTo(frame, 4);
        if (CorruptFrame is { } corrupt)
        {
            var (bytes, close) = corrupt(frame);
            await stream.WriteAsync(bytes, _stop.Token);
            await stream.FlushAsync(_stop.Token);
            if (close) throw new OperationCanceledException();       // caught by ServeAsync: the peer is closed
            return;
        }
        await stream.WriteAsync(frame, _stop.Token);
    }

    private byte[] Ack(byte[] job, byte[] parameters, byte[] data, (byte, byte) error)
    {
        var frame = new byte[3 + 12 + parameters.Length + data.Length];
        frame[0] = 2; frame[1] = 0xF0; frame[2] = 0x80; frame[3] = 0x32; frame[4] = 3;
        frame[7] = job[7]; frame[8] = job[8];
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(9), (ushort)parameters.Length);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(11), (ushort)data.Length);
        frame[13] = error.Item1; frame[14] = error.Item2;
        parameters.CopyTo(frame, 15);
        data.CopyTo(frame, 15 + parameters.Length);
        return frame;
    }

    private async Task ServeAsync(TcpClient peer)
    {
        using var _ = peer;
        var stream = peer.GetStream();
        try
        {
            var request = await ReadTpkt(stream);
            if (request[1] != 0xE0) return;
            CalledTsaps.Enqueue(BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(16)));
            await WriteTpkt(stream, [0x11, 0xD0, 0, 1, 0, 1, 0, 0xC0, 1, 0x0A, 0xC1, 2, 1, 0, 0xC2, 2, 1, 1]);
            while (!_stop.IsCancellationRequested)
            {
                var job = await ReadTpkt(stream);
                byte function = job[13];
                Functions.Enqueue(function);
                if (function == 0xF0)
                {
                    await WriteTpkt(stream, Ack(job, [0xF0, 0, 0, 1, 0, 1, 0x01, 0xE0], [], default));
                    continue;
                }
                if (HeaderError != default)
                {
                    await WriteTpkt(stream, Ack(job, [], [], HeaderError));
                    continue;
                }
                int items = job[14];
                var data = new List<byte>();
                for (int i = 0; i < items; i++)
                {
                    var item = job.AsSpan(15 + 12 * i, 12);
                    int length = BinaryPrimitives.ReadUInt16BigEndian(item[4..]);
                    int db = BinaryPrimitives.ReadUInt16BigEndian(item[6..]);
                    byte area = item[8];
                    int offset = ((item[9] << 16) | (item[10] << 8) | item[11]) / 8;
                    if (ItemCode != 0xFF || !Memory.TryGetValue((area, db), out var memory) || offset + length > memory.Length)
                    {
                        data.AddRange([ItemCode != 0xFF ? ItemCode : (byte)0x0A, 0, 0, 0]);
                        continue;
                    }
                    data.AddRange([0xFF, 0x04, (byte)((length * 8) >> 8), (byte)(length * 8)]);
                    data.AddRange(memory.AsSpan(offset, length).ToArray());
                    if (length % 2 == 1 && i < items - 1) data.Add(0);
                }
                await WriteTpkt(stream, Ack(job, [0x04, (byte)items], data.ToArray(), default));
            }
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try { await _loop; } catch { }
    }
}
