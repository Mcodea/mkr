using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Mkr.Core;
using Mkr.Infrastructure;

var directory = Directory.CreateTempSubdirectory("mkr-test-");
try
{
    var path = Path.Combine(directory.FullName, "test.db");
    var store = new SqliteProjectStore(path);
    Require(store.List().Count == 0, "The migrated database is empty.");
    var project = Project.Create("  Banc d’essai  ");
    Require(project.Name == "Banc d’essai", "Project name is trimmed.");
    Throws<ArgumentException>(() => Project.Create(" "));
    Throws<ArgumentException>(() => Project.Create(new string('x', 121)));

    store.Add(project);
    var reopened = new SqliteProjectStore(path);
    Require(reopened.List().Single() == project, "Project survives reopening and migration replay.");
    Throws<SqliteException>(() => reopened.Add(project));
    Require(reopened.List().Single() == project, "Duplicate insert preserves the original project.");

    using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path, Pooling = false
    }.ToString()))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version = 99";
        command.ExecuteNonQuery();
    }
    Throws<InvalidDataException>(() => new SqliteProjectStore(path));

    var interpreter = Environment.GetEnvironmentVariable("MKR_PYTHON") ?? "python";
    var engine = new PythonDiagnostics(interpreter, Path.Combine(AppContext.BaseDirectory, "engine", "mkr_engine.py"));
    Require(await engine.GetVersionAsync(default) == "0.4.0", "Python round trip returns the expected version.");

    var legacyPath = Path.Combine(directory.FullName, "legacy.db");
    using (var legacy = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = legacyPath, Pooling = false
    }.ToString()))
    {
        legacy.Open();
        using var command = legacy.CreateCommand();
        command.CommandText = """
            CREATE TABLE projects (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL, created_at TEXT NOT NULL);
            INSERT INTO projects VALUES ($id, 'Projet initial', '2025-01-01T00:00:00.0000000+00:00');
            PRAGMA user_version = 1;
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.ExecuteNonQuery();
    }
    var migrated = new SqliteProjectStore(legacyPath);
    Require(migrated.List().Single().Id == project.Id && migrated.ReadDescription(project.Id) == "",
        "Migration from schema v1 preserves the original project.");
    migrated.SaveDescription(project.Id, "Procédé d’essai");
    var raw = Encoding.UTF8.GetBytes("t;mesure\r\n2025-01-01T00:00:00Z;2\r\n2025-01-01T00:00:01Z;4\r\n");
    var preview = await engine.InspectAsync(raw, ";", default);
    Require(preview.RowCount == 2 && preview.Quality is null, "CSV inspection returns unmodified fields.");
    var mapping = new CsvMapping("t", "mesure", "PV", "bar");
    var (result, version) = await engine.AnalyzeAsync(raw, ";", mapping, default);
    Require(result.Quality is { ValidValues: 2, Minimum: 2, Maximum: 4, MinPositiveStepSeconds: 1 },
        "CSV report matches the known input.");
    var summary = new ImportSummary(Guid.NewGuid(), "essai.csv", Convert.ToHexString(SHA256.HashData(raw)), DateTimeOffset.UtcNow);
    var import = new CsvImport(summary, ";", mapping, result, version, raw);
    migrated.SaveImport(project.Id, import);
    Throws<SqliteException>(() => migrated.SaveImport(Guid.NewGuid(), import with
    {
        Summary = summary with { Id = Guid.NewGuid() }
    }));
    var restored = new SqliteProjectStore(legacyPath);
    var saved = restored.ReadImport(project.Id, summary.Id);
    Require(restored.ReadDescription(project.Id) == "Procédé d’essai" && restored.ListImports(project.Id).Single() == summary &&
        saved.Original.SequenceEqual(raw) && saved.Mapping == mapping && saved.Result.Quality == result.Quality,
        "Description, original, mapping and report survive reopening.");
    Throws<InvalidDataException>(() => restored.ReadImport(Guid.NewGuid(), summary.Id));

    async Task<CsvImport> SaveStepAsync(int day, double amplitude, bool secondOrder = false)
    {
        var csv = new StringBuilder("timestamp;mv;pv\n");
        var start = new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 60; i++)
        {
            var t = i - 6;
            var x = Math.Max(0, t - 2.5);
            var response = secondOrder
                ? 1 - (5 * Math.Exp(-x / 5) - 2 * Math.Exp(-x / 2)) / 3
                : 1 - Math.Exp(-x / 4);
            var y = 8 - 3 * amplitude * response;
            csv.Append(FormattableString.Invariant($"{start.AddSeconds(i):O};{(t >= 0 ? amplitude : 0)};{y:G17}\n"));
        }

        // The CSV parser accepts up to six fractional digits; use an exact seconds representation.
        var stepRaw = Encoding.UTF8.GetBytes(csv.ToString().Replace(".0000000+00:00", "Z", StringComparison.Ordinal));
        var stepMapping = new CsvMapping("timestamp", "pv", "PV", "bar");
        var (stepResult, stepVersion) = await engine.AnalyzeAsync(stepRaw, ";", stepMapping, default);
        var stepSummary = new ImportSummary(Guid.NewGuid(), $"essai-{day}.csv", Convert.ToHexString(SHA256.HashData(stepRaw)), DateTimeOffset.UtcNow);
        var step = new CsvImport(stepSummary, ";", stepMapping, stepResult, stepVersion, stepRaw);
        restored.SaveImport(project.Id, step);
        return step;
    }

    var training = await SaveStepAsync(1, 2);
    var validation = await SaveStepAsync(2, -1.5);
    var columns = new StepColumns("timestamp", "mv", "pv");
    var (fit, fitVersion) = await engine.IdentifyAsync(ModelFamily.FOPDT, new StepImport(training, columns),
        new StepImport(validation, columns), 0.01, 0.01, default);
    Require(Math.Abs(fit.Gain + 3) < 1e-5 && Math.Abs(fit.TauSeconds - 4) < 1e-5 &&
        Math.Abs(fit.DelaySeconds - 2.5) < 1e-5 && fit.Validation.Rmse < 1e-6,
        "Analytic FOPDT parameters and holdout prediction agree through the process boundary.");
    var study = new IdentificationStudy(Guid.NewGuid(), training.Summary.Id, validation.Summary.Id, columns, columns,
        "%", "bar", 0.01, 0.01, DateTimeOffset.UtcNow, fitVersion, fit);
    restored.SaveStudy(project.Id, study);
    Throws<InvalidDataException>(() => restored.SaveStudy(Guid.NewGuid(), study with { Id = Guid.NewGuid() }));
    using (var v3 = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = legacyPath, Pooling = false
    }.ToString()))
    {
        v3.Open();
        using var command = v3.CreateCommand();
        command.CommandText = """
            DROP INDEX identification_studies_project;
            ALTER TABLE identification_studies RENAME TO fopdt_studies;
            CREATE INDEX fopdt_studies_project ON fopdt_studies(project_id, created_at, id);
            UPDATE fopdt_studies SET metadata = json_remove(metadata, '$.Family', '$.Result.SecondTauSeconds');
            PRAGMA user_version = 3;
            """;
        command.ExecuteNonQuery();
    }
    var persisted = new SqliteProjectStore(legacyPath).ListStudies(project.Id).Single();
    Require(persisted.Id == study.Id && persisted.Family == ModelFamily.FOPDT && persisted.ValidationId == validation.Summary.Id &&
        persisted.Result.Validation.Points.SequenceEqual(fit.Validation.Points), "Study provenance survives reopening.");

    var trainingSecond = await SaveStepAsync(3, 2, secondOrder: true);
    var validationSecond = await SaveStepAsync(4, -1.5, secondOrder: true);
    var (second, secondVersion) = await engine.IdentifyAsync(ModelFamily.SOPDT, new StepImport(trainingSecond, columns),
        new StepImport(validationSecond, columns), 0.01, 0.01, default);
    Require(Math.Abs(second.Gain + 3) < 1e-5 && Math.Abs(second.TauSeconds - 5) < 1e-5 &&
        second.SecondTauSeconds is { } fast && Math.Abs(fast - 2) < 1e-5 && second.Validation.Rmse < 1e-6,
        "SOPDT poles and prediction agree through the process boundary.");
    var secondStudy = study with
    {
        Id = Guid.NewGuid(), TrainingId = trainingSecond.Summary.Id, ValidationId = validationSecond.Summary.Id,
        Family = ModelFamily.SOPDT, Result = second, EngineVersion = secondVersion
    };
    restored.SaveStudy(project.Id, secondStudy);
    var restoredSecond = new SqliteProjectStore(legacyPath).ListStudies(project.Id).Single(item => item.Id == secondStudy.Id);
    Require(restoredSecond.Family == ModelFamily.SOPDT && restoredSecond.Result.SecondTauSeconds == second.SecondTauSeconds,
        "The SOPDT family and both time constants survive reopening.");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await engine.GetVersionAsync(cancellation.Token);
        throw new InvalidOperationException("Expected cancellation.");
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Project validation, SQLite migration/persistence, IPC round trip, CSV provenance, FOPDT/SOPDT holdout and persistence, and pre-cancellation passed.");
}
finally
{
    directory.Delete(recursive: true);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void Throws<T>(Action operation) where T : Exception
{
    try { operation(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
