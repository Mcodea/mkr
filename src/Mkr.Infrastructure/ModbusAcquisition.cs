using System.Buffers.Binary;
using System.Net.Sockets;
using Mkr.Core;

namespace Mkr.Infrastructure;

/// <summary>Reads one contiguous block of six registers per sample, in SP, PV, MV order.</summary>
public sealed class ModbusAcquisition : IReadOnlyAcquisition
{
    public async Task CaptureAsync(RegisterSource source, int maximumSamples, Action<AcquiredSample> received,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.Host) || source.Port is < 1 or > 65535 ||
            source.FirstRegister > 65530 || source.IntervalMs is < 100 or > 60000 ||
            maximumSamples is < 1 or > 5000 || string.IsNullOrWhiteSpace(source.ProcessUnit) ||
            string.IsNullOrWhiteSpace(source.CommandUnit))
            throw new ArgumentException("Paramètres de lecture invalides.");
        using var client = new TcpClient();
        using (var connect = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connect.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(source.Host, source.Port, connect.Token);
        }
        using var stream = client.GetStream();
        for (int i = 0; i < maximumSamples; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] request = [0,0,0,0,0,6,source.UnitId,3,0,0,0,6];
            BinaryPrimitives.WriteUInt16BigEndian(request, (ushort)(i + 1));
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8), source.FirstRegister);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await stream.WriteAsync(request, timeout.Token);
            byte[] header = new byte[7];
            await stream.ReadExactlyAsync(header, timeout.Token);
            int length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
            if (BinaryPrimitives.ReadUInt16BigEndian(header) != i + 1 || header[2] != 0 ||
                header[3] != 0 || header[6] != source.UnitId || (length != 15 && length != 3))
                throw new InvalidDataException("Réponse Modbus incohérente.");
            var pdu = new byte[length - 1];
            await stream.ReadExactlyAsync(pdu, timeout.Token);
            if (pdu[0] == 0x83 && pdu.Length == 2)
                throw new InvalidDataException($"Exception Modbus 0x{pdu[1]:X2}.");
            if (pdu.Length != 14 || pdu[0] != 3 || pdu[1] != 12)
                throw new InvalidDataException("Fonction ou longueur de réponse inattendue.");
            float Value(int offset)
            {
                uint bits = BinaryPrimitives.ReadUInt32BigEndian(pdu.AsSpan(offset));
                if (source.LowWordFirst) bits = (bits << 16) | (bits >> 16);
                float value = BitConverter.UInt32BitsToSingle(bits);
                return float.IsFinite(value) ? value : throw new InvalidDataException("Mesure non finie.");
            }
            received(new(DateTimeOffset.UtcNow, Value(2), Value(6), Value(10)));
            if (i + 1 < maximumSamples) await Task.Delay(source.IntervalMs, cancellationToken);
        }
    }
}
