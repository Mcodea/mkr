using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class CsvWindow : Window
{
    private readonly CsvWorkspace _workspace;
    private readonly IdentificationWorkspace _identification;
    private readonly CancellationTokenSource _lifetime = new();

    internal CsvWindow(CsvWorkspace workspace, IdentificationWorkspace identification)
    {
        InitializeComponent();
        _workspace = workspace;
        _identification = identification;
        Title = $"MKR — {workspace.ProjectName}";
        Delimiter.SelectionChanged += (_, _) => Analyze.IsEnabled = false;
        Role.ItemsSource = new[] { "SP", "PV", "CV", "MV", "d" };
        Description.Text = workspace.ReadDescription();
        Imports.ItemsSource = workspace.List();
        StatusText.Text = "Choisissez un CSV ou ouvrez un import enregistré.";
        Closed += (_, _) => { _lifetime.Cancel(); _lifetime.Dispose(); };
    }

    private async void ChooseCsv(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFileDialog { Filter = "Fichiers CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true)
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
            Analyze.IsEnabled = true;
            StatusText.Text = "Sélectionnez les colonnes, le rôle et l’unité. L’horodatage doit inclure un décalage UTC ou Z.";
        });
    }

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
            var import = await _workspace.AnalyzeAndSaveAsync(new CsvMapping(time, value, role, Unit.Text.Trim()), token);
            Analyze.IsEnabled = false;
            ShowImport(import);
            Imports.ItemsSource = _workspace.List();
            StatusText.Text = "Import et rapport enregistrés.";
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
            if (dialog.ShowDialog(this) != true)
                return;
            File.WriteAllBytes(dialog.FileName, import.Original);
            StatusText.Text = "Fichier original exporté.";
        }
        catch (Exception error) { ShowError(error); }
    }

    private void OpenIdentification(object sender, RoutedEventArgs args)
    {
        try { new IdentificationWindow(_identification) { Owner = this }.ShowDialog(); }
        catch (Exception error) { ShowError(error); }
    }

    private void SaveDescription(object sender, RoutedEventArgs args)
    {
        try
        {
            _workspace.SaveDescription(Description.Text);
            StatusText.Text = "Description enregistrée.";
        }
        catch (Exception error) { ShowError(error); }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        Controls.IsEnabled = false;
        StatusText.Text = "Traitement du CSV…";
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
        var quality = import.Result.Quality!;
        Report.Text += $"  |  {import.Summary.FileName}  |  SHA-256 : {import.Summary.Sha256}\n" +
            $"{import.Mapping.Role} ({import.Mapping.Unit}) · valeurs finies : {quality.ValidValues}, " +
            $"manquantes : {quality.MissingValues}, invalides : {quality.InvalidValues}. " +
            $"Min./max. : {Format(quality.Minimum)} / {Format(quality.Maximum)}.\n" +
            $"Horodatages invalides : {quality.InvalidTimestamps}, doublons : {quality.DuplicateTimestamps}, " +
            $"inversions : {quality.ReversedTimestamps}. Pas positif min./max. : " +
            $"{Format(quality.MinPositiveStepSeconds)} / {Format(quality.MaxPositiveStepSeconds)} s. " +
            $"Méthode {quality.AlgorithmVersion} · moteur {import.EngineVersion}.";
    }

    private static string Format(double? value) => value?.ToString("G6") ?? "—";

    private void ShowError(Exception error)
    {
        Trace.TraceError("csv.workspace {0}", error.GetType().Name);
        StatusText.Text = error is CsvInputException input ? input.Code switch
        {
            "csv_limit" => "Le CSV dépasse les limites de 1 Mio ou 10 000 lignes.",
            "csv_header" => "Utilisez de 1 à 64 en-têtes distincts et non vides (120 caractères maximum).",
            "csv_width" => "Une ligne ne correspond pas au nombre de colonnes. Vérifiez le séparateur.",
            "csv_empty" => "Le fichier ne contient aucune ligne de données.",
            "csv_encoding_or_syntax" => "Le CSV doit être en UTF-8 et respecter la syntaxe des champs entre guillemets.",
            "csv_mapping" => "Le mapping doit indiquer deux colonnes distinctes, un rôle et une unité.",
            _ => "Requête CSV invalide."
        } : "Opération impossible. Vérifiez le stockage local, le fichier et la disponibilité de Python.";
    }
}
