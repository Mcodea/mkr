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
            System.Windows.Automation.AutomationProperties.SetName(type, Loc.T("cs.type", role));
            System.Windows.Automation.AutomationProperties.SetAutomationId(type, role + "Type");
            Grid.SetRow(type, row);
            Grid.SetColumn(type, 2);
            SignalGrid.Children.Add(type);
            _rows.Add(new SignalRow(role, offset, type, Box(3, "0", Loc.T("cs.brut_bas"), "RawLow"), Box(4, "1", Loc.T("cs.brut_haut"), "RawHigh"),
                Box(5, "0", Loc.T("cs.physique_bas"), "EngineeringLow"), Box(6, "1", Loc.T("cs.physique_haut"), "EngineeringHigh")));
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
            ? Loc.T("cs.adresse_offset_doctet_dans_la")
            : Loc.T("cs.adresse_offset_de_registre_en");
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
        Start.Content = IsHistorian ? Loc.T("cs.demarrer_lenregistrement_continu") : Loc.T("cs.demarrer_lacquisition");
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
            PlanSummary.Text = historian is null ? Loc.T("cs.indiquez_une_duree_une_periode")
                : historian.Validate() ?? Loc.T("cs.mesures_sur_en_segment_s", historian.TotalSamples, Format(historian.PlannedDuration), historian.ExpectedSegments, historian.SegmentSamples * 120 / 1024, historian.OverlapSamples * historian.PeriodMs / 1000.0);
            return;
        }
        if (int.TryParse(Count.Text, out int count) && int.TryParse(Period.Text, out int period) && count > 0 && period > 0)
        {
            var plan = new AcquisitionPlan(count, period);
            PlanSummary.Text = Loc.T("cs.duree_prevue_mesures_toutes_les", Format(plan.PlannedDuration), count, period);
        }
        else PlanSummary.Text = Loc.T("cs.indiquez_une_periode_et_un");
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
                Status.Text = Loc.T("cs.adresse_type_ou_mise_a", row.Role);
                return null;
            }
            addresses.Add(new SignalAddress(offset, type, rawLow, rawHigh, engLow, engHigh));
        }
        if (!int.TryParse(Port.Text, out int port) || !int.TryParse(Count.Text, out int count) ||
            !int.TryParse(Period.Text, out int period) || !Number(Outage, out var outage))
        {
            Status.Text = Loc.T("cs.port_periode_nombre_de_mesures");
            return null;
        }
        SignalAddress? mode = null;
        double? manualValue = null;
        if (ModeEnabled.IsChecked == true)
        {
            if (!int.TryParse(ModeOffset.Text, out int modeOffset) || ModeType.SelectedItem is not PlcDataType modeType ||
                !Number(ManualValue, out var manual))
            {
                Status.Text = Loc.T("cs.adresse_type_du_mode_ou");
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
                Status.Text = Loc.T("cs.rack_emplacement_ou_numero_de");
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
                Status.Text = Loc.T("cs.choisissez_lunite_modbus_et_lordre");
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
            Status.Text = Loc.T("cs.adresses_eloignees_plus_de_125");
        return (target, plan);
    }

    private void ShowState(AcquisitionState state, string? detail = null)
    {
        (ConnectionLight.Fill, ConnectionState.Text) = state switch
        {
            AcquisitionState.Connecting => (PlotColors.Status("MkrWarning"), Loc.T("cs.connexion_en_cours")),
            AcquisitionState.Connected => (PlotColors.Status("MkrSuccess"), Loc.T("cs.connecte_lecture_seule")),
            AcquisitionState.Reading => (PlotColors.Status("MkrSuccess"), Loc.T("acq.state.running")),
            AcquisitionState.Reconnecting => (PlotColors.Status("MkrWarning"), Loc.T("cs.liaison_perdue_reconnexion")),
            _ => (PlotColors.Status("MkrMuted"), Loc.T("cs.deconnecte"))
        };
        if (detail is not null) ConnectionState.Text += " · " + detail;
    }

    /// <summary>The end state of an acquisition: completed, stopped by the operator, link lost, connection failed or error (always with its reason in the status line).</summary>
    private void ShowOutcome(string stopReason, string? message = null)
    {
        (ConnectionLight.Fill, ConnectionState.Text) = stopReason switch
        {
            "sample_limit" => (PlotColors.Status("MkrSuccess"), Loc.T("acq.state.completed")),
            "operator_stop" => (PlotColors.Status("MkrMuted"), Loc.T("acq.state.stopped")),
            "connection_lost" => (PlotColors.Status("MkrDanger"), Loc.T("acq.state.lost")),
            "connection_failed" => (PlotColors.Status("MkrDanger"), Loc.T("acq.state.failed")),
            _ => (PlotColors.Status("MkrDanger"), Loc.T("acq.state.error"))
        };
        if (!string.IsNullOrWhiteSpace(message) && stopReason is not ("sample_limit" or "operator_stop")) ConnectionState.Text += " · " + message;
    }

    private string Describe(AcquiredSample sample) =>
        Loc.T("cs.utc_aller_retour_ms_consigne", sample.ReceivedAt, sample.RoundTripMs, sample.Sp, ProcessUnit.Text, sample.RawSp, sample.Pv, ProcessUnit.Text, sample.RawPv, sample.Mv, CommandUnit.Text, sample.RawMv) +
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
                Status.Text = Loc.T("cs.lecture_dessai_reussie_comparez_les");
            }
            else Status.Text = Loc.T("cs.lecture_dessai_impossible", result.Message ?? result.StopReason);
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
            Status.Text = requested is null ? Loc.T("cs.duree_periode_segment_ou_recouvrement") : requested.Validate();
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
        Status.Text = Loc.T("cs.enregistrement_continu_en_cours_chaque");
        var progress = new Progress<AcquisitionProgress>(update =>
        {
            if (_lifetime.IsCancellationRequested) return;
            ShowState(update.State, update.LastRoundTripMs is { } rtt ? Loc.T("cs.dernier_aller_retour_ms", rtt) : null);
            Progress.Value = Math.Min(plan.TotalSamples, update.Received + update.MissedSamples);
            ProgressText.Text = Loc.T("cs.mesures_manquee_s_segment_s", update.Received, update.Requested, update.MissedSamples, segments, Format(update.Elapsed), Format(update.Remaining)) +
                (update.LastError is { } error ? Loc.T("cs.dernier_incident", error) : "");
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
            Status.Text = Loc.T("cs.enregistrement_continu_impossible", e.Message);
            ShowOutcome("error", e.Message);
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
            "sample_limit" => Loc.T("cs.duree_prevue_atteinte"),
            "operator_stop" => Loc.T("cs.enregistrement_arrete_par_loperateur"),
            "connection_failed" => Loc.T("cs.connexion_impossible", result.Message),
            "connection_lost" => Loc.T("cs.liaison_perdue_au_dela_de", result.Message),
            "storage_error" => result.Message ?? Loc.T("cs.enregistrement_dun_segment_impossible"),
            _ => Loc.T("cs.enregistrement_interrompu", result.Message)
        };
        summary += Loc.T("cs.segment_s_enregistre_s_mesures", result.SegmentsSaved, result.Statistics.Received, result.Statistics.MissedSamples, result.Statistics.Gaps.Length) +
            (result.Statistics.SamplesNotInManual > 0
                ? Loc.T("cs.attention_mesure_s_hors_du", result.Statistics.SamplesNotInManual)
                : "");
        Status.Text = summary;
        ShowOutcome(result.StopReason, result.Message);
        if (result.SegmentsSaved > 0 && !_lifetime.IsCancellationRequested)
        {
            MessageBox.Show(this, summary + Loc.T("cs.retrouvez_les_essais_dans_identification"),
                Loc.T("cs.orias_enregistrement_continu_termine"), MessageBoxButton.OK, MessageBoxImage.Information);
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
        Status.Text = Loc.T("cs.acquisition_en_cours_realisez_lessai");
        var progress = new Progress<AcquisitionProgress>(update =>
        {
            if (_lifetime.IsCancellationRequested) return;
            ShowState(update.State, update.LastRoundTripMs is { } rtt ? Loc.T("cs.dernier_aller_retour_ms", rtt) : null);
            Progress.Value = Math.Min(plan.Samples, update.Received + update.MissedSamples);
            ProgressText.Text = Loc.T("cs.mesures_manquee_s_ecoule_restant", update.Received, update.Requested, update.MissedSamples, Format(update.Elapsed), Format(update.Remaining)) +
                (update.LastError is { } error ? Loc.T("cs.dernier_incident", error) : "");
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
            "sample_limit" => Loc.T("cs.nombre_de_mesures_atteint"),
            "operator_stop" => Loc.T("cs.acquisition_arretee_par_loperateur"),
            "connection_failed" => Loc.T("cs.connexion_impossible", result.Message),
            "connection_lost" => Loc.T("cs.liaison_perdue_au_dela_de", result.Message),
            _ => Loc.T("cs.acquisition_interrompue", result.Message)
        } + (result.Statistics.MissedSamples > 0
            ? Loc.T("cs.mesure_s_manquee_s_sont", result.Statistics.MissedSamples)
            : "") + (result.Statistics.SamplesNotInManual > 0
            ? Loc.T("cs.attention_mesure_s_prise_s", result.Statistics.SamplesNotInManual)
            : "");
        ShowOutcome(result.StopReason, result.Message);
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
            Status.Text = Loc.T("cs.enregistrement_impossible", e.Message);
            Save.IsEnabled = true;
        }
    }
}
