namespace Mkr.Core;

/// <summary>
/// The vocabulary shown to people. Abbreviations (SP, PV, MV, CV) stay in data, code and CSV headers, and appear in parentheses next to the
/// plain name where an engineer expects them. Standard names: Consigne (SP), Mesure (PV), Sortie de commande (MV, la commande appliquée au
/// procédé), Commande calculée (CV, avant limitation), Perturbation (d). Compact places may say « Commande » for « Sortie de commande ».
/// </summary>
public static class Terms
{
    public static string Setpoint => Loc.T("terms.setpoint");
    public static string Measurement => Loc.T("terms.measurement");
    public static string Command => Loc.T("terms.command");
    public static string CommandShort => Loc.T("terms.command_short");

    /// <summary>Plain name of a signal role code (SP, PV, MV, CV, d), without the abbreviation.</summary>
    public static string RoleName(string code) => code switch
    {
        "SP" => Setpoint,
        "PV" => Measurement,
        "MV" => Command,
        "CV" => Loc.T("terms.computed_command"),
        "d" => Loc.T("terms.disturbance"),
        _ => code
    };

    /// <summary>Plain name with the engineering abbreviation in parentheses, for selection lists.</summary>
    public static string RoleLabel(string code) => RoleName(code) == code ? code : $"{RoleName(code)} ({code})";
}
