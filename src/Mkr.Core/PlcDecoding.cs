using System.Buffers.Binary;

namespace Mkr.Core;

/// <summary>Pure decoding of PLC values. No I/O: unit-testable against independent references.</summary>
public static class PlcDecoding
{
    /// <summary>Decodes a value from bytes in transmission order (registers or S7 bytes, big-endian per protocol).</summary>
    public static double Decode(ReadOnlySpan<byte> wire, PlcDataType type, ByteOrder order)
    {
        int length = type is PlcDataType.Int16 or PlcDataType.UInt16 ? 2 : 4;
        if (wire.Length != length) throw new ArgumentException("Longueur de valeur incohérente.");
        Span<byte> b = stackalloc byte[length];
        if (length == 2)
        {
            bool swap = order is ByteOrder.BADC or ByteOrder.DCBA;
            b[0] = wire[swap ? 1 : 0];
            b[1] = wire[swap ? 0 : 1];
        }
        else
        {
            // Map transmitted positions back to A (MSB) .. D (LSB).
            int[] source = order switch
            {
                ByteOrder.ABCD => [0, 1, 2, 3],
                ByteOrder.CDAB => [2, 3, 0, 1],
                ByteOrder.BADC => [1, 0, 3, 2],
                ByteOrder.DCBA => [3, 2, 1, 0],
                _ => throw new ArgumentOutOfRangeException(nameof(order))
            };
            for (int i = 0; i < 4; i++) b[i] = wire[source[i]];
        }
        // Explicit double arms: the natural type of this switch would otherwise be float,
        // silently rounding 32-bit integers to 24 significant bits.
        return type switch
        {
            PlcDataType.Int16 => (double)BinaryPrimitives.ReadInt16BigEndian(b),
            PlcDataType.UInt16 => (double)BinaryPrimitives.ReadUInt16BigEndian(b),
            PlcDataType.Int32 => (double)BinaryPrimitives.ReadInt32BigEndian(b),
            PlcDataType.UInt32 => (double)BinaryPrimitives.ReadUInt32BigEndian(b),
            PlcDataType.Real32 => (double)BinaryPrimitives.ReadSingleBigEndian(b),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    /// <summary>Inverse of <see cref="Decode"/>, used by tests and fixtures only.</summary>
    public static byte[] Encode(double value, PlcDataType type, ByteOrder order)
    {
        int length = type is PlcDataType.Int16 or PlcDataType.UInt16 ? 2 : 4;
        var b = new byte[length];
        switch (type)
        {
            case PlcDataType.Int16: BinaryPrimitives.WriteInt16BigEndian(b, checked((short)value)); break;
            case PlcDataType.UInt16: BinaryPrimitives.WriteUInt16BigEndian(b, checked((ushort)value)); break;
            case PlcDataType.Int32: BinaryPrimitives.WriteInt32BigEndian(b, checked((int)value)); break;
            case PlcDataType.UInt32: BinaryPrimitives.WriteUInt32BigEndian(b, checked((uint)value)); break;
            case PlcDataType.Real32: BinaryPrimitives.WriteSingleBigEndian(b, (float)value); break;
        }
        var wire = new byte[length];
        if (length == 2)
        {
            bool swap = order is ByteOrder.BADC or ByteOrder.DCBA;
            wire[0] = b[swap ? 1 : 0];
            wire[1] = b[swap ? 0 : 1];
            return wire;
        }
        int[] source = order switch
        {
            ByteOrder.ABCD => [0, 1, 2, 3],
            ByteOrder.CDAB => [2, 3, 0, 1],
            ByteOrder.BADC => [1, 0, 3, 2],
            _ => [3, 2, 1, 0]
        };
        for (int i = 0; i < 4; i++) wire[source[i]] = b[i];
        return wire;
    }

    public static double Scale(double raw, SignalAddress address)
    {
        if (address.IsIdentity) return raw;
        return address.EngineeringLow + (raw - address.RawLow) *
            (address.EngineeringHigh - address.EngineeringLow) / (address.RawHigh - address.RawLow);
    }

    /// <summary>Returns a French reason when the target cannot be read safely, or null when it is consistent.</summary>
    public const int MaximumOneShotSamples = 5000;

    public static string? Validate(AcquisitionTarget target, AcquisitionPlan plan, long maximumSamples = MaximumOneShotSamples)
    {
        if (string.IsNullOrWhiteSpace(target.Host)) return "Indiquez l’adresse du banc.";
        if (target.Port is < 1 or > 65535) return "Port TCP invalide.";
        if (string.IsNullOrWhiteSpace(target.ProcessUnit) || string.IsNullOrWhiteSpace(target.CommandUnit))
            return "Indiquez les unités de SP/PV et de MV.";
        if (plan.Samples < 1 || plan.Samples > maximumSamples) return $"Le nombre de mesures doit être compris entre 1 et {maximumSamples:N0}.";
        if (plan.PeriodMs is < 100 or > 60000) return "La période doit être comprise entre 100 ms et 60 s.";
        if (!double.IsFinite(plan.MaximumOutageSeconds) || plan.MaximumOutageSeconds is < 0 or > 600)
            return "La coupure tolérée doit être comprise entre 0 et 600 s.";
        foreach (var (role, address) in target.Signals)
        {
            if (!double.IsFinite(address.RawLow) || !double.IsFinite(address.RawHigh) ||
                !double.IsFinite(address.EngineeringLow) || !double.IsFinite(address.EngineeringHigh) ||
                address.RawHigh == address.RawLow || address.EngineeringHigh == address.EngineeringLow)
                return $"Mise à l’échelle de {role} invalide : les bornes brutes et physiques doivent être finies et distinctes.";
            if (target.Protocol == PlcProtocol.ModbusTcp)
            {
                if (address.Offset < 0 || address.Offset + address.ByteLength / 2 - 1 > 65535)
                    return $"Registre de {role} hors de la plage Modbus 0–65535.";
            }
            else if (address.Offset < 0 || address.Offset > 65535)
                return $"Adresse d’octet de {role} hors plage.";
        }
        if (target.Protocol == PlcProtocol.S7)
        {
            if (target.Rack is < 0 or > 7 || target.Slot is < 0 or > 31) return "Rack 0–7 et emplacement 0–31.";
            if (target.Area == S7Area.DataBlock && target.DataBlock is < 1 or > 65535) return "Numéro de DB invalide.";
            if (target.Order != ByteOrder.ABCD) return "Les valeurs S7 sont transmises en ordre ABCD.";
        }
        return null;
    }
}
