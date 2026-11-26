using System.Globalization;

namespace Mkr.Core;

/// <summary>
/// Central user-visible texts, French (reference language) and English. A text is looked up by key; a missing English text falls back to the French one,
/// a missing key to the key itself, so a gap never breaks the screen. Official product texts (name, meaning, slogan) are not translated here.
/// Technical formats of exchange and storage never depend on the language.
/// </summary>
public static class Loc
{
    public const string French = "fr", English = "en";

    /// <summary>The active language: MKR_LANGUAGE, else the saved choice, else the Windows display language (English only when it is English).</summary>
    public static string Language { get; private set; } = French;

    private static string? _file;

    public static void Initialize(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "language.txt");
        string? choice = Environment.GetEnvironmentVariable("MKR_LANGUAGE");
        try { if (string.IsNullOrWhiteSpace(choice) && File.Exists(_file)) choice = File.ReadAllText(_file).Trim(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Language = Normalize(choice) ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? English : French);
    }

    /// <summary>Remembers the language for the next start (texts already built keep their language until then).</summary>
    public static void Remember(string language)
    {
        if (Normalize(language) is not { } normal || _file is null) return;
        try { File.WriteAllText(_file, normal); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string? Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch { "fr" or "french" or "français" => French, "en" or "english" => English, _ => null };

    public static string T(string key) => Lookup(Language, key);

    public static string T(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Lookup(Language, key), arguments);

    public static string Lookup(string language, string key) =>
        language == English && En.TryGetValue(key, out var english) ? english
        : Fr.TryGetValue(key, out var french) ? french : key;

    public static IReadOnlyDictionary<string, string> FrenchTexts => Fr;
    public static IReadOnlyDictionary<string, string> EnglishTexts => En;

    private static readonly Dictionary<string, string> Fr = new()
    {
        ["about.title"] = "À propos",
        ["about.version"] = "Version",
        ["about.version.line"] = "Version {0}",
        ["about.version.build"] = "Version {0} · compilation {1}",
        ["about.technical"] = "Nom technique du projet : {0} (dépôt, modules et formats de données inchangés).",
        ["about.qualification.title"] = "Limites de qualification industrielle",
        ["about.thirdparty.title"] = "Composants tiers",
        ["about.thirdparty.text"] = "PDFsharp / MigraDoc (génération des fiches PDF) · Microsoft.Data.Sqlite et SQLitePCLRaw (stockage local) · System.Security.Cryptography.Pkcs · composants Python du moteur de calcul listés dans engine/requirements.txt. Les licences sont celles de chaque composant, fournies avec sa distribution.",
        ["about.legal.title"] = "Éditeur et mentions légales",
        ["about.legal.text"] = "Non renseignés à ce stade : aucune société éditrice, certification ni mention légale n’est déclarée.",
        ["about.close"] = "_Fermer",
        ["product.description"] = ProductIdentity.Description,
        ["product.qualification"] = ProductIdentity.QualificationNotice,
        ["startup.title"] = "Démarrage",
        ["startup.step.database"] = "Ouverture de la base de projets (SQLite)…",
        ["startup.step.engine"] = "Préparation du moteur de calcul…",
        ["startup.step.workspace"] = "Ouverture de l’espace de travail…",
        ["startup.error"] = "Impossible d’ouvrir le stockage ORIAS. {0}",
        ["nav.next"] = "Suivant : {0}",
        ["step.project"] = "Projet", ["step.data"] = "Données", ["step.diagnostic"] = "Diagnostic", ["step.identification"] = "Identification",
        ["step.validation"] = "Validation", ["step.tuning"] = "Réglage PID", ["step.block"] = "Bloc", ["step.pi"] = "Paramètres PI",
        ["step.simulation"] = "Simulation", ["step.report"] = "Rapport",
        ["menu.language"] = "Langue : français",
        ["menu.language.restart"] = "La langue sera appliquée au prochain démarrage d’ORIAS.",
    };

    // English: interface chrome only. Product name, meaning and slogan stay as published; the long French product texts are kept in French until validated.
    private static readonly Dictionary<string, string> En = new()
    {
        ["about.title"] = "About",
        ["about.version"] = "Version",
        ["about.version.line"] = "Version {0}",
        ["about.version.build"] = "Version {0} · build {1}",
        ["about.technical"] = "Technical project name: {0} (repository, modules and data formats unchanged).",
        ["about.qualification.title"] = "Limits of industrial qualification",
        ["about.thirdparty.title"] = "Third-party components",
        ["about.thirdparty.text"] = "PDFsharp / MigraDoc (PDF sheets) · Microsoft.Data.Sqlite and SQLitePCLRaw (local storage) · System.Security.Cryptography.Pkcs · Python components of the calculation engine listed in engine/requirements.txt. Licences are those of each component, supplied with its distribution.",
        ["about.legal.title"] = "Publisher and legal notices",
        ["about.legal.text"] = "Not provided at this stage: no publishing company, certification or legal notice is declared.",
        ["about.close"] = "_Close",
        ["startup.title"] = "Starting",
        ["startup.step.database"] = "Opening the project database (SQLite)…",
        ["startup.step.engine"] = "Preparing the calculation engine…",
        ["startup.step.workspace"] = "Opening the workspace…",
        ["startup.error"] = "ORIAS cannot open its storage. {0}",
        ["nav.next"] = "Next: {0}",
        ["step.project"] = "Project", ["step.data"] = "Data", ["step.diagnostic"] = "Diagnostic", ["step.identification"] = "Identification",
        ["step.validation"] = "Validation", ["step.tuning"] = "PID tuning", ["step.block"] = "Block", ["step.pi"] = "PI parameters",
        ["step.simulation"] = "Simulation", ["step.report"] = "Report",
        ["menu.language"] = "Language: English",
        ["menu.language.restart"] = "The language will apply the next time ORIAS starts.",
    };
}
