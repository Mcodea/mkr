using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>
/// Diagnostic of the imported data: only what the CSV analysis measured (counts of valid, missing and invalid values, timestamp faults, sampling steps).
/// It states facts and flags what needs a decision; it never declares the data « exploitable ».
/// </summary>
public partial class DiagnosticView : UserControl, IStepView
{
    private readonly CsvWorkspace _workspace;

    internal DiagnosticView(CsvWorkspace workspace)
    {
        InitializeComponent();
        _workspace = workspace;
        var imports = workspace.List().OrderByDescending(item => item.ImportedAt).ToList();
        Imports.ItemsSource = imports;
        Empty.Visibility = imports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (imports.Count > 0) Imports.SelectedIndex = 0;
        else Findings.Text = Loc.T("cs.aucun_import_rien_a_diagnostiquer");
    }

    public void Cancel() { }

    private static string N(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
    private static string G(double? value) => value is { } v ? v.ToString("G6", CultureInfo.CurrentCulture) : "—";

    private void ImportChanged(object sender, SelectionChangedEventArgs args)
    {
        if (Imports.SelectedItem is not ImportSummary summary) return;
        CsvImport import;
        try { import = _workspace.Open(summary.Id); }
        catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or KeyNotFoundException)
        {
            Findings.Text = Loc.T("cs.impossible_de_relire_cet_import");
            return;
        }
        Preview.Columns.Clear();
        for (var index = 0; index < import.Result.Headers.Length; index++)
            Preview.Columns.Add(new DataGridTextColumn { Header = import.Result.Headers[index], Binding = new Binding($"[{index}]") });
        Preview.ItemsSource = import.Result.Preview;
        PreviewTitle.Text = Loc.T("cs.lignes_source_affichees_sur_valeurs", import.Result.Preview.Length, N(import.Result.RowCount));

        Quality.Children.Clear();
        var q = import.Result.Quality;
        var findings = new List<string>();
        if (q is null)
        {
            Findings.Text = Loc.T("cs.cet_import_n_a_pas");
        }
        else
        {
            void Line(string label, string value, bool flag = false) => Quality.Children.Add(Row(label, value, flag));
            Line(Loc.T("cs.role_unite"), $"{import.Mapping.Role} · {import.Mapping.Unit}");
            Line(Loc.T("cs.valeurs_valides"), N(q.ValidValues));
            Line(Loc.T("cs.valeurs_manquantes"), N(q.MissingValues), q.MissingValues > 0);
            Line(Loc.T("cs.valeurs_invalides"), N(q.InvalidValues), q.InvalidValues > 0);
            Line(Loc.T("cs.horodatages_invalides"), N(q.InvalidTimestamps), q.InvalidTimestamps > 0);
            Line(Loc.T("cs.horodatages_en_double"), N(q.DuplicateTimestamps), q.DuplicateTimestamps > 0);
            Line(Loc.T("cs.horodatages_a_rebours"), N(q.ReversedTimestamps), q.ReversedTimestamps > 0);
            Line(Loc.T("cs.minimum_maximum"), $"{G(q.Minimum)} / {G(q.Maximum)}");
            Line(Loc.T("cs.pas_d_echantillonnage_s"), $"{G(q.MinPositiveStepSeconds)} à {G(q.MaxPositiveStepSeconds)}");
            if (q.MissingValues > 0) findings.Add(Loc.T("cs.valeur_s_manquante_s_a", N(q.MissingValues)));
            if (q.InvalidValues > 0) findings.Add(Loc.T("cs.valeur_s_non_numeriques_verifiez", N(q.InvalidValues)));
            if (q.InvalidTimestamps + q.DuplicateTimestamps + q.ReversedTimestamps > 0) findings.Add(Loc.T("cs.horodatages_incoherents_l_identification_exige"));
            if (q.MinPositiveStepSeconds is { } min && q.MaxPositiveStepSeconds is { } max && min > 0 && max > 1.5 * min)
                findings.Add(Loc.T("cs.echantillonnage_irregulier_pas_de_s", G(min), G(max)));
            var flagged = findings.Count;          // real findings only: the « no anomaly » line below is not one
            if (flagged == 0) findings.Add(Loc.T("cs.aucune_anomalie_de_comptage_ni"));
            Findings.Text = string.Join("\n", findings.Select(line => "· " + line));
            // The verdict restates the findings above (counts and the sampling rule already shown): it adds no criterion and never declares the data fit for use.
            SetVerdict(flagged == 0 ? Loc.T("cs.aucune_anomalie_de_comptage_ni_2") : Loc.T("cs.constat_s_a_traiter_avant", flagged), flagged == 0 ? "MkrSuccess" : "MkrWarning", flagged == 0 ? "MkrSuccessPale" : "MkrWarningPale");
        }
        if (q is null) SetVerdict(Loc.T("cs.import_non_analyse_lancez_analyser"), "MkrMuted", "MkrSurfaceAlt");
        ShowGraphs(import, q);
        Provenance.Text = Loc.T("cs.sha_256_importe_le_moteur", summary.FileName, summary.Sha256, summary.ImportedAt.ToLocalTime(), import.EngineVersion) +
                          (q is null ? "" : Loc.T("cs.algorithme", q.AlgorithmVersion)) + Loc.T("cs.lecture_2", (import.Format ?? CsvFormat.Default).Describe());
    }

