using Mkr.Core;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Mkr.Desktop;

public partial class BlockCatalogueWindow : Window
{
    private sealed record Card(string Name, string Target, string Status, string Evidence, string BenchSheet);
    private readonly List<Card> _cards = new();

    internal static void OpenFor(Window owner, Func<LoopBrief>? readBrief = null)
    {
        try
        {
            new BlockCatalogueWindow(readBrief?.Invoke()) { Owner = owner }.ShowDialog();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("catalogue.open {0}", error.GetType().Name);
            MessageBox.Show(owner, Loc.T("cs.impossible_de_lire_le_catalogue"), "ORIAS",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private BlockCatalogueWindow(LoopBrief? brief)
    {
        InitializeComponent();
        var context = new List<string>();
        if (brief is not null)
        {
            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) context.Add($"{label} : {value}");
            }
            Add(Loc.T("cs.repere"), brief.Tag);
            Add(Loc.T("cs.grandeur_regulee"), brief.ControlledQuantity);
            Add(Loc.T("cs.point_de_fonctionnement"), brief.OperatingPoint);
            Add("Objectif", brief.Objective);
            Add("Description", brief.Description);
        }
        if (context.Count > 0)
        {
            context.Insert(0, Loc.T("cs.fiche_de_boucle_enregistree"));
            LoopContext.Text = string.Join(Environment.NewLine, context);
            LoopContext.Visibility = Visibility.Visible;
        }
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
                $"ORIAS — Relevé de banc · {Read("name")}",
                $"Catalogue : {root.GetProperty("catalogueVersion").GetString()}",
                $"Constructeur : {Read("manufacturer")} · Logiciel : {Read("software")}",
                Loc.T("cs.statut_initial_essai_a_realiser"),
                "",
            };
            if (context.Count > 0)
            {
                sheet.AddRange(context);
                sheet.Add("");
            }
            sheet.AddRange(new[]
            {
                Loc.T("cs.identification_de_la_configuration_observee"),
                Loc.T("cs.reference_cpu"),
                Loc.T("cs.version_firmware"),
                Loc.T("cs.version_du_logiciel"),
                Loc.T("cs.nom_et_version_de_bibliotheque"),
                Loc.T("cs.instance_du_bloc_et_periode"),
                Loc.T("cs.plage_et_unite_de_mesure"),
                Loc.T("cs.plage_et_unite_de_commande"),
                Loc.T("cs.sens_daction_et_signe_du"),
                Loc.T("cs.unite_de_ti_et_de"),
                "",
                Loc.T("cs.parametres_releves_sur_linstance")
            });
            foreach (var parameter in block.GetProperty("parameters").EnumerateArray())
            {
                var source = sources[parameter.GetProperty("source").GetString()!];
                sheet.Add($"{parameter.GetProperty("name").GetString()} [{source}] — valeur observée :");
            }
            if (block.GetProperty("parameters").GetArrayLength() == 0)
                sheet.Add(Loc.T("cs.aucun_parametre_documente_dans_cette"));
            sheet.AddRange(new[]
            {
                "", Loc.T("cs.essai_et_revue"),
                Loc.T("cs.identifiant_de_letude_et_de"),
                Loc.T("cs.conditions_initiales_et_scenario"),
                Loc.T("cs.resultat_attendu_et_tolerance_fixee"),
                Loc.T("cs.resultat_observe_et_ecart"),
                Loc.T("cs.reference_de_la_trace_de"),
                Loc.T("cs.date_operateur_et_relecteur"),
                Loc.T("cs.conclusion_de_la_revue")
            });
            _cards.Add(new Card(Read("name")!,
                $"{Read("manufacturer")} · {Read("software")}\nCPU : {Read("cpu") ?? "à vérifier"}\nVersion logicielle : {Read("softwareVersion") ?? "à vérifier"}",
                Read("status") == "verified" ? Loc.T("cs.fiche_documentaire_verifiee") : Loc.T("cs.compatibilite_a_verifier"),
                evidence.Length > 0 ? evidence : Loc.T("cs.aucune_propriete_documentee_dans_cette"),
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
            CopyStatus.Text = Loc.T("cs.releve_copie", card.Name);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            CopyStatus.Text = Loc.T("cs.presse_papiers_indisponible_reessayez");
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
