using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class SqliteProjectStore : IProjectStore, IImportStore, IIdentificationStore, IPiStore
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
                goto case 3;
            case 3:
                command.CommandText = """
                    ALTER TABLE fopdt_studies RENAME TO identification_studies;
                    DROP INDEX fopdt_studies_project;
                    CREATE INDEX identification_studies_project ON identification_studies(project_id, created_at, id);
                    PRAGMA user_version = 4;
                    """;
                command.ExecuteNonQuery();
                goto case 4;
            case 4:
                command.CommandText = """
                    CREATE TABLE pi_studies (
                        id TEXT PRIMARY KEY NOT NULL,
                        model_id TEXT NOT NULL REFERENCES identification_studies(id),
                        created_at TEXT NOT NULL,
                        metadata TEXT NOT NULL
                    );
                    CREATE INDEX pi_studies_model ON pi_studies(model_id, created_at, id);
                    PRAGMA user_version = 5;
                    """;
                command.ExecuteNonQuery();
                goto case 5;
            case 5:
                command.CommandText = """
                    CREATE TABLE pi_simulations (
                        id TEXT PRIMARY KEY NOT NULL,
                        pi_id TEXT NOT NULL REFERENCES pi_studies(id),
                        created_at TEXT NOT NULL,
                        metadata TEXT NOT NULL
                    );
                    CREATE INDEX pi_simulations_pi ON pi_simulations(pi_id, created_at, id);
                    PRAGMA user_version = 6;
                    """;
                command.ExecuteNonQuery();
                goto case 6;
            case 6:
                command.CommandText = """
                    CREATE TABLE study_reviews (
                        sequence INTEGER PRIMARY KEY,
                        project_id TEXT NOT NULL REFERENCES projects(id),
                        run_id TEXT NOT NULL, stage TEXT NOT NULL, state TEXT NOT NULL,
                        reviewer TEXT NOT NULL, reason TEXT NOT NULL, evidence TEXT NOT NULL,
                        recorded_at TEXT NOT NULL
                    );
                    CREATE INDEX study_review_history ON study_reviews(project_id, run_id, stage, sequence);
                    PRAGMA user_version = 7;
                    """;
                command.ExecuteNonQuery();
                goto case 7;
            case 7:
                command.CommandText = """
                    ALTER TABLE projects ADD COLUMN loop_tag TEXT NOT NULL DEFAULT '';
                    ALTER TABLE projects ADD COLUMN controlled_quantity TEXT NOT NULL DEFAULT '';
                    ALTER TABLE projects ADD COLUMN operating_point TEXT NOT NULL DEFAULT '';
                    ALTER TABLE projects ADD COLUMN control_objective TEXT NOT NULL DEFAULT '';
                    PRAGMA user_version = 8;
                    """;
                command.ExecuteNonQuery();
                break;
            case 8:
            case 9:
                break;
            default:
                throw new InvalidDataException($"Version SQLite MKR non prise en charge : {version}.");
        }

        transaction.Commit();
        MigrateToVersion9(path);
    }

    /// <summary>
    /// Migration 9 (ADR 0004): two trials of one recording may be estimation and validation of a study,
    /// provided both source ranges are recorded and differ. The table is rebuilt with the official SQLite
    /// procedure (foreign keys off, copy, drop, rename, foreign_key_check) because a CHECK cannot be altered.
    /// Studies and every child table are preserved row for row. Rollback: restore the project database backup;
    /// an older MKR refuses user_version 9 instead of misreading it.
    /// </summary>
    private static void MigrateToVersion9(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version";
        if ((long)command.ExecuteScalar()! != 8)
            return;
        command.CommandText = """
            CREATE TABLE identification_studies_v9 (
                id TEXT PRIMARY KEY NOT NULL,
                project_id TEXT NOT NULL REFERENCES projects(id),
                training_id TEXT NOT NULL REFERENCES csv_imports(id),
                validation_id TEXT NOT NULL REFERENCES csv_imports(id),
                created_at TEXT NOT NULL,
                metadata TEXT NOT NULL,
                CHECK(training_id <> validation_id OR (
                    json_extract(metadata, '$.TrainingWindow') IS NOT NULL
                    AND json_extract(metadata, '$.ValidationWindow') IS NOT NULL
                    AND json_extract(metadata, '$.TrainingWindow') <> json_extract(metadata, '$.ValidationWindow')))
            );
            INSERT INTO identification_studies_v9 (id, project_id, training_id, validation_id, created_at, metadata)
                SELECT id, project_id, training_id, validation_id, created_at, metadata FROM identification_studies;
            DROP TABLE identification_studies;
            ALTER TABLE identification_studies_v9 RENAME TO identification_studies;
            CREATE INDEX identification_studies_project ON identification_studies(project_id, created_at, id);
            """;
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA foreign_key_check";
        using (var violations = command.ExecuteReader())
            if (violations.Read())
                throw new InvalidDataException("La migration SQLite 9 a révélé une référence orpheline : migration annulée.");
        command.CommandText = "PRAGMA user_version = 9";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public ReviewEvent RecordReview(Guid projectId, Guid runId, ReviewStage stage,
        ReviewState? expected, ReviewState next, string reviewer, string reason, string evidence)
    {
        if (runId == Guid.Empty || !Enum.IsDefined(stage))
            throw new ArgumentException("Étude ou étape invalide.");
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$run", runId.ToString());
        command.Parameters.AddWithValue("$stage", stage.ToString());
        command.CommandText = """
            SELECT state FROM study_reviews WHERE project_id=$project AND run_id=$run AND stage=$stage
            ORDER BY sequence DESC LIMIT 1
            """;
        string? current = command.ExecuteScalar() as string;
        if (current != expected?.ToString())
            throw new InvalidOperationException("La revue a changé. Rechargez son historique.");
        StudyReview.Validate(expected, next, reviewer, reason, evidence);
        var now = DateTimeOffset.UtcNow;
        command.CommandText = """
            INSERT INTO study_reviews(project_id,run_id,stage,state,reviewer,reason,evidence,recorded_at)
            VALUES ($project,$run,$stage,$state,$reviewer,$reason,$evidence,$date)
            """;
        command.Parameters.AddWithValue("$state", next.ToString());
        command.Parameters.AddWithValue("$reviewer", reviewer);
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$evidence", evidence);
        command.Parameters.AddWithValue("$date", now.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        transaction.Commit();
        return new(projectId, runId, stage, next, reviewer, reason, evidence, now);
    }

    public IReadOnlyList<ReviewEvent> ReadReviewHistory(Guid projectId, Guid runId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT stage, state, reviewer, reason, evidence, recorded_at
            FROM study_reviews WHERE project_id=$project AND run_id=$run ORDER BY sequence
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$run", runId.ToString());
        using var rows = command.ExecuteReader();
        var history = new List<ReviewEvent>();
        while (rows.Read())
            history.Add(new(projectId, runId, Enum.Parse<ReviewStage>(rows.GetString(0)),
                Enum.Parse<ReviewState>(rows.GetString(1)), rows.GetString(2), rows.GetString(3),
                rows.GetString(4), DateTimeOffset.ParseExact(rows.GetString(5), "O", CultureInfo.InvariantCulture)));
        return history;
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

    public LoopBrief ReadLoopBrief(Guid projectId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT loop_tag, controlled_quantity, operating_point, control_objective, description FROM projects WHERE id = $project";
        command.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("Projet introuvable.");
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
    }

    public void SaveLoopBrief(Guid projectId, LoopBrief brief)
    {
        if (brief.Description.Length > 4000)
            throw new ArgumentException("La description est limitée à 4 000 caractères.", nameof(brief));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE projects SET loop_tag = $tag, controlled_quantity = $quantity, operating_point = $point,
                control_objective = $objective, description = $description WHERE id = $project
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$tag", brief.Tag);
        command.Parameters.AddWithValue("$quantity", brief.ControlledQuantity);
        command.Parameters.AddWithValue("$point", brief.OperatingPoint);
        command.Parameters.AddWithValue("$objective", brief.Objective);
        command.Parameters.AddWithValue("$description", brief.Description);
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Projet introuvable.");
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
        return new CsvImport(summary, metadata.Delimiter, metadata.Mapping, metadata.Result, metadata.EngineVersion, original, metadata.Acquisition);
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
            JsonSerializer.Serialize(new ImportMetadata(import.Delimiter, import.Mapping, import.Result, import.EngineVersion, import.Acquisition)));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<IdentificationStudy> ListStudies(Guid projectId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT metadata FROM identification_studies
            WHERE project_id = $project ORDER BY created_at DESC, id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        using var reader = command.ExecuteReader();
        var studies = new List<IdentificationStudy>();
        while (reader.Read())
            studies.Add(JsonSerializer.Deserialize<IdentificationStudy>(reader.GetString(0))
                ?? throw new InvalidDataException("Étude d’identification illisible."));
        return studies;
    }

    public void SaveStudy(Guid projectId, IdentificationStudy study)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO identification_studies (id, project_id, training_id, validation_id, created_at, metadata)
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

    public IReadOnlyList<PiStudy> ListPi(Guid projectId, Guid modelId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pi.metadata FROM pi_studies AS pi
            JOIN identification_studies AS fit ON fit.id = pi.model_id
            WHERE fit.project_id = $project AND fit.id = $model
            ORDER BY pi.created_at DESC, pi.id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$model", modelId.ToString());
        using var reader = command.ExecuteReader();
        var studies = new List<PiStudy>();
        while (reader.Read())
            studies.Add(JsonSerializer.Deserialize<PiStudy>(reader.GetString(0))
                ?? throw new InvalidDataException("Étude PI illisible."));
        return studies;
    }

    public void SavePi(Guid projectId, PiStudy study)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pi_studies (id, model_id, created_at, metadata)
            SELECT $id, $model, $created, $metadata
            WHERE EXISTS (SELECT 1 FROM identification_studies WHERE id = $model AND project_id = $project)
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$id", study.Id.ToString());
        command.Parameters.AddWithValue("$model", study.ModelId.ToString());
        command.Parameters.AddWithValue("$created", study.CalculatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(study));
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Le modèle doit appartenir au projet.");
    }

    public IReadOnlyList<PiSimulationStudy> ListSimulations(Guid projectId, Guid piId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sim.metadata FROM pi_simulations AS sim
            JOIN pi_studies AS pi ON pi.id = sim.pi_id
            JOIN identification_studies AS fit ON fit.id = pi.model_id
            WHERE fit.project_id = $project AND pi.id = $pi
            ORDER BY sim.created_at DESC, sim.id
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$pi", piId.ToString());
        using var reader = command.ExecuteReader();
        var studies = new List<PiSimulationStudy>();
        while (reader.Read())
            studies.Add(JsonSerializer.Deserialize<PiSimulationStudy>(reader.GetString(0))
                ?? throw new InvalidDataException("Simulation PI illisible."));
        return studies;
    }

    public void SaveSimulation(Guid projectId, PiSimulationStudy study)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pi_simulations (id, pi_id, created_at, metadata)
            SELECT $id, $pi, $created, $metadata
            WHERE EXISTS (
                SELECT 1 FROM pi_studies AS pi
                JOIN identification_studies AS fit ON fit.id = pi.model_id
                WHERE pi.id = $pi AND fit.project_id = $project
            )
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$id", study.Id.ToString());
        command.Parameters.AddWithValue("$pi", study.PiId.ToString());
        command.Parameters.AddWithValue("$created", study.CalculatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(study));
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Le calcul PI doit appartenir au projet.");
    }

    private static ImportSummary ReadSummary(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
            DateTimeOffset.ParseExact(reader.GetString(3), "O", CultureInfo.InvariantCulture));

    private sealed record ImportMetadata(string Delimiter, CsvMapping Mapping, CsvResult Result, string EngineVersion, AcquisitionOrigin? Acquisition = null);

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
