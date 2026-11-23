using System.Buffers.Binary;
using System.Net.Sockets;
using Mkr.Core;

namespace Mkr.Infrastructure.Plc;

/// <summary>
/// Read-only S7comm client (ISO-on-TCP, RFC 1006, port 102) for Siemens S7-1200/1500 and S7-300/400.
/// Frames sent: COTP connection request, S7 "setup communication" (0xF0) and S7 "read variable" (0x04) jobs.
/// No write (0x05), PLC control, upload/download or block function is implemented.
/// S7-1200/1500 prerequisites: PUT/GET access enabled in the CPU protection settings and the values
/// placed in a data block with "optimized block access" disabled (absolute offsets).
/// </summary>
public sealed class S7Reader : IPlcReader
{
    /// <summary>Exhaustive list of S7 job functions this client can emit.</summary>
    public static readonly IReadOnlySet<byte> EmittedFunctions = new HashSet<byte> { 0xF0, 0x04 };

    private readonly AcquisitionTarget _target;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ushort _reference;

    public S7Reader(AcquisitionTarget target)
    {
        if (target.Protocol != PlcProtocol.S7) throw new ArgumentException("Cible S7 attendue.");
        _target = target;
    }

    /// <summary>Remote TSAP for a PG connection: 0x01, then rack × 32 + slot.</summary>
    public static ushort RemoteTsap(int rack, int slot) => (ushort)(0x0100 | (rack << 5) | slot);

    public int NegotiatedPduLength { get; private set; }

    public async Task ConnectAsync(CancellationToken token)
    {
        await DisposeAsync().ConfigureAwait(false);
        _client = await PlcReaders.ConnectTcpAsync(_target.Host, _target.Port, token).ConfigureAwait(false);
        _stream = _client.GetStream();
        var stream = _stream;
        await PlcReaders.ExchangeAsync(async t =>
        {
            ushort remote = RemoteTsap(_target.Rack, _target.Slot);
            byte[] cotp =
            [
                0x11, 0xE0, 0x00, 0x00, 0x00, 0x01, 0x00,
                0xC0, 0x01, 0x0A,                         // TPDU size 1024
                0xC1, 0x02, 0x01, 0x00,                   // calling TSAP
                0xC2, 0x02, (byte)(remote >> 8), (byte)remote // called TSAP
            ];
            await WriteTpktAsync(stream, cotp, t).ConfigureAwait(false);
            var reply = await ReadTpktAsync(stream, t).ConfigureAwait(false);
            if (reply.Length < 2 || reply[1] != 0xD0)
                throw new PlcReadException("s7_connection_refused",
                    "L’automate a refusé la connexion ISO (rack ou emplacement incorrect ?).", false);

            byte[] setup = Job(0xF0, [0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x03, 0xC0], []);
            await WriteTpktAsync(stream, setup, t).ConfigureAwait(false);
            var (parameters, _) = ParseAck(await ReadTpktAsync(stream, t).ConfigureAwait(false));
            if (parameters.Length < 8 || parameters[0] != 0xF0)
                throw new PlcReadException("s7_frame", "Réponse S7 de négociation inattendue.", false);
            NegotiatedPduLength = BinaryPrimitives.ReadUInt16BigEndian(parameters.AsSpan(6));
            if (NegotiatedPduLength < 64)
                throw new PlcReadException("s7_frame", "Taille de PDU S7 négociée trop faible.", false);
            return 0;
        }, token).ConfigureAwait(false);
    }

