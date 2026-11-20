using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class SqliteProjectStore : IProjectStore, IImportStore, IFopdtStore
{
    private readonly string _connectionString;

    public SqliteProjectStore(string path)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version";
        var version = (long)command.ExecuteScalar()!;
        switch (version)
        {
            case 0:
                command.CommandText = """
                    CREATE TABLE projects (
                        id TEXT PRIMARY KEY NOT NULL,
                        name TEXT NOT NULL,
                        created_at TEXT NOT NULL
                    );
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
                goto case 1;
            case 1:
                command.CommandText = """
                    ALTER TABLE projects ADD COLUMN description TEXT NOT NULL DEFAULT '';
                    CREATE TABLE csv_imports (
                        id TEXT PRIMARY KEY NOT NULL,
                        project_id TEXT NOT NULL REFERENCES projects(id),
                        file_name TEXT NOT NULL,
                        sha256 TEXT NOT NULL,
                        imported_at TEXT NOT NULL,
                        original BLOB NOT NULL,
                        metadata TEXT NOT NULL
                    );
                    CREATE INDEX csv_imports_project ON csv_imports(project_id, imported_at, id);
                    PRAGMA user_version = 2;
                    """;
                command.ExecuteNonQuery();
                goto case 2;
            case 2:
                command.CommandText = """
                    CREATE TABLE fopdt_studies (
                        id TEXT PRIMARY KEY NOT NULL,
                        project_id TEXT NOT NULL REFERENCES projects(id),
                        training_id TEXT NOT NULL REFERENCES csv_imports(id),
                        validation_id TEXT NOT NULL REFERENCES csv_imports(id),
                        created_at TEXT NOT NULL,
                        metadata TEXT NOT NULL,
                        CHECK(training_id <> validation_id)
                    );
                    CREATE INDEX fopdt_studies_project ON fopdt_studies(project_id, created_at, id);
                    PRAGMA user_version = 3;
                    """;
                command.ExecuteNonQuery();
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException($"Version SQLite MKR non prise en charge : {version}.");
        }

        transaction.Commit();
    }

    public IReadOnlyList<Project> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_at FROM projects ORDER BY created_at, id";
        using var reader = command.ExecuteReader();
        var projects = new List<Project>();
        while (reader.Read())
            projects.Add(new Project(Guid.Parse(reader.GetString(0)), reader.GetString(1),
                DateTimeOffset.ParseExact(reader.GetString(2), "O", CultureInfo.InvariantCulture)));
        return projects;
    }

    public void Add(Project project)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO projects (id, name, created_at) VALUES ($id, $name, $created)";
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$created", project.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public string ReadDescription(Guid projectId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT description FROM projects WHERE id = $project";
        command.Parameters.AddWithValue("$project", projectId.ToString());
        return (string?)command.ExecuteScalar() ?? throw new InvalidDataException("Projet introuvable.");
    }

    public void SaveDescription(Guid projectId, string description)
    {
        if (description.Length > 4000)
            throw new ArgumentException("La description est limitée à 4 000 caractères.", nameof(description));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET description = $description WHERE id = $project";
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$description", description);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Projet introuvable.");
    }

    public IReadOnlyList<ImportSummary> ListImports(Guid projectId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_name, sha256, imported_at FROM csv_imports
            WHERE project_id = $project ORDER BY imported_at DESC, id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = command.ExecuteReader();
        var summaries = new List<ImportSummary>();
        while (reader.Read())
            summaries.Add(ReadSummary(reader));
        return summaries;
    }

    public CsvImport ReadImport(Guid projectId, Guid importId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_name, sha256, imported_at, original, metadata
            FROM csv_imports WHERE project_id = $project AND id = $id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$id", importId.ToString());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidDataException("Import introuvable dans ce projet.");
        var summary = ReadSummary(reader);
        var original = reader.GetFieldValue<byte[]>(4);
        if (Convert.ToHexString(SHA256.HashData(original)) != summary.Sha256)
            throw new InvalidDataException("L’empreinte du fichier original ne correspond pas.");
        var metadata = JsonSerializer.Deserialize<ImportMetadata>(reader.GetString(5))
            ?? throw new InvalidDataException("Métadonnées CSV indisponibles.");
        return new CsvImport(summary, metadata.Delimiter, metadata.Mapping, metadata.Result, metadata.EngineVersion, original);
    }

    public void SaveImport(Guid projectId, CsvImport import)
    {
        if (import.Result.Quality is null || Convert.ToHexString(SHA256.HashData(import.Original)) != import.Summary.Sha256)
            throw new InvalidDataException("L’import doit contenir l’original vérifié et son rapport.");
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO csv_imports (id, project_id, file_name, sha256, imported_at, original, metadata)
            VALUES ($id, $project, $file, $sha, $date, $original, $metadata)
            """;
        command.Parameters.AddWithValue("$id", import.Summary.Id.ToString());
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$file", import.Summary.FileName);
        command.Parameters.AddWithValue("$sha", import.Summary.Sha256);
        command.Parameters.AddWithValue("$date", import.Summary.ImportedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$original", import.Original);
        command.Parameters.AddWithValue("$metadata",
            JsonSerializer.Serialize(new ImportMetadata(import.Delimiter, import.Mapping, import.Result, import.EngineVersion)));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<FopdtStudy> ListFopdt(Guid projectId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT metadata FROM fopdt_studies
            WHERE project_id = $project ORDER BY created_at DESC, id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = command.ExecuteReader();
        var studies = new List<FopdtStudy>();
        while (reader.Read())
            studies.Add(JsonSerializer.Deserialize<FopdtStudy>(reader.GetString(0))
                ?? throw new InvalidDataException("Étude FOPDT illisible."));
        return studies;
    }

    public void SaveFopdt(Guid projectId, FopdtStudy study)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO fopdt_studies (id, project_id, training_id, validation_id, created_at, metadata)
            SELECT $id, $project, $training, $validation, $created, $metadata
            WHERE EXISTS (SELECT 1 FROM csv_imports WHERE id = $training AND project_id = $project)
              AND EXISTS (SELECT 1 FROM csv_imports WHERE id = $validation AND project_id = $project)
            """;
        command.Parameters.AddWithValue("$id", study.Id.ToString());
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$training", study.TrainingId.ToString());
        command.Parameters.AddWithValue("$validation", study.ValidationId.ToString());
        command.Parameters.AddWithValue("$created", study.CalculatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(study));
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Les deux imports doivent appartenir au projet.");
    }

    private static ImportSummary ReadSummary(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
            DateTimeOffset.ParseExact(reader.GetString(3), "O", CultureInfo.InvariantCulture));

    private sealed record ImportMetadata(string Delimiter, CsvMapping Mapping, CsvResult Result, string EngineVersion);

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
