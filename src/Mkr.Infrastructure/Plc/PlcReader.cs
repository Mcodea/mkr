using System.Net.Sockets;
using Mkr.Core;

namespace Mkr.Infrastructure.Plc;

/// <summary>
/// A read failure. <see cref="Recoverable"/> communication failures allow a reconnection;
/// configuration and protocol refusals (bad address, access denied) stop the capture.
/// <see cref="QualityOnly"/> marks a rejected value on a healthy connection (for example a non-finite REAL).
/// </summary>
public sealed class PlcReadException(string code, string message, bool recoverable, bool qualityOnly = false)
    : Exception(message)
{
    public string Code { get; } = code;
    public bool Recoverable { get; } = recoverable;
    public bool QualityOnly { get; } = qualityOnly;
}

/// <summary>One coherent reading: raw decoded SP, PV, MV and, when configured, the raw controller mode.</summary>
public sealed record PlcReading(double Sp, double Pv, double Mv, double? Mode = null);

/// <summary>Read-only PLC session returning the raw decoded values of one coherent reading.</summary>
public interface IPlcReader : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken token);
    Task<PlcReading> ReadAsync(CancellationToken token);
}

public static class PlcReaders
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public static IPlcReader Create(AcquisitionTarget target) => target.Protocol switch
    {
        PlcProtocol.ModbusTcp => new ModbusTcpReader(target),
        PlcProtocol.S7 => new S7Reader(target),
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    internal static async Task<TcpClient> ConnectTcpAsync(string host, int port, CancellationToken token)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(token);
            connect.CancelAfter(RequestTimeout);
            await client.ConnectAsync(host, port, connect.Token).ConfigureAwait(false);
            return client;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            client.Dispose();
            throw new PlcReadException("connect_timeout", "Connexion TCP impossible dans le délai de 5 s.", true);
        }
        catch (SocketException error)
        {
            client.Dispose();
            throw new PlcReadException("connect_failed", $"Connexion TCP refusée ou injoignable ({error.SocketErrorCode}).", true);
        }
    }

    /// <summary>Runs one network exchange with the request timeout, translating transport failures.</summary>
    internal static async Task<T> ExchangeAsync<T>(Func<CancellationToken, Task<T>> exchange, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            return await exchange(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new PlcReadException("timeout", "Pas de réponse de l’automate dans le délai de 5 s.", true);
        }
        catch (EndOfStreamException)
        {
            throw new PlcReadException("connection_closed", "Connexion fermée par l’automate.", true);
        }
        catch (IOException error)
        {
            throw new PlcReadException("io_error", $"Liaison interrompue : {error.Message}", true);
        }
        catch (SocketException error)
        {
            throw new PlcReadException("socket_error", $"Erreur réseau {error.SocketErrorCode}.", true);
        }
        catch (ObjectDisposedException)
        {
            throw new PlcReadException("connection_closed", "Connexion fermée.", true);
        }
    }

    internal static double Checked(double value, string role) => double.IsFinite(value)
        ? value
        : throw new PlcReadException("non_finite_value", $"Valeur {role} non finie reçue.", true, qualityOnly: true);
}