    public async Task<(double Sp, double Pv, double Mv)> ReadAsync(CancellationToken token)
    {
        var stream = _stream ?? throw new PlcReadException("not_connected", "Session S7 non ouverte.", true);
        var signals = _target.Signals.ToArray();
        var parameter = new List<byte> { 0x04, (byte)signals.Length };
        foreach (var (_, address) in signals)
        {
            int bit = address.Offset * 8;
            parameter.AddRange(
            [
                0x12, 0x0A, 0x10, 0x02,                                   // any-pointer, transport size BYTE
                0x00, (byte)address.ByteLength,                           // number of bytes
                (byte)(DbNumber >> 8), (byte)DbNumber,
                AreaCode,
                (byte)(bit >> 16), (byte)(bit >> 8), (byte)bit
            ]);
        }
        var job = Job(0x04, parameter.ToArray(), []);
        var data = await PlcReaders.ExchangeAsync(async t =>
        {
            await WriteTpktAsync(stream, job, t).ConfigureAwait(false);
            var (parameters, payload) = ParseAck(await ReadTpktAsync(stream, t).ConfigureAwait(false));
            if (parameters.Length != 2 || parameters[0] != 0x04 || parameters[1] != signals.Length)
                throw new PlcReadException("s7_frame", "Réponse S7 de lecture inattendue.", true);
            return payload;
        }, token).ConfigureAwait(false);

        var values = new double[signals.Length];
        int position = 0;
        for (int i = 0; i < signals.Length; i++)
        {
            var (role, address) = signals[i];
            if (position + 4 > data.Length)
                throw new PlcReadException("s7_frame", "Réponse S7 tronquée.", true);
            byte code = data[position];
            if (code != 0xFF) throw ItemError(code, role);
            byte transport = data[position + 1];
            int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2));
            if (transport is 0x03 or 0x04 or 0x05) length /= 8; // length expressed in bits
            if (length != address.ByteLength || position + 4 + length > data.Length)
                throw new PlcReadException("s7_frame", $"Longueur S7 inattendue pour {role}.", true);
            var wire = data.AsSpan(position + 4, length);
            values[i] = PlcReaders.Checked(PlcDecoding.Decode(wire, address.Type, ByteOrder.ABCD), role);
            position += 4 + length + (length % 2 == 1 && i < signals.Length - 1 ? 1 : 0);
        }
        return (values[0], values[1], values[2]);
    }

    private int DbNumber => _target.Area == S7Area.DataBlock ? _target.DataBlock : 0;
    private byte AreaCode => _target.Area == S7Area.DataBlock ? (byte)0x84 : (byte)0x83;

    private byte[] Job(byte function, byte[] parameter, byte[] data)
    {
        if (!EmittedFunctions.Contains(function)) throw new InvalidOperationException("Fonction S7 non autorisée.");
        ushort reference = ++_reference;
        var frame = new byte[3 + 10 + parameter.Length + data.Length];
        frame[0] = 0x02; frame[1] = 0xF0; frame[2] = 0x80;            // COTP data, last unit
        frame[3] = 0x32; frame[4] = 0x01;                              // S7 job
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(7), reference);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(9), (ushort)parameter.Length);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(11), (ushort)data.Length);
        parameter.CopyTo(frame, 13);
        data.CopyTo(frame, 13 + parameter.Length);
        return frame;
    }

    private (byte[] Parameters, byte[] Data) ParseAck(byte[] cotp)
    {
        if (cotp.Length < 3 + 12 || cotp[1] != 0xF0 || cotp[3] != 0x32 || cotp[4] != 0x03)
            throw new PlcReadException("s7_frame", "Trame S7 inattendue.", true);
        if (BinaryPrimitives.ReadUInt16BigEndian(cotp.AsSpan(7)) != _reference)
            throw new PlcReadException("s7_frame", "Référence de PDU S7 incohérente.", true);
        int parameterLength = BinaryPrimitives.ReadUInt16BigEndian(cotp.AsSpan(9));
        int dataLength = BinaryPrimitives.ReadUInt16BigEndian(cotp.AsSpan(11));
        byte errorClass = cotp[13], errorCode = cotp[14];
        if (errorClass != 0 || errorCode != 0)
            throw new PlcReadException($"s7_error_{errorClass:X2}{errorCode:X2}", errorClass == 0x81 && errorCode == 0x04
                ? "La CPU refuse la lecture S7 (erreur 0x8104). Activez l’accès PUT/GET dans la protection de la CPU."
                : $"Erreur S7 classe 0x{errorClass:X2}, code 0x{errorCode:X2}.", false);
        if (15 + parameterLength + dataLength != cotp.Length)
            throw new PlcReadException("s7_frame", "Longueur de trame S7 incohérente.", true);
        return (cotp[15..(15 + parameterLength)], cotp[(15 + parameterLength)..]);
    }

    private static PlcReadException ItemError(byte code, string role) => code switch
    {
        0x03 => new("s7_item_03", $"Accès refusé à {role} : bloc à accès optimisé ou protection de la CPU.", false),
        0x05 => new("s7_item_05", $"Adresse de {role} hors de la zone (DB trop court ?).", false),
        0x06 => new("s7_item_06", $"Type de données non pris en charge pour {role}.", false),
        0x07 => new("s7_item_07", $"Type de données incohérent pour {role}.", false),
        0x0A => new("s7_item_0A", $"Objet inexistant pour {role} : vérifiez le numéro de DB.", false),
        _ => new($"s7_item_{code:X2}", $"Lecture S7 refusée pour {role} (code 0x{code:X2}).", false)
    };

    private static async Task WriteTpktAsync(NetworkStream stream, byte[] payload, CancellationToken token)
    {
        var frame = new byte[4 + payload.Length];
        frame[0] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), (ushort)frame.Length);
        payload.CopyTo(frame, 4);
        await stream.WriteAsync(frame, token).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadTpktAsync(NetworkStream stream, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
        if (header[0] != 0x03 || length is < 7 or > 4096)
            throw new PlcReadException("s7_frame", "En-tête TPKT invalide.", true);
        var payload = new byte[length - 4];
        await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return payload;
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return ValueTask.CompletedTask;
    }
}
