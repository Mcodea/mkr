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
        else Findings.Text = "Aucun import : rien à diagnostiquer.";
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
            Findings.Text = "Impossible de relire cet import. Vérifiez le stockage local.";
            return;
        }
        Preview.Columns.Clear();
        for (var index = 0; index < import.Result.Headers.Length; index++)
            Preview.Columns.Add(new DataGridTextColumn { Header = import.Result.Headers[index], Binding = new Binding($"[{index}]") });
        Preview.ItemsSource = import.Result.Preview;
        PreviewTitle.Text = $"Lignes source : {import.Result.Preview.Length} affichées sur {N(import.Result.RowCount)} (valeurs brutes inchangées)";

        Quality.Children.Clear();
        var q = import.Result.Quality;
        var findings = new List<string>();
        if (q is null)
        {
            Findings.Text = "Cet import n'a pas encore été analysé (étape Données : « Analyser et enregistrer »).";
        }
        else
        {
            void Line(string label, string value, bool flag = false) => Quality.Children.Add(Row(label, value, flag));
            Line("Rôle / unité", $"{import.Mapping.Role} · {import.Mapping.Unit}");
            Line("Valeurs valides", N(q.ValidValues));
            Line("Valeurs manquantes", N(q.MissingValues), q.MissingValues > 0);
            Line("Valeurs invalides", N(q.InvalidValues), q.InvalidValues > 0);
            Line("Horodatages invalides", N(q.InvalidTimestamps), q.InvalidTimestamps > 0);
            Line("Horodatages en double", N(q.DuplicateTimestamps), q.DuplicateTimestamps > 0);
            Line("Horodatages à rebours", N(q.ReversedTimestamps), q.ReversedTimestamps > 0);
            Line("Minimum / maximum", $"{G(q.Minimum)} / {G(q.Maximum)}");
            Line("Pas d'échantillonnage (s)", $"{G(q.MinPositiveStepSeconds)} à {G(q.MaxPositiveStepSeconds)}");
            if (q.MissingValues > 0) findings.Add($"{N(q.MissingValues)} valeur(s) manquante(s) : à traiter (suppression ou interpolation sur une courte période) avant l'identification.");
            if (q.InvalidValues > 0) findings.Add($"{N(q.InvalidValues)} valeur(s) non numériques : vérifiez le séparateur décimal et la colonne choisie.");
            if (q.InvalidTimestamps + q.DuplicateTimestamps + q.ReversedTimestamps > 0) findings.Add("Horodatages incohérents : l'identification exige une base de temps croissante.");
            if (q.MinPositiveStepSeconds is { } min && q.MaxPositiveStepSeconds is { } max && min > 0 && max > 1.5 * min)
                findings.Add($"Échantillonnage irrégulier (pas de {G(min)} s à {G(max)} s) : à rééchantillonner ou à signaler avant l'identification.");
            if (findings.Count == 0) findings.Add("Aucune anomalie de comptage ni d'horodatage détectée. L'excitation de la commande et le bruit de mesure sont évalués à l'étape Identification.");
            Findings.Text = string.Join("\n", findings.Select(line => "· " + line));
        }
        Provenance.Text = $"{summary.FileName}\nSHA-256 : {summary.Sha256}\nImporté le {summary.ImportedAt.ToLocalTime():g}\nMoteur d'analyse {import.EngineVersion}" +
                          (q is null ? "" : $" · algorithme {q.AlgorithmVersion}") + $"\nLecture : {(import.Format ?? CsvFormat.Default).Describe()}";
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
