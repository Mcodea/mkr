using System.Windows;
using Mkr.Core;
using Mkr.Infrastructure;

namespace Mkr.Desktop;

public partial class AcquisitionWindow : Window
{
    private readonly CsvWorkspace _workspace;
    private readonly List<AcquiredSample> _samples = [];
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _reading;
    private AcquisitionOrigin? _origin;
    internal CsvImport? SavedImport { get; private set; }

    internal AcquisitionWindow(CsvWorkspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        Closed += (_, _) => _lifetime.Cancel();
    }

    private async void StartReading(object sender, RoutedEventArgs args)
    {
        if (!int.TryParse(Port.Text, out int port) || !byte.TryParse(UnitId.Text, out byte unit) ||
            !ushort.TryParse(Offset.Text, out ushort offset) || !int.TryParse(Interval.Text, out int interval) ||
            !int.TryParse(Count.Text, out int count) || Order.SelectedIndex < 0)
        {
            Status.Text = "Renseignez la cible, la cadence et l’ordre des mots.";
            return;
        }
        var source = new RegisterSource(Host.Text.Trim(), port, unit, offset, Order.SelectedIndex == 1,
            ProcessUnit.Text.Trim(), CommandUnit.Text.Trim(), interval);
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _reading = reading;
        Settings.IsEnabled = Start.IsEnabled = Save.IsEnabled = false;
        Stop.IsEnabled = true;
        Status.Text = "Lecture en cours…";
        var reason = "sample_limit";
        try
        {
            await new ModbusAcquisition().CaptureAsync(source, count, sample =>
            {
                _samples.Add(sample);
                Latest.Text = $"{_samples.Count} mesures · {sample.ReceivedAt:HH:mm:ss.fff} UTC · SP {sample.Sp:G6} · PV {sample.Pv:G6} · MV {sample.Mv:G6}";
            }, reading.Token);
            Status.Text = "Nombre de mesures atteint.";
        }
        catch (OperationCanceledException)
        {
            reason = reading.IsCancellationRequested ? "operator_stop" : "timeout";
            Status.Text = reading.IsCancellationRequested ? "Lecture arrêtée." : "Délai de communication dépassé.";
        }
        catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException or System.Net.Sockets.SocketException or ArgumentException)
        {
            reason = "read_error";
            Status.Text = $"Lecture interrompue. {e.Message}";
        }
        finally
        {
            _reading = null;
            Stop.IsEnabled = false;
            _origin = new(source, reason, "receiver_utc");
            Save.IsEnabled = _samples.Count > 0;
            Settings.IsEnabled = Start.IsEnabled = _samples.Count == 0;
        }
    }

    private void StopReading(object sender, RoutedEventArgs args) => _reading?.Cancel();

    private async void SaveCapture(object sender, RoutedEventArgs args)
    {
        if (_origin is null || _samples.Count == 0) return;
        Save.IsEnabled = false;
        try
        {
            SavedImport = await _workspace.SaveCaptureAsync(_samples, _origin, _lifetime.Token);
            if (!_lifetime.IsCancellationRequested) DialogResult = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception e)
        {
            Status.Text = $"Enregistrement impossible. {e.Message}";
            Save.IsEnabled = true;
        }
    }
}
