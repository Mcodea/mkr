namespace Mkr.Core;

/// <summary>
/// The vocabulary shown to people. Abbreviations (SP, PV, MV, CV) stay in data, code and CSV headers, and appear in parentheses next to the
/// plain name where an engineer expects them. Standard names: Consigne (SP), Mesure (PV), Sortie de commande (MV, la commande appliquée au
/// procédé), Commande calculée (CV, avant limitation), Perturbation (d). Compact places may say « Commande » for « Sortie de commande ».
/// </summary>
public static class Terms
{
    public const string Setpoint = "Consigne";
    public const string Measurement = "Mesure";
    public const string Command = "Sortie de commande";
    public const string CommandShort = "Commande";

    /// <summary>Plain name of a signal role code (SP, PV, MV, CV, d), without the abbreviation.</summary>
    public static string RoleName(string code) => code switch
    {
        "SP" => Setpoint,
        "PV" => Measurement,
        "MV" => Command,
        "CV" => "Commande calculée",
        "d" => "Perturbation",
        _ => code
    };

    /// <summary>Plain name with the engineering abbreviation in parentheses, for selection lists.</summary>
    public static string RoleLabel(string code) => RoleName(code) == code ? code : $"{RoleName(code)} ({code})";
}
