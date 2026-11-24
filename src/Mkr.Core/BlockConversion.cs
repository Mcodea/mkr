using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mkr.Core;

public sealed record BlockTime(double Seconds, string Iec);

/// <summary>PIDFF parameters that follow from a MKR tuning (mixed structure). Names are the Control Expert names.</summary>
public sealed record PidffValues(double Kp, BlockTime Ti, BlockTime Td, double? Kd,
    [property: JsonPropertyName("rev_dir")] bool RevDir,
    [property: JsonPropertyName("mix_par")] bool MixPar,
    [property: JsonPropertyName("pv_dev")] bool PvDev);

public sealed record DocumentedEquation(int Page, string Name, string Text);

public sealed record BlockSource(string Document, string Edition, string Library, string Block);

/// <summary>
/// Documentary conversion of a MKR tuning to a manufacturer block. It is NEVER qualified and never ready to load:
/// qualification needs the bench tests on the real controller and software version (AGENTS.md §7).
/// </summary>
public sealed record BlockConversion(string Block, string Structure, string Qualification, bool ReadyToLoad,
    BlockSource Source, PidffValues Parameters, Dictionary<string, JsonElement> Neutral, string[] ToDetermine,
    DocumentedEquation[] Equations, string[] Warnings)
{
    private static string Text(double value) => value.ToString("G6", CultureInfo.CurrentCulture);

    public string Describe(string methodLabel, string mvUnit, string pvUnit)
    {
        var p = Parameters;
        var text = new StringBuilder();
        text.AppendLine($"{Block} · structure {(Structure == "mixed" ? "mixte (mix_par = 0)" : Structure)} · {methodLabel}");
        text.AppendLine("NON QUALIFIÉ : valeurs documentaires à relire et à saisir à la main. Aucune n'est prête à charger.");
        text.AppendLine();
        text.AppendLine($"kp = {Text(p.Kp)} {mvUnit}/{pvUnit}");
        text.AppendLine($"ti = {p.Ti.Iec}  ({Text(p.Ti.Seconds)} s)");
        text.AppendLine(p.Td.Seconds > 0 ? $"td = {p.Td.Iec}  ({Text(p.Td.Seconds)} s) · kd = {Text(p.Kd!.Value)}"
                                         : "td = T#0ms (pas d'action dérivée)");
        text.AppendLine($"rev_dir = {(p.RevDir ? "1" : "0")}  " + (p.RevDir
            ? "(un écart positif PV − SP donne une sortie supérieure : gain du procédé négatif)"
            : "(un écart positif PV − SP donne une sortie inférieure : gain du procédé positif)"));
        text.AppendLine($"mix_par = 0 · pv_dev = {(p.PvDev ? "1" : "0")} (dérivée sur la mesure)");
        text.AppendLine();
        text.AppendLine("À mettre à leur valeur neutre : ovs_att = 0, dband = 0, ff_sup = ff_inf, outbias = 0, en_rcpy = 0.");
        text.AppendLine("À déterminer sur l'installation : " + string.Join(", ", ToDetermine) + ".");
        text.AppendLine();
        text.AppendLine("Références (" + Source.Document + ", édition " + Source.Edition + ") :");
        foreach (var equation in Equations) text.AppendLine($"  p. {equation.Page} · {equation.Name} : {equation.Text}");
        text.AppendLine();
        text.AppendLine("Réserves :");
        foreach (var warning in Warnings) text.AppendLine("  · " + Warning(warning));
        return text.ToString();
    }

    private static string Warning(string code) => code switch
    {
        "integral_uses_current_deviation" => "le bloc intègre l'écart du cycle courant ; MKR simule avec un cycle de retard : écart d'ordre dt/ti.",
        "label_inconsistency_rev_dir" => "la documentation libelle rev_dir différemment en deux endroits (comportements concordants) : vérifier le sens sur banc.",
        "sampled_behaviour_unqualified" => "limiteurs, anti-enroulement, suivi et discrétisation ne sont pas reproduits : comportement échantillonné à qualifier sur banc.",
        "time_rounded_to_milliseconds" => "un temps a été arrondi à la milliseconde (résolution du type TIME).",
        _ => code
    };
}

public sealed record BlockConversionResult(bool Converted, string? Reason, BlockConversion? Conversion)
{
    public string RefusalText => Reason switch
    {
        "conversion_parallel_undocumented" => "Structure parallèle : la documentation n'est pas cohérente (p. 176 et p. 183), aucune conversion n'est offerte.",
        "conversion_kd_below_one" => "La constante du filtre de dérivée dépasse td : le bloc remplacerait kd par 1. Choisissez un filtre inférieur ou égal à td.",
        "conversion_filter_required" => "Une action dérivée exige une constante de filtre : le bloc filtre toujours sa dérivée (Tf = td/kd).",
        "conversion_time_resolution" => "Un temps est inférieur à 1 ms, résolution du type TIME.",
        "conversion_parameters" => "Paramètres de réglage invalides (kp non nul, ti > 0, td ≥ 0).",
        "no_verified_adapter" => "Aucune conversion documentée pour ce bloc.",
        _ => "Conversion refusée : " + Reason
    };
}
