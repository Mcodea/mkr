using System.Reflection;

namespace Mkr.Core;

/// <summary>
/// Single source of the product identity shown to users. ORIAS is the commercial name; MKR stays the technical name
/// (repository, namespaces, assemblies, storage paths, data formats, update identifiers). Only user-visible texts read from here.
/// </summary>
public static class ProductIdentity
{
    public const string CommercialName = "ORIAS";
    public const string TechnicalName = "MKR";
    public const string Meaning = "Optimisation de la Régulation Industrielle et Assistance aux Systèmes asservis";
    public const string Tagline = "De la mesure à la maîtrise du procédé.";
    public const string Description =
        "Logiciel d’assistance à l’ingénierie de la régulation et de l’asservissement industriel, permettant l’acquisition et l’analyse des données, " +
        "l’identification des procédés, le réglage, l’optimisation et la validation des boucles de régulation.";
    public const string QualificationNotice =
        "Les résultats sont théoriques et issus de modèles identifiés : ils doivent être essayés sur banc ou en simulation avant toute mise en service. " +
        "ORIAS lit les automates en lecture seule, n’y écrit jamais et n’est pas déclaré qualifié industriellement.";

    /// <summary>« ORIAS — Section » : the title of every window.</summary>
    public static string Title(string section) => $"{CommercialName} — {section}";

    /// <summary>Version number (major.minor.build) and short build identifier of the assembly that hosts the product, read from its metadata.</summary>
    public static (string Number, string? Build) VersionOf(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = assembly.GetName().Version;
        var number = version is null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
        string? build = null;
        if (informational is not null && informational.IndexOf('+') is var plus and >= 0 && informational.Length > plus + 1)
            build = informational[(plus + 1)..][..Math.Min(8, informational.Length - plus - 1)];
        return (number, build);
    }
}
