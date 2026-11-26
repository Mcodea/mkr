using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class CsvView : UserControl, IStepView
{
    private readonly CsvWorkspace _workspace;
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>The « Identification… » button: the dialog host opens the identification dialog, the workspace goes to the identification tab.</summary>
    public event EventHandler? IdentificationRequested;

    /// <summary>An import, an acquisition or the process description was saved: a hosting workspace refreshes its journey.</summary>
    public event EventHandler? DataChanged;
    public void Cancel() { _lifetime.Cancel(); _lifetime.Dispose(); }
    public void ShowLoopDescription() { if (Tabs.Items.Count > 1) Tabs.SelectedIndex = 1; }

    internal CsvView(CsvWorkspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        Delimiter.SelectionChanged += (_, _) => Analyze.IsEnabled = false;
        TimeFormat.Items.Add(new ComboBoxItem { Content = Loc.T("cs.iso_8601_avec_decalage_z"), Tag = "" });
        foreach (var (pattern, label) in CsvFormat.Patterns)
            TimeFormat.Items.Add(new ComboBoxItem { Content = Loc.T("cs.heure_locale") + label, Tag = pattern });
        TimeFormat.SelectedIndex = 0;
        TimeFormat.SelectionChanged += (_, _) => TimeZone.IsEnabled = !string.IsNullOrEmpty((string?)TimeFormat.SelectedValue);
        Role.ItemsSource = new[] { "SP", "PV", "CV", "MV", "d" };
        var roleText = new FrameworkElementFactory(typeof(TextBlock));
        roleText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = new RoleNameConverter() });
        Role.ItemTemplate = new DataTemplate { VisualTree = roleText };
        var brief = workspace.ReadLoopBrief();
        LoopTag.Text = brief.Tag;
        LoopControlledQuantity.Text = brief.ControlledQuantity;
        LoopOperatingPoint.Text = brief.OperatingPoint;
        LoopObjective.Text = brief.Objective;
        Description.Text = brief.Description;
        Imports.ItemsSource = workspace.List();
        StatusText.Text = Loc.T("cs.choisissez_un_csv_ou_ouvrez");
    }

    private void OpenCatalogue(object sender, RoutedEventArgs args) => BlockCatalogueWindow.OpenFor(Window.GetWindow(this), _workspace.ReadLoopBrief);

    private void OpenAcquisition(object sender, RoutedEventArgs args)
    {
        var window = new AcquisitionWindow(_workspace) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true && window.SavedImport is { } recorded)
        {
            Analyze.IsEnabled = false;
            Imports.ItemsSource = _workspace.List();
            Imports.SelectedItem = recorded.Summary;
            ShowImport(recorded);
            StatusText.Text = Loc.T("cs.acquisition_enregistree_dans_le_projet");
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void ChooseCsv(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog { Filter = Loc.T("cs.fichiers_csv_csv_csv") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        Analyze.IsEnabled = false;
        Preview.ItemsSource = null;
        Report.Text = "";
        Unit.Clear();
        Role.SelectedIndex = -1;
        await RunAsync(async token =>
        {
            var result = await _workspace.InspectAsync(dialog.FileName, (string)Delimiter.SelectedValue, token);
            if (token.IsCancellationRequested)
                return;
            ShowResult(result);
            ShowTrend(File.ReadAllBytes(dialog.FileName), Path.GetFileName(dialog.FileName), null, null, ((string)Delimiter.SelectedValue)[0], ((string)DecimalSeparator.SelectedValue)[0], null);
            Analyze.IsEnabled = true;
            StatusText.Text = Loc.T("cs.selectionnez_les_colonnes_le_role");
            if (result.SuggestedFormat is { } suggestion) ApplySuggestion(suggestion);
        });
    }

    /// <summary>The proposal only fills the controls: the user confirms it by importing, or corrects it.</summary>
    private void ApplySuggestion(CsvFormatSuggestion suggestion)
    {
        DecimalSeparator.SelectedValue = suggestion.Decimal;
        TimeFormat.SelectedValue = suggestion.TimePattern ?? "";
        var local = suggestion.TimePattern is not null;
        StatusText.Text = Loc.T("cs.format_propose_dapres_les_premieres") +
            (suggestion.Decimal == "," ? Loc.T("cs.decimale_virgule") : Loc.T("cs.decimale_point")) + " · " +
            (local ? Loc.T("cs.heure_locale_colonne_verifiez_le", suggestion.TimeColumn)
                   : suggestion.IsoWithOffset ? Loc.T("cs.horodatage_iso_8601_avec_decalage")
                   : Loc.T("cs.aucune_colonne_de_date_reconnue")) +
            Loc.T("cs.selectionnez_les_colonnes_le_role_2");
    }

    private CsvFormat ReadFormat() => new((string)DecimalSeparator.SelectedValue,
        string.IsNullOrEmpty((string?)TimeFormat.SelectedValue) ? null
            : new CsvTimeFormat((string)TimeFormat.SelectedValue, TimeZone.Text.Trim()));

    private async void AnalyzeCsv(object sender, RoutedEventArgs args)
    {
        if (TimeColumn.SelectedItem is not string time || ValueColumn.SelectedItem is not string value ||
            Role.SelectedItem is not string role || string.IsNullOrWhiteSpace(Unit.Text) || time == value)
        {
            StatusText.Text = Loc.T("cs.indiquez_deux_colonnes_distinctes_un");
            return;
        }
        await RunAsync(async token =>
        {
            var import = await _workspace.AnalyzeAndSaveAsync(new CsvMapping(time, value, role, Unit.Text.Trim()), token, ReadFormat());
            Analyze.IsEnabled = false;
            ShowImport(import);
            Imports.ItemsSource = _workspace.List();
            StatusText.Text = Loc.T("cs.import_et_rapport_enregistres");
            DataChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OpenImport(object sender, RoutedEventArgs args)
    {
        if (Imports.SelectedItem is not ImportSummary summary)
        {
            StatusText.Text = Loc.T("cs.selectionnez_un_import_enregistre");
            return;
        }
        try
        {
            ShowImport(_workspace.Open(summary.Id));
            Analyze.IsEnabled = false;
            StatusText.Text = Loc.T("cs.import_rouvert_et_empreinte_de");
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ExportOriginal(object sender, RoutedEventArgs args)
    {
        if (Imports.SelectedItem is not ImportSummary summary)
        {
            StatusText.Text = Loc.T("cs.selectionnez_un_import_enregistre");
            return;
        }
        try
        {
            var import = _workspace.Open(summary.Id);
            var dialog = new SaveFileDialog
            {
                FileName = import.Summary.FileName,
                Filter = Loc.T("cs.fichiers_csv_csv_csv"),
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;
            File.WriteAllBytes(dialog.FileName, import.Original);
            StatusText.Text = Loc.T("cs.fichier_original_exporte");
        }
        catch (Exception error) { ShowError(error); }
    }

    private void OpenAudit(object sender, RoutedEventArgs args)
    {
        try { new AuditWindow(_workspace) { Owner = Window.GetWindow(this) }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private void OpenIdentification(object sender, RoutedEventArgs args)
    {
        IdentificationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveDescription(object sender, RoutedEventArgs args)
    {
        try
        {
            _workspace.SaveLoopBrief(new(LoopTag.Text, LoopControlledQuantity.Text, LoopOperatingPoint.Text, LoopObjective.Text, Description.Text));
            StatusText.Text = Loc.T("cs.description_enregistree");
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error) { ShowError(error); }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        Controls.IsEnabled = false;
        StatusText.Text = Loc.T("cs.traitement_du_csv");
        SetState(DataState.Loading, "Chargement…");
        var token = _lifetime.Token;
        try { await operation(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { Controls.IsEnabled = true; }
    }

    private void ShowResult(CsvResult result)
    {
        TimeColumn.ItemsSource = result.Headers;
        ValueColumn.ItemsSource = result.Headers;
        TimeColumn.SelectedIndex = -1;
        ValueColumn.SelectedIndex = -1;
        Preview.Columns.Clear();
        for (var index = 0; index < result.Headers.Length; index++)
            Preview.Columns.Add(new DataGridTextColumn { Header = result.Headers[index], Binding = new Binding($"[{index}]") });
        Preview.ItemsSource = result.Preview;
        Report.Text = Loc.T("cs.apercu_lignes", result.Preview.Length, result.RowCount);
    }

    private void ShowImport(CsvImport import)
    {
        ShowResult(import.Result);
        TimeColumn.SelectedItem = import.Mapping.TimeColumn;
        ValueColumn.SelectedItem = import.Mapping.ValueColumn;
        Role.SelectedItem = import.Mapping.Role;
        Unit.Text = import.Mapping.Unit;
        Delimiter.SelectedValue = import.Delimiter;
        var format = import.Format ?? CsvFormat.Default;
        DecimalSeparator.SelectedValue = format.Decimal;
        TimeFormat.SelectedValue = format.Time?.Pattern ?? "";
        TimeZone.Text = format.Time?.Zone ?? TimeZone.Text;
        if (!(import.Format ?? CsvFormat.Default).IsDefault)
            Report.Text += Loc.T("cs.lecture", import.Format!.Describe());
        if (import.Acquisition is { } origin)
            Report.Text += Loc.T("cs.unite_de_la_mesure_de", origin.Describe(), origin.ProcessUnit, origin.CommandUnit);
        ShowTrend(import.Original, import.Summary.FileName, import.Mapping.ValueColumn, import.Mapping.Unit, import.Delimiter.Length > 0 ? import.Delimiter[0] : ',',
            (import.Format ?? CsvFormat.Default).Decimal is { Length: > 0 } dec ? dec[0] : '.', import.Mapping.TimeColumn);
        var quality = import.Result.Quality!;
        Report.Text += Loc.T("cs.sha_256_valeurs_finies_manquantes", import.Summary.FileName, import.Summary.Sha256, Terms.RoleName(import.Mapping.Role), import.Mapping.Unit, quality.ValidValues, quality.MissingValues, quality.InvalidValues, Format(quality.Minimum), Format(quality.Maximum), quality.InvalidTimestamps, quality.DuplicateTimestamps, quality.ReversedTimestamps, Format(quality.MinPositiveStepSeconds), Format(quality.MaxPositiveStepSeconds), quality.AlgorithmVersion, import.EngineVersion);
    }

    // ---------- trends and summary (display only: the raw columns of the file, nothing interpreted) ----------
    private enum DataState { None, Loading, Loaded, Error }

    private Trend? _trend;
    private string _trendSource = "";
    private string? _mappedColumn, _mappedUnit;
    private readonly HashSet<string> _hiddenSignals = new();

    private void SetState(DataState state, string text)
    {
        StateText.Text = text;
        var (back, fore) = state switch
        {
            DataState.Loaded => ("MkrSuccessPale", "MkrSuccess"),
            DataState.Error => ("MkrDangerPale", "MkrDanger"),
            DataState.Loading => ("MkrWarningPale", "MkrWarning"),
            _ => ("MkrSurfaceAlt", "MkrMuted")
        };
        StateBadge.SetResourceReference(Border.BackgroundProperty, back);
        StateBadge.SetResourceReference(Border.BorderBrushProperty, fore);
        StateText.SetResourceReference(TextBlock.ForegroundProperty, fore);
    }

    private void ShowTrend(byte[] bytes, string source, string? mappedColumn, string? unit, char delimiter, char decimalSeparator, string? timeColumn)
    {
        _trendSource = source; _mappedColumn = mappedColumn; _mappedUnit = unit;
        _trend = TrendReader.Read(bytes, delimiter, decimalSeparator, timeColumn);
        _hiddenSignals.Clear();
        SignalPicker.Children.Clear();
        if (_trend is null)
        {
            TrendChart.SetTracks(Array.Empty<ChartTrack>());
            TrendEmpty.Visibility = Visibility.Visible;
            SetState(DataState.Loaded, Loc.T("cs.donnees_chargees_tendance_non_disponible"));
            FillSummary(null);
            SignalPicker.Children.Add(new TextBlock { Text = Loc.T("cs.aucune_colonne_numerique_lisible_les"), TextWrapping = TextWrapping.Wrap });
            return;
        }
        // The first three numeric columns are drawn by default; the others stay one click away.
        for (var i = 0; i < _trend.Names.Length; i++)
        {
            var name = _trend.Names[i];
            if (i >= 3) _hiddenSignals.Add(name);
            var box = new CheckBox { Content = name + (name == _mappedColumn && !string.IsNullOrEmpty(_mappedUnit) ? $" ({_mappedUnit})" : ""), IsChecked = i < 3, Margin = new Thickness(0, 0, 0, 4), Tag = name };
            System.Windows.Automation.AutomationProperties.SetName(box, Loc.T("cs.afficher_le_signal") + name);
            box.Checked += (_, _) => { _hiddenSignals.Remove(name); DrawTrend(); };
            box.Unchecked += (_, _) => { _hiddenSignals.Add(name); DrawTrend(); };
            SignalPicker.Children.Add(box);
        }
        SetState(DataState.Loaded, Loc.T("cs.donnees_chargees"));
        FillSummary(_trend);
        DrawTrend();
        DataTabs.SelectedIndex = 0;
    }

    private void DrawTrend()
    {
        if (_trend is null) return;
        var tracks = new List<ChartTrack>();
        var colour = 1;
        for (var i = 0; i < _trend.Names.Length; i++)
        {
            var name = _trend.Names[i];
            if (_hiddenSignals.Contains(name)) continue;
            var unit = name == _mappedColumn ? _mappedUnit ?? "" : "";
            var points = Enumerable.Range(0, _trend.X.Length).Where(k => !double.IsNaN(_trend.X[k]) && !double.IsNaN(_trend.Values[i][k])).ToArray();
            tracks.Add(new ChartTrack(name, unit, new[] { new ChartSeries(name, "MkrSignal" + (colour++ % 4 + 1), points.Select(k => _trend.X[k]).ToArray(), points.Select(k => _trend.Values[i][k]).ToArray()) })
            { YLabel = unit.Length > 0 ? $"{name} ({unit})" : name, ShowLegend = false });
        }
        TrendChart.XLabel = _trend.HasTime ? Loc.T("cs.temps_depuis_le_premier_echantillon") : Loc.T("cs.numero_dechantillon");
        TrendChart.SetTracks(tracks);
        TrendEmpty.Visibility = tracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (tracks.Count == 0) TrendEmpty.Text = Loc.T("cs.aucun_signal_coche_cochez_au");
    }

    private void FillSummary(Trend? trend)
    {
        SummaryGrid.Children.Clear();
        SummaryGrid.RowDefinitions.Clear();
        var rows = new List<(string, string)> { ("Source", _trendSource) };
        if (trend is not null)
        {
            rows.Add(("Points", trend.Rows.ToString("N0", CultureInfo.CurrentCulture)));
            if (trend.Start is { } start && trend.End is { } end)
            {
                rows.Add((Loc.T("cs.periode"), $"{start.ToLocalTime():g} → {end.ToLocalTime():g}"));
                rows.Add((Loc.T("cs.duree"), TimeSpan.FromSeconds((end - start).TotalSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)));
            }
            if (trend.MedianStepSeconds is { } step) rows.Add((Loc.T("cs.periode_dechantillonnage"), $"{step.ToString("G4", CultureInfo.CurrentCulture)} s"));
            rows.Add((Loc.T("cs.colonnes_numeriques"), trend.Names.Length.ToString(CultureInfo.CurrentCulture)));
        }
        for (var r = 0; r < rows.Count; r++)
        {
            SummaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = rows[r].Item1, Margin = new Thickness(0, 0, 12, 4) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "MkrMuted");
            var value = new TextBlock { Text = rows[r].Item2, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            value.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
            Grid.SetRow(label, r); Grid.SetRow(value, r); Grid.SetColumn(value, 1);
            SummaryGrid.Children.Add(label); SummaryGrid.Children.Add(value);
        }
    }

    private static string Format(double? value) => value?.ToString("G6") ?? "—";

    private void ShowError(Exception error)
    {
        Trace.TraceError("csv.workspace {0}", error.GetType().Name);
        SetState(DataState.Error, Loc.T("cs.erreur_voir_le_message_en"));
        StatusText.Text = error is CsvInputException input ? input.Code switch
        {
            "csv_limit" => Loc.T("cs.le_csv_depasse_les_limites"),
            "csv_header" => Loc.T("cs.utilisez_de_1_a_64"),
            "csv_width" => Loc.T("cs.une_ligne_ne_correspond_pas"),
            "csv_empty" => Loc.T("cs.le_fichier_ne_contient_aucune"),
            "csv_encoding_or_syntax" => Loc.T("cs.le_csv_doit_etre_en"),
            "csv_mapping" => Loc.T("cs.le_mapping_doit_indiquer_deux"),
            "csv_format" => Loc.T("cs.format_de_lecture_invalide_une"),
            "csv_timezone" => Loc.T("cs.fuseau_horaire_inconnu_utilisez_un"),
            _ => Loc.T("cs.requete_csv_invalide")
        } : Loc.T("cs.operation_impossible_verifiez_le_stockage");
    }
}

/// <summary>Shows the plain name of a signal role in selection lists while the stored value stays the role code.</summary>
internal sealed class RoleNameConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is string code ? Terms.RoleLabel(code) : value ?? "";
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
