using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mkr.Core;
using Mkr.Infrastructure.Plc;

namespace Mkr.Desktop;

public partial class AcquisitionWindow : Window
{
    private sealed record SignalRow(string Role, TextBox Offset, ComboBox Type, TextBox RawLow, TextBox RawHigh,
        TextBox EngineeringLow, TextBox EngineeringHigh);

    private static readonly PlcDataType[] Types = Enum.GetValues<PlcDataType>();
    private readonly CsvWorkspace _workspace;
    private readonly List<AcquiredSample> _samples = [];
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<SignalRow> _rows = [];
    private CancellationTokenSource? _reading;
    private AcquisitionOrigin? _origin;
    private AcquiredSample? _latest;
    internal CsvImport? SavedImport { get; private set; }

    internal AcquisitionWindow(CsvWorkspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        int row = 1;
        foreach (var role in new[] { "SP", "PV", "MV" })
        {
            TextBox Box(int column, string text, string name, string id)
            {
                var box = new TextBox { Text = text, Margin = new Thickness(0, 4, 8, 0) };
                System.Windows.Automation.AutomationProperties.SetName(box, $"{Terms.RoleName(role)} {name}");
                System.Windows.Automation.AutomationProperties.SetAutomationId(box, role + id);
                Grid.SetRow(box, row);
                Grid.SetColumn(box, column);
                SignalGrid.Children.Add(box);
                return box;
            }
            var label = new TextBlock { Text = Terms.RoleName(role), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(label, row);
            SignalGrid.Children.Add(label);
            var offset = Box(1, ((row - 1) * 2).ToString(CultureInfo.CurrentCulture), "adresse", "Offset");
            var type = new ComboBox { ItemsSource = Types, SelectedItem = PlcDataType.Real32, Margin = new Thickness(0, 4, 8, 0) };
            System.Windows.Automation.AutomationProperties.SetName(type, $"{role} type");
            System.Windows.Automation.AutomationProperties.SetAutomationId(type, role + "Type");
            Grid.SetRow(type, row);
            Grid.SetColumn(type, 2);
            SignalGrid.Children.Add(type);
            _rows.Add(new SignalRow(role, offset, type, Box(3, "0", "brut bas", "RawLow"), Box(4, "1", "brut haut", "RawHigh"),
                Box(5, "0", "physique bas", "EngineeringLow"), Box(6, "1", "physique haut", "EngineeringHigh")));
            row++;
        }
        ModeType.ItemsSource = Types.Where(t => t != PlcDataType.Real32).ToArray();
        ModeType.SelectedItem = PlcDataType.UInt16;
        Protocol.SelectedIndex = 0;
        Closed += (_, _) => _lifetime.Cancel();
        UpdatePlan();
    }

    private void ModeOptionChanged(object sender, RoutedEventArgs args)
    {
        if (ModePanel is null) return;
        var visibility = ModeEnabled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ModePanel.Visibility = ModeHelp.Visibility = visibility;
    }

    private bool IsS7 => Protocol.SelectedIndex == 1;

    private void ProtocolChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ModbusPanel is null) return;
        ModbusPanel.Visibility = IsS7 ? Visibility.Collapsed : Visibility.Visible;
        S7Panel.Visibility = IsS7 ? Visibility.Visible : Visibility.Collapsed;
        if (Port.Text is "" or "502" or "102") Port.Text = IsS7 ? "102" : "502";
        AddressHelp.Text = IsS7
            ? "Adresse = offset d’octet dans la zone : DB10.DBD4 → 4, DB10.DBW8 → 8, MD20 → 20. INT/WORD sur 2 octets, DINT/DWORD/REAL sur 4 octets."
            : "Adresse = offset de registre en base 0 : %MW100 → 100 (notation Modicon 400101 → 100). Un REAL ou un entier 32 bits occupe 2 registres consécutifs.";
        int step = IsS7 ? 4 : 2;
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].Offset.Text is "0" or "2" or "4" or "8") _rows[i].Offset.Text = (i * step).ToString(CultureInfo.CurrentCulture);
    }

    private void PlanChanged(object sender, TextChangedEventArgs args) => UpdatePlan();

    private bool IsHistorian => Continuous.IsChecked == true;

    private void ModeChanged(object sender, RoutedEventArgs args)
    {
        if (HistorianPanel is null) return;
        HistorianPanel.Visibility = IsHistorian ? Visibility.Visible : Visibility.Collapsed;
        CountPanel.Visibility = IsHistorian ? Visibility.Collapsed : Visibility.Visible;
        Probe.Visibility = Save.Visibility = IsHistorian ? Visibility.Collapsed : Visibility.Visible;
        Start.Content = IsHistorian ? "_Démarrer l’enregistrement continu" : "_Démarrer l’acquisition";
        UpdatePlan();
    }

    private HistorianPlan? ReadHistorian()
    {
        if (!double.TryParse(Hours.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours) &&
            !double.TryParse(Hours.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out hours) ||
            !int.TryParse(Period.Text, out int period) || period <= 0 || !double.IsFinite(hours) || hours <= 0 ||
            !int.TryParse(SegmentSize.Text, out int segment) || !int.TryParse(Overlap.Text, out int overlap) ||
            !Number(Outage, out var outage))
            return null;
        var total = (long)Math.Round(hours * 3600_000 / period) + 1;
        return new HistorianPlan(period, total, segment, overlap, outage);
    }

    private void UpdatePlan()
    {
        if (PlanSummary is null) return;
        if (IsHistorian)
        {
            var historian = ReadHistorian();
            PlanSummary.Text = historian is null ? "Indiquez une durée, une période, une taille de segment et un recouvrement."
                : historian.Validate() ?? $"{historian.TotalSamples:N0} mesures sur {Format(historian.PlannedDuration)} en {historian.ExpectedSegments} segment(s) " +
                    $"d’environ {historian.SegmentSamples * 120 / 1024} Kio, enregistrés dès qu’ils sont complets. Le recouvrement garde entier un échelon proche d’une coupure " +
                    $"s’il dure moins de {historian.OverlapSamples * historian.PeriodMs / 1000.0:G3} s : pour un essai plus long, utilisez l’acquisition ponctuelle. " +
                    "Réalisez les échelons de la sortie de commande avec le régulateur en manuel ; MKR les retrouvera ensuite dans l’historique.";
            return;
        }
        if (int.TryParse(Count.Text, out int count) && int.TryParse(Period.Text, out int period) && count > 0 && period > 0)
        {
            var plan = new AcquisitionPlan(count, period);
            PlanSummary.Text = $"Durée prévue : {Format(plan.PlannedDuration)} ({count} mesures toutes les {period} ms). " +
                "Vérifiez qu’elle couvre la stabilisation avant l’échelon, le retard et 4 à 5 constantes de temps du procédé.";
        }
        else PlanSummary.Text = "Indiquez une période et un nombre de mesures pour estimer la durée.";
    }

    private static string Format(TimeSpan span) => span.TotalHours >= 1
        ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    private static bool Number(TextBox box, out double value) =>
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private (AcquisitionTarget Target, AcquisitionPlan Plan)? ReadSettings()
    {
        var addresses = new List<SignalAddress>();
        foreach (var row in _rows)
        {
            if (!int.TryParse(row.Offset.Text, out int offset) || row.Type.SelectedItem is not PlcDataType type ||
                !Number(row.RawLow, out var rawLow) || !Number(row.RawHigh, out var rawHigh) ||
                !Number(row.EngineeringLow, out var engLow) || !Number(row.EngineeringHigh, out var engHigh))
            {
                Status.Text = $"Adresse, type ou mise à l’échelle de {row.Role} invalide.";
                return null;
            }
            addresses.Add(new SignalAddress(offset, type, rawLow, rawHigh, engLow, engHigh));
        }
        if (!int.TryParse(Port.Text, out int port) || !int.TryParse(Count.Text, out int count) ||
            !int.TryParse(Period.Text, out int period) || !Number(Outage, out var outage))
        {
            Status.Text = "Port, période, nombre de mesures ou coupure tolérée invalide.";
            return null;
        }
        SignalAddress? mode = null;
        double? manualValue = null;
        if (ModeEnabled.IsChecked == true)
        {
            if (!int.TryParse(ModeOffset.Text, out int modeOffset) || ModeType.SelectedItem is not PlcDataType modeType ||
                !Number(ManualValue, out var manual))
            {
                Status.Text = "Adresse, type du mode ou valeur brute « manuel » invalide : aucune convention n’est supposée.";
                return null;
            }
            mode = new SignalAddress(modeOffset, modeType);
            manualValue = manual;
        }
        AcquisitionTarget target;
        if (IsS7)
        {
            if (!int.TryParse(Rack.Text, out int rack) || !int.TryParse(Slot.Text, out int slot) ||
                !int.TryParse(DataBlock.Text, out int db))
            {
                Status.Text = "Rack, emplacement ou numéro de DB invalide.";
                return null;
            }
            target = new AcquisitionTarget(PlcProtocol.S7, Host.Text.Trim(), port, addresses[0], addresses[1], addresses[2],
                ProcessUnit.Text.Trim(), CommandUnit.Text.Trim(), ByteOrder.ABCD,
                Rack: rack, Slot: slot, Area: Area.SelectedIndex == 1 ? S7Area.Merker : S7Area.DataBlock, DataBlock: db,
                Mode: mode, ManualModeValue: manualValue);
        }
        else
        {
            if (!byte.TryParse(UnitId.Text, out byte unit) || Order.SelectedIndex < 0)
            {
                Status.Text = "Choisissez l’unité Modbus et l’ordre des octets : un mauvais ordre donne des valeurs plausibles mais fausses.";
                return null;
            }
            target = new AcquisitionTarget(PlcProtocol.ModbusTcp, Host.Text.Trim(), port, addresses[0], addresses[1], addresses[2],
                ProcessUnit.Text.Trim(), CommandUnit.Text.Trim(), (ByteOrder)Order.SelectedIndex, unit,
                Table.SelectedIndex == 1 ? ModbusTable.InputRegisters : ModbusTable.HoldingRegisters,
                Mode: mode, ManualModeValue: manualValue);
        }
        var plan = new AcquisitionPlan(count, period, outage);
        if (PlcDecoding.Validate(target, plan) is { } problem)
        {
            Status.Text = problem;
            return null;
        }
        if (target is { Protocol: PlcProtocol.ModbusTcp } && new ModbusTcpReader(target).RequestsPerReading > 1)
            Status.Text = "Adresses éloignées (plus de 125 registres) : une requête par signal, lues à des instants légèrement différents. Rapprochez-les pour une lecture groupée.";
        return (target, plan);
    }

    private void ShowState(AcquisitionState state, string? detail = null)
    {
        (ConnectionLight.Fill, ConnectionState.Text) = state switch
        {
            AcquisitionState.Connecting => (PlotColors.Status("MkrWarning"), "Connexion en cours…"),
            AcquisitionState.Connected or AcquisitionState.Reading => (PlotColors.Status("MkrSuccess"), "Connecté · lecture seule"),
            AcquisitionState.Reconnecting => (PlotColors.Status("MkrWarning"), "Liaison perdue · reconnexion…"),
            _ => (PlotColors.Status("MkrMuted"), "Déconnecté")
        };
        if (detail is not null) ConnectionState.Text += " · " + detail;
    }

    private string Describe(AcquiredSample sample) =>
        $"{sample.ReceivedAt:HH:mm:ss.fff} UTC · aller-retour {sample.RoundTripMs:0.#} ms\n" +
        $"Consigne {sample.Sp:G6} {ProcessUnit.Text} (brut {sample.RawSp:G9})\n" +
        $"Mesure {sample.Pv:G6} {ProcessUnit.Text} (brut {sample.RawPv:G9})\n" +
        $"Sortie de commande {sample.Mv:G6} {CommandUnit.Text} (brut {sample.RawMv:G9})" +
        (sample.RawMode is { } rawMode ? $"\nMode brut {rawMode:G9} → {(sample.InManual == true ? "manuel" : "PAS en manuel")}" : "");

    private async void ProbeReading(object sender, RoutedEventArgs args)
    {
        if (ReadSettings() is not { } settings) return;
        Settings.IsEnabled = Probe.IsEnabled = Start.IsEnabled = false;
        ShowState(AcquisitionState.Connecting);
        try
        {
            var samples = new List<AcquiredSample>();
            var result = await new PlcAcquisition().CaptureAsync(settings.Target, settings.Plan with { Samples = 1 },
                samples.Add, null, _lifetime.Token);
            if (samples.Count == 1)
            {
                Latest.Text = Describe(samples[0]);
                Status.Text = "Lecture d’essai réussie. Comparez les valeurs brutes et physiques à celles affichées dans l’atelier de l’automate avant de lancer l’acquisition.";
            }
            else Status.Text = $"Lecture d’essai impossible : {result.Message ?? result.StopReason}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        finally
        {
            ShowState(AcquisitionState.Stopped);
            Settings.IsEnabled = Probe.IsEnabled = Start.IsEnabled = true;
        }
    }

    private async Task StartHistorian(AcquisitionTarget target)
    {
        var requested = ReadHistorian();
        if (requested is null || requested.Validate() is { } problem)
        {
            Status.Text = requested is null ? "Durée, période, segment ou recouvrement invalide." : requested.Validate();
            return;
        }
        var plan = requested;
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _reading = reading;
        Settings.IsEnabled = Start.IsEnabled = false;
        Stop.IsEnabled = true;
        Progress.Maximum = plan.TotalSamples;
        Progress.Value = 0;
        int segments = 0;
        Status.Text = "Enregistrement continu en cours. Chaque segment est enregistré dans le projet dès qu’il est complet : fermer cette fenêtre n’en perd aucun.";
        var progress = new Progress<AcquisitionProgress>(update =>
        {
            if (_lifetime.IsCancellationRequested) return;
            ShowState(update.State, update.LastRoundTripMs is { } rtt ? $"dernier aller-retour {rtt:0.#} ms" : null);
            Progress.Value = Math.Min(plan.TotalSamples, update.Received + update.MissedSamples);
            ProgressText.Text = $"{update.Received:N0} / {update.Requested:N0} mesures · {update.MissedSamples} manquée(s) · {segments} segment(s) enregistré(s) · " +
                $"écoulé {Format(update.Elapsed)} · restant estimé {Format(update.Remaining)}" +
                (update.LastError is { } error ? $"\nDernier incident : {error}" : "");
        });
        HistorianResult? result = null;
        try
        {
            result = await new HistorianRecorder().RunAsync(target, plan, async segment =>
            {
                // Saved even after a stop: the open segment must not be lost.
                SavedImport = await _workspace.SaveCaptureAsync(segment.Samples.ToList(), segment.Origin, CancellationToken.None);
                segments++;
            }, progress, reading.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (Exception e) when (e is ArgumentException or System.IO.IOException or System.IO.InvalidDataException)
        {
            Status.Text = $"Enregistrement continu impossible. {e.Message}";
            return;
        }
        finally
        {
            _reading = null;
            Stop.IsEnabled = false;
            Settings.IsEnabled = Start.IsEnabled = true;
        }
        var summary = result.StopReason switch
        {
            "sample_limit" => "Durée prévue atteinte.",
            "operator_stop" => "Enregistrement arrêté par l’opérateur.",
            "connection_failed" => $"Connexion impossible : {result.Message}",
            "connection_lost" => $"Liaison perdue au-delà de la coupure tolérée : {result.Message}",
            "storage_error" => result.Message ?? "Enregistrement d’un segment impossible.",
            _ => $"Enregistrement interrompu : {result.Message}"
        };
        summary += $" {result.SegmentsSaved} segment(s) enregistré(s), {result.Statistics.Received:N0} mesures, " +
            $"{result.Statistics.MissedSamples} manquée(s) en {result.Statistics.Gaps.Length} lacune(s), aucune interpolée." +
            (result.Statistics.SamplesNotInManual > 0
                ? $" ATTENTION : {result.Statistics.SamplesNotInManual:N0} mesure(s) hors du mode manuel déclaré ; la recherche d'essais les écartera (colonne MANUAL)."
                : "");
        Status.Text = summary;
        if (result.SegmentsSaved > 0 && !_lifetime.IsCancellationRequested)
        {
            MessageBox.Show(this, summary + "\n\nRetrouvez les essais dans « Identification » avec « Rechercher les essais exploitables ».",
                "MKR — Enregistrement continu terminé", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
    }

    private async void StartReading(object sender, RoutedEventArgs args)
    {
        if (ReadSettings() is not { } settings) return;
        var (target, plan) = settings;
        if (IsHistorian)
        {
            await StartHistorian(target);
            return;
        }
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _reading = reading;
        Settings.IsEnabled = Probe.IsEnabled = Start.IsEnabled = Save.IsEnabled = false;
        Stop.IsEnabled = true;
        Progress.Maximum = plan.Samples;
        Progress.Value = 0;
        Status.Text = "Acquisition en cours. Réalisez l’essai prévu sur le banc depuis l’atelier de l’automate.";
        var progress = new Progress<AcquisitionProgress>(update =>
        {
            if (_lifetime.IsCancellationRequested) return;
            ShowState(update.State, update.LastRoundTripMs is { } rtt ? $"dernier aller-retour {rtt:0.#} ms" : null);
            Progress.Value = Math.Min(plan.Samples, update.Received + update.MissedSamples);
            ProgressText.Text = $"{update.Received} / {update.Requested} mesures · {update.MissedSamples} manquée(s) · " +
                $"écoulé {Format(update.Elapsed)} · restant estimé {Format(update.Remaining)}" +
                (update.LastError is { } error ? $"\nDernier incident : {error}" : "");
            AcquiredSample? latest;
            lock (_gate) latest = _latest;
            if (latest is not null) Latest.Text = Describe(latest);
        });
        AcquisitionResult? result = null;
        try
        {
            result = await new PlcAcquisition().CaptureAsync(target, plan, sample =>
            {
                lock (_gate)
                {
                    _samples.Add(sample);
                    _latest = sample;
                }
            }, progress, reading.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        finally
        {
            _reading = null;
            Stop.IsEnabled = false;
        }
        int count;
        lock (_gate) count = _samples.Count;
        _origin = new AcquisitionOrigin(null, result.StopReason, "receiver_utc_midpoint", target, plan, result.Statistics);
        Status.Text = result.StopReason switch
        {
            "sample_limit" => "Nombre de mesures atteint.",
            "operator_stop" => "Acquisition arrêtée par l’opérateur.",
            "connection_failed" => $"Connexion impossible : {result.Message}",
            "connection_lost" => $"Liaison perdue au-delà de la coupure tolérée : {result.Message}",
            _ => $"Acquisition interrompue : {result.Message}"
        } + (result.Statistics.MissedSamples > 0
            ? $" {result.Statistics.MissedSamples} mesure(s) manquée(s) sont enregistrées comme lacunes, sans interpolation."
            : "") + (result.Statistics.SamplesNotInManual > 0
            ? $" ATTENTION : {result.Statistics.SamplesNotInManual} mesure(s) prise(s) hors du mode manuel déclaré : un essai en boucle ouverte n’y est pas valable (colonne MANUAL de l’archive)."
            : "");
        Save.IsEnabled = count > 0;
        Settings.IsEnabled = Probe.IsEnabled = Start.IsEnabled = count == 0;
    }

    private void StopReading(object sender, RoutedEventArgs args) => _reading?.Cancel();

    private async void SaveCapture(object sender, RoutedEventArgs args)
    {
        if (_origin is null) return;
        List<AcquiredSample> samples;
        lock (_gate) samples = [.. _samples];
        if (samples.Count == 0) return;
        Save.IsEnabled = false;
        try
        {
            SavedImport = await _workspace.SaveCaptureAsync(samples, _origin, _lifetime.Token);
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
