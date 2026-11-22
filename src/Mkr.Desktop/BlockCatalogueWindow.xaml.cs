using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Mkr.Desktop;

public partial class BlockCatalogueWindow : Window
{
    private sealed record Card(string Name, string Target, string Status, string Evidence, string BenchSheet);
    private readonly List<Card> _cards = new();

    internal static void OpenFor(Window owner)
    {
        try
        {
            new BlockCatalogueWindow { Owner = owner }.ShowDialog();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("catalogue.open {0}", error.GetType().Name);
            MessageBox.Show(owner, "Impossible de lire le catalogue installé.", "MKR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public BlockCatalogueWindow()
    {
        InitializeComponent();
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "engine", "catalogue", "schneider.json"));
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var sources = root.GetProperty("sources").EnumerateArray().ToDictionary(
            source => source.GetProperty("id").GetString()!,
            source => $"{source.GetProperty("document").GetString()}, {source.GetProperty("section").GetString()}");
        foreach (var block in root.GetProperty("blocks").EnumerateArray())
        {
            string? Read(string key) => block.GetProperty(key).GetString();
            var facts = block.GetProperty("facts").EnumerateArray().Select(fact =>
                $"{fact.GetProperty("claim").GetString()}\n{sources[fact.GetProperty("source").GetString()!]}");
            var parameters = block.GetProperty("parameters").EnumerateArray().Select(parameter =>
            {
                string? Detail(string key) => parameter.TryGetProperty(key, out var value) ? value.GetString() : null;
                var source = sources[parameter.GetProperty("source").GetString()!];
                return $"{Detail("name")} {Detail("iecType")} · {Detail("label")} · {Detail("unit")}\n{source}";
            });
            var evidence = string.Join("\n\n", parameters.Concat(facts));
            var sheet = new List<string>
            {
                $"MKR — Relevé de banc · {Read("name")}",
                $"Catalogue : {root.GetProperty("catalogueVersion").GetString()}",
                $"Constructeur : {Read("manufacturer")} · Logiciel : {Read("software")}",
                "Statut initial : essai à réaliser",
                "",
                "Identification de la configuration observée",
                "Référence CPU :",
                "Version firmware :",
                "Version du logiciel :",
                "Nom et version de bibliothèque :",
                "Instance du bloc et période d’exécution :",
                "Plage et unité de mesure :",
                "Plage et unité de commande :",
                "",
                "Paramètres relevés sur l’instance"
            };
            foreach (var parameter in block.GetProperty("parameters").EnumerateArray())
            {
                var source = sources[parameter.GetProperty("source").GetString()!];
                sheet.Add($"{parameter.GetProperty("name").GetString()} [{source}] — valeur observée :");
            }
            if (block.GetProperty("parameters").GetArrayLength() == 0)
                sheet.Add("Aucun paramètre documenté dans cette fiche.");
            sheet.AddRange(new[]
            {
                "", "Essai et revue",
                "Identifiant de l’étude et de l’essai :",
                "Conditions initiales et scénario :",
                "Résultat attendu et tolérance fixée avant essai :",
                "Résultat observé et écart :",
                "Référence de la trace de mesure :",
                "Date, opérateur et relecteur :",
                "Conclusion de la revue :"
            });
            _cards.Add(new Card(Read("name")!,
                $"{Read("manufacturer")} · {Read("software")}\nCPU : {Read("cpu") ?? "à vérifier"}\nVersion logicielle : {Read("softwareVersion") ?? "à vérifier"}",
                Read("status") == "verified" ? "Fiche documentaire vérifiée" : "Compatibilité à vérifier",
                evidence.Length > 0 ? evidence : "Aucune propriété documentée dans cette édition du catalogue.",
                string.Join(Environment.NewLine, sheet)));
        }
        Refresh();
    }

    private void CopyBenchSheet(object sender, RoutedEventArgs args)
    {
        if (Blocks.SelectedItem is not Card card) return;
        try
        {
            Clipboard.SetText(card.BenchSheet);
            CopyStatus.Text = $"Relevé {card.Name} copié.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            CopyStatus.Text = "Presse-papiers indisponible. Réessayez.";
        }
    }

    private void ClearCopyStatus(object sender, SelectionChangedEventArgs args)
    {
        if (CopyStatus is not null) CopyStatus.Text = "";
    }

    private void ApplyFilter(object sender, TextChangedEventArgs args)
    {
        if (Blocks is not null) Refresh();
    }

    private void Refresh()
    {
        var filter = Filter.Text.Trim();
        Blocks.ItemsSource = _cards.Where(card => (card.Name + " " + card.Target)
            .Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (Blocks.Items.Count > 0) Blocks.SelectedIndex = 0;
    }
}
