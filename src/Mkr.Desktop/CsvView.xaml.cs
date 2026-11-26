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
        TimeFormat.Items.Add(new ComboBoxItem { Content = "ISO 8601 avec décalage (Z ou +hh:mm)", Tag = "" });
        foreach (var (pattern, label) in CsvFormat.Patterns)
            TimeFormat.Items.Add(new ComboBoxItem { Content = "Heure locale · " + label, Tag = pattern });
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
        StatusText.Text = "Choisissez un CSV ou ouvrez un import enregistré.";
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
            StatusText.Text = "Acquisition enregistrée dans le projet.";
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void ChooseCsv(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog { Filter = "Fichiers CSV (*.csv)|*.csv" };
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
            StatusText.Text = "Sélectionnez les colonnes, le rôle et l’unité.";
            if (result.SuggestedFormat is { } suggestion) ApplySuggestion(suggestion);
        });
    }

    /// <summary>The proposal only fills the controls: the user confirms it by importing, or corrects it.</summary>
    private void ApplySuggestion(CsvFormatSuggestion suggestion)
    {
        DecimalSeparator.SelectedValue = suggestion.Decimal;
        TimeFormat.SelectedValue = suggestion.TimePattern ?? "";
        var local = suggestion.TimePattern is not null;
        StatusText.Text = "Format proposé d’après les premières lignes : " +
            (suggestion.Decimal == "," ? "décimale virgule" : "décimale point") + " · " +
            (local ? $"heure locale (colonne {suggestion.TimeColumn}) : vérifiez le fuseau avant d’importer."
                   : suggestion.IsoWithOffset ? "horodatage ISO 8601 avec décalage."
                   : "aucune colonne de date reconnue : choisissez la notation.") +
            " Sélectionnez les colonnes, le rôle et l’unité.";
    }

    private CsvFormat ReadFormat() => new((string)DecimalSeparator.SelectedValue,
        string.IsNullOrEmpty((string?)TimeFormat.SelectedValue) ? null
            : new CsvTimeFormat((string)TimeFormat.SelectedValue, TimeZone.Text.Trim()));

    private async void AnalyzeCsv(object sender, RoutedEventArgs args)
    {
        if (TimeColumn.SelectedItem is not string time || ValueColumn.SelectedItem is not string value ||
            Role.SelectedItem is not string role || string.IsNullOrWhiteSpace(Unit.Text) || time == value)
        {
            StatusText.Text = "Indiquez deux colonnes distinctes, un rôle et une unité.";
            return;
        }
        await RunAsync(async token =>
        {
            var import = await _workspace.AnalyzeAndSaveAsync(new CsvMapping(time, value, role, Unit.Text.Trim()), token, ReadFormat());
            Analyze.IsEnabled = false;
            ShowImport(import);
            Imports.ItemsSource = _workspace.List();
            StatusText.Text = "Import et rapport enregistrés.";
            DataChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OpenImport(object sender, RoutedEventArgs args)
    {
        if (Imports.SelectedItem is not ImportSummary summary)
        {
            StatusText.Text = "Sélectionnez un import enregistré.";
            return;
        }
        try
        {
            ShowImport(_workspace.Open(summary.Id));
            Analyze.IsEnabled = false;
            StatusText.Text = "Import rouvert et empreinte de l’original vérifiée.";
        }
        catch (Exception error) { ShowError(error); }
    }

    private void ExportOriginal(object sender, RoutedEventArgs args)
    {
        if (Imports.SelectedItem is not ImportSummary summary)
        {
            StatusText.Text = "Sélectionnez un import enregistré.";
            return;
        }
        try
        {
            var import = _workspace.Open(summary.Id);
            var dialog = new SaveFileDialog
            {
                FileName = import.Summary.FileName,
                Filter = "Fichiers CSV (*.csv)|*.csv",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;
            File.WriteAllBytes(dialog.FileName, import.Original);
            StatusText.Text = "Fichier original exporté.";
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
            StatusText.Text = "Description enregistrée.";
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error) { ShowError(error); }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        Controls.IsEnabled = false;
        StatusText.Text = "Traitement du CSV…";
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
        Report.Text = $"Aperçu : {result.Preview.Length} / {result.RowCount} lignes.";
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
            Report.Text += $"  |  Lecture : {import.Format!.Describe()}.";
        if (import.Acquisition is { } origin)
            Report.Text += $"  |  {origin.Describe()} · unité de la mesure {origin.ProcessUnit}, de la sortie de commande {origin.CommandUnit}.";
        ShowTrend(import.Original, import.Summary.FileName, import.Mapping.ValueColumn, import.Mapping.Unit, import.Delimiter.Length > 0 ? import.Delimiter[0] : ',',
            (import.Format ?? CsvFormat.Default).Decimal is { Length: > 0 } dec ? dec[0] : '.', import.Mapping.TimeColumn);
        var quality = import.Result.Quality!;
        Report.Text += $"  |  {import.Summary.FileName}  |  SHA-256 : {import.Summary.Sha256}\n" +
            $"{Terms.RoleName(import.Mapping.Role)} ({import.Mapping.Unit}) · valeurs finies : {quality.ValidValues}, " +
            $"manquantes : {quality.MissingValues}, invalides : {quality.InvalidValues}. " +
            $"Min./max. : {Format(quality.Minimum)} / {Format(quality.Maximum)}.\n" +
            $"Horodatages invalides : {quality.InvalidTimestamps}, doublons : {quality.DuplicateTimestamps}, " +
            $"inversions : {quality.ReversedTimestamps}. Pas positif min./max. : " +
            $"{Format(quality.MinPositiveStepSeconds)} / {Format(quality.MaxPositiveStepSeconds)} s. " +
            $"Méthode {quality.AlgorithmVersion} · moteur {import.EngineVersion}.";
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
            SetState(DataState.Loaded, "Données chargées (tendance non disponible)");
            FillSummary(null);
            SignalPicker.Children.Add(new TextBlock { Text = "Aucune colonne numérique lisible : les lignes source restent disponibles dans « Données brutes ».", TextWrapping = TextWrapping.Wrap });
            return;
        }
        // The first three numeric columns are drawn by default; the others stay one click away.
        for (var i = 0; i < _trend.Names.Length; i++)
        {
            var name = _trend.Names[i];
            if (i >= 3) _hiddenSignals.Add(name);
            var box = new CheckBox { Content = name + (name == _mappedColumn && !string.IsNullOrEmpty(_mappedUnit) ? $" ({_mappedUnit})" : ""), IsChecked = i < 3, Margin = new Thickness(0, 0, 0, 4), Tag = name };
            System.Windows.Automation.AutomationProperties.SetName(box, "Afficher le signal " + name);
            box.Checked += (_, _) => { _hiddenSignals.Remove(name); DrawTrend(); };
            box.Unchecked += (_, _) => { _hiddenSignals.Add(name); DrawTrend(); };
            SignalPicker.Children.Add(box);
        }
        SetState(DataState.Loaded, "Données chargées");
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
        TrendChart.XLabel = _trend.HasTime ? "Temps depuis le premier échantillon (s)" : "Numéro d’échantillon";
        TrendChart.SetTracks(tracks);
        TrendEmpty.Visibility = tracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (tracks.Count == 0) TrendEmpty.Text = "Aucun signal coché : cochez au moins une colonne dans « Signaux affichés ».";
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
                rows.Add(("Période", $"{start.ToLocalTime():g} → {end.ToLocalTime():g}"));
                rows.Add(("Durée", TimeSpan.FromSeconds((end - start).TotalSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)));
            }
            if (trend.MedianStepSeconds is { } step) rows.Add(("Période d’échantillonnage", $"{step.ToString("G4", CultureInfo.CurrentCulture)} s"));
            rows.Add(("Colonnes numériques", trend.Names.Length.ToString(CultureInfo.CurrentCulture)));
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
        SetState(DataState.Error, "Erreur : voir le message en bas de l’écran");
        StatusText.Text = error is CsvInputException input ? input.Code switch
        {
            "csv_limit" => "Le CSV dépasse les limites de 1 Mio ou 10 000 lignes.",
            "csv_header" => "Utilisez de 1 à 64 en-têtes distincts et non vides (120 caractères maximum).",
            "csv_width" => "Une ligne ne correspond pas au nombre de colonnes. Vérifiez le séparateur.",
            "csv_empty" => "Le fichier ne contient aucune ligne de données.",
            "csv_encoding_or_syntax" => "Le CSV doit être en UTF-8 et respecter la syntaxe des champs entre guillemets.",
            "csv_mapping" => "Le mapping doit indiquer deux colonnes distinctes, un rôle et une unité.",
            "csv_format" => "Format de lecture invalide : une décimale virgule exige le séparateur point-virgule, et le fuseau doit être un nom IANA (par exemple Europe/Paris ou UTC).",
            "csv_timezone" => "Fuseau horaire inconnu. Utilisez un nom IANA, par exemple Europe/Paris, Europe/Brussels ou UTC.",
            _ => "Requête CSV invalide."
        } : "Opération impossible. Vérifiez le stockage local, le fichier et la disponibilité de Python.";
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