    private void SetVerdict(string text, string foreground, string background)
    {
        VerdictText.Text = text;
        VerdictText.SetResourceReference(TextBlock.ForegroundProperty, foreground);
        VerdictBadge.SetResourceReference(Border.BackgroundProperty, background);
        VerdictBadge.SetResourceReference(Border.BorderBrushProperty, foreground);
    }

    /// <summary>Plots the numeric columns of the import (raw values) and marks, at their instants, the faults the analysis counts: missing values of the analysed signal and sampling steps beyond 1.5 × the smallest step.</summary>
    private void ShowGraphs(CsvImport import, CsvQuality? quality)
    {
        var format = import.Format ?? CsvFormat.Default;
        var trend = TrendReader.Read(import.Original, import.Delimiter.Length > 0 ? import.Delimiter[0] : ',', format.Decimal is { Length: > 0 } d ? d[0] : '.', import.Mapping.TimeColumn);
        if (trend is null) { TrendChart.SetTracks(Array.Empty<ChartTrack>()); TrendChart.SetEvents(Array.Empty<(double, string)>()); TrendEmpty.Visibility = Visibility.Visible; return; }
        TrendEmpty.Visibility = Visibility.Collapsed;
        var tracks = new List<ChartTrack>();
        var order = Enumerable.Range(0, trend.Names.Length).OrderBy(i => trend.Names[i] == import.Mapping.ValueColumn ? 0 : 1).Take(3).ToList();
        var colour = 1;
        foreach (var i in order)
        {
            var name = trend.Names[i];
            var unit = name == import.Mapping.ValueColumn ? import.Mapping.Unit : "";
            var points = Enumerable.Range(0, trend.X.Length).Where(k => !double.IsNaN(trend.X[k]) && !double.IsNaN(trend.Values[i][k])).ToArray();
            tracks.Add(new ChartTrack(name, unit, new[] { new ChartSeries(name, "MkrSignal" + (colour++ % 4 + 1), points.Select(k => trend.X[k]).ToArray(), points.Select(k => trend.Values[i][k]).ToArray()) })
            { YLabel = unit.Length > 0 ? $"{name} ({unit})" : name });
        }
        TrendChart.XLabel = trend.HasTime ? Loc.T("cs.temps_depuis_le_premier_echantillon") : Loc.T("cs.numero_dechantillon");
        TrendChart.SetTracks(tracks);
        var events = new List<(double X, string Label)>();
        var mapped = Array.IndexOf(trend.Names, import.Mapping.ValueColumn);
        if (mapped >= 0)
            for (var k = 0; k < trend.X.Length && events.Count < 20; k++)
                if (double.IsNaN(trend.Values[mapped][k]) && !double.IsNaN(trend.X[k])) events.Add((trend.X[k], Loc.T("cs.valeur_manquante")));
        if (trend.HasTime && quality?.MinPositiveStepSeconds is { } min && min > 0)
            for (var k = 1; k < trend.X.Length && events.Count < 40; k++)
                if (!double.IsNaN(trend.X[k]) && !double.IsNaN(trend.X[k - 1]) && trend.X[k] - trend.X[k - 1] > 1.5 * min) events.Add((trend.X[k - 1], Loc.T("cs.pas_irregulier")));
        TrendChart.SetEvents(events);
    }

    private UIElement Row(string label, string value, bool flag)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBlock { Text = label, Margin = new Thickness(10, 5, 8, 5), TextWrapping = TextWrapping.Wrap };
        name.SetResourceReference(TextBlock.ForegroundProperty, "MkrText");
        grid.Children.Add(name);
        var text = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 5, 10, 5), TextAlignment = TextAlignment.Right };
        text.SetResourceReference(TextBlock.ForegroundProperty, flag ? "MkrDanger" : "MkrText");
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var border = new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, 1) };
        border.SetResourceReference(Border.BorderBrushProperty, "MkrBorder");
        return border;
    }
}
