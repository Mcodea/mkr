using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Mkr.Desktop;

public partial class BlockCatalogueWindow : Window
{
    private sealed record Card(string Name, string Target, string Status, string Evidence);
    private readonly List<Card> _cards = new();

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
            var evidence = string.Join("\n\n", facts);
            _cards.Add(new Card(Read("name")!,
                $"{Read("manufacturer")} · {Read("software")}\nCPU : {Read("cpu") ?? "à vérifier"}\nVersion logicielle : {Read("softwareVersion") ?? "à vérifier"}",
                Read("status") == "verified" ? "Fiche documentaire vérifiée" : "Compatibilité à vérifier",
                evidence.Length > 0 ? evidence : "Aucune propriété documentée dans cette édition du catalogue."));
        }
        Refresh();
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
