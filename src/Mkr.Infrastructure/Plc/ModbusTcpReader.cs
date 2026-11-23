using System.Buffers.Binary;
using System.Net.Sockets;
using Mkr.Core;

namespace Mkr.Infrastructure.Plc;

/// <summary>
/// Read-only Modbus TCP client for Schneider Electric controllers.
/// The only function codes ever transmitted are 0x03 (read holding registers, %MW) and 0x04 (read input registers).
/// </summary>
public sealed class ModbusTcpReader : IPlcReader
{
    /// <summary>Exhaustive list of function codes this client can emit.</summary>
    public static readonly IReadOnlySet<byte> EmittedFunctionCodes = new HashSet<byte> { 0x03, 0x04 };

    private const int MaximumRegistersPerRequest = 125;
    private readonly AcquisitionTarget _target;
    private readonly (ushort Start, ushort Count)[] _requests;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ushort _transaction;

    public ModbusTcpReader(AcquisitionTarget target)
    {
        if (target.Protocol != PlcProtocol.ModbusTcp) throw new ArgumentException("Cible Modbus attendue.");
        _target = target;
        var signals = target.Signals.Select(s => (Start: s.Address.Offset, End: s.Address.Offset + s.Address.ByteLength / 2 - 1)).ToArray();
        int first = signals.Min(s => s.Start), last = signals.Max(s => s.End);
        // One request when possible so the three values are sampled by the same PLC scan read.
        _requests = last - first + 1 <= MaximumRegistersPerRequest
            ? [((ushort)first, (ushort)(last - first + 1))]
            : signals.Select(s => ((ushort)s.Start, (ushort)(s.End - s.Start + 1))).ToArray();
    }

    /// <summary>Number of Modbus requests needed for one reading (1 when the signals span at most 125 registers).</summary>
    public int RequestsPerReading => _requests.Length;

    public async Task ConnectAsync(CancellationToken token)
    {
        await DisposeAsync().ConfigureAwait(false);
        _client = await PlcReaders.ConnectTcpAsync(_target.Host, _target.Port, token).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    public async Task<(double Sp, double Pv, double Mv)> ReadAsync(CancellationToken token)
    {
        var stream = _stream ?? throw new PlcReadException("not_connected", "Session Modbus non ouverte.", true);
        var registers = new Dictionary<int, byte[]>();
        foreach (var (start, count) in _requests)
        {
            var data = await PlcReaders.ExchangeAsync(t => ReadRegistersAsync(stream, start, count, t), token).ConfigureAwait(false);
            for (int i = 0; i < count; i++) registers[start + i] = data[(2 * i)..(2 * i + 2)];
        }
        double Value(string role, SignalAddress address)
        {
            var wire = new byte[address.ByteLength];
            for (int i = 0; i < address.ByteLength / 2; i++) registers[address.Offset + i].CopyTo(wire, 2 * i);
            return PlcReaders.Checked(PlcDecoding.Decode(wire, address.Type, _target.Order), role);
        }
        return (Value("SP", _target.Sp), Value("PV", _target.Pv), Value("MV", _target.Mv));
    }

    private async Task<byte[]> ReadRegistersAsync(NetworkStream stream, ushort start, ushort count, CancellationToken token)
    {
        byte function = _target.Table == ModbusTable.HoldingRegisters ? (byte)0x03 : (byte)0x04;
        ushort transaction = ++_transaction;
        byte[] request = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(request, transaction);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4), 6);
        request[6] = _target.UnitId;
        request[7] = function;
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8), start);
        BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10), count);
        await stream.WriteAsync(request, token).ConfigureAwait(false);

        byte[] header = new byte[7];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        if (BinaryPrimitives.ReadUInt16BigEndian(header) != transaction || header[2] != 0 || header[3] != 0 ||
            header[6] != _target.UnitId || length is < 3 or > 254)
            throw new PlcReadException("modbus_frame", "Réponse Modbus incohérente (transaction, protocole ou unité).", true);
        var pdu = new byte[length - 1];
        await stream.ReadExactlyAsync(pdu, token).ConfigureAwait(false);
        if (pdu[0] == (function | 0x80) && pdu.Length == 2)
            throw Exception(pdu[1]);
        if (pdu[0] != function || pdu.Length != 2 + 2 * count || pdu[1] != 2 * count)
            throw new PlcReadException("modbus_frame", "Fonction ou longueur de réponse Modbus inattendue.", true);
        return pdu[2..];
    }

    private static PlcReadException Exception(byte code) => code switch
    {
        0x01 => new("modbus_exception_01", "Exception Modbus 01 : fonction non prise en charge par l’équipement.", false),
        0x02 => new("modbus_exception_02", "Exception Modbus 02 : adresse de registre non autorisée. Vérifiez les offsets (base 0) et la taille de la zone %MW.", false),
        0x03 => new("modbus_exception_03", "Exception Modbus 03 : valeur de requête refusée.", false),
        0x04 => new("modbus_exception_04", "Exception Modbus 04 : défaut de l’équipement.", true),
        0x06 => new("modbus_exception_06", "Exception Modbus 06 : équipement occupé.", true),
        0x0A => new("modbus_exception_0A", "Exception Modbus 0A : passerelle sans chemin.", true),
        0x0B => new("modbus_exception_0B", "Exception Modbus 0B : la cible derrière la passerelle ne répond pas.", true),
        _ => new($"modbus_exception_{code:X2}", $"Exception Modbus {code:X2}.", false)
    };

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return ValueTask.CompletedTask;
    }
}
