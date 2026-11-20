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
    Require(await engine.GetVersionAsync(default) == "0.9.0", "Python round trip returns the expected version.");

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

    async Task<CsvImport> SaveStepAsync(int day, double amplitude, ModelFamily family = ModelFamily.FOPDT)
    {
        var csv = new StringBuilder("timestamp;mv;pv\n");
        var start = new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 60; i++)
        {
            var t = i - 6;
            var x = Math.Max(0, t - 2.5);
            var response = family switch
            {
                ModelFamily.SOPDT => 1 - (5 * Math.Exp(-x / 5) - 2 * Math.Exp(-x / 2)) / 3,
                ModelFamily.Integrateur => x / 10,
                _ => 1 - Math.Exp(-x / 4)
            };
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
    Require(Math.Abs(fit.Gain + 3) < 1e-5 && fit.TauSeconds is { } firstTau && Math.Abs(firstTau - 4) < 1e-5 &&
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
            DROP TABLE pi_simulations;
            DROP TABLE pi_studies;
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

    // Row-window fit preserves untouched originals and survives SQLite JSON reopening.
    var selectedRange = new StepWindow(0, 55);
    var (windowFit, windowEngineVersion) = await engine.IdentifyAsync(ModelFamily.FOPDT,
        new StepImport(training, columns, selectedRange),
        new StepImport(validation, columns, selectedRange), 0.01, 0.01, default);
    Require(Math.Abs(windowFit.Gain + 3) < 1e-4 && windowFit.Validation.Rmse < 1e-5,
        "Scientific fitting uses selected source rows for each independent trial.");
    var windowStudy = new IdentificationStudy(Guid.NewGuid(), training.Summary.Id,
        validation.Summary.Id, columns, columns, "%", "bar", 0.01, 0.01,
        DateTimeOffset.UtcNow, windowEngineVersion, windowFit, ModelFamily.FOPDT,
        selectedRange, selectedRange);
    restored.SaveStudy(project.Id, windowStudy);
    var reopenedWindow = new SqliteProjectStore(legacyPath).ListStudies(project.Id)
        .Single(item => item.Id == windowStudy.Id);
    Require(reopenedWindow.TrainingWindow == selectedRange &&
            reopenedWindow.ValidationWindow == selectedRange &&
            reopenedWindow.TrainingId == training.Summary.Id &&
            reopenedWindow.ValidationId == validation.Summary.Id,
        "Selected original row indices and independent import references survive SQLite reopening.");

    var (openLoop, openLoopVersion) = await engine.SimulateOpenLoopAsync(persisted.Result,
        new OpenLoopScenario(1, 8, 4, 1, -2, 2, 1), default);
    Require(openLoopVersion == "0.9.0" && openLoop.Points.Length == 9 &&
        openLoop.Points[0].AppliedMv == 0 && openLoop.Points[1].RequestedMv == 4 &&
        openLoop.Points[1].AppliedMv == 1 && openLoop.Points[2].AppliedMv == 2 && openLoop.Points[3].Pv == 0 &&
        Math.Abs(openLoop.Points[4].Pv - (-3 * (1 - Math.Exp(-0.5 / 4)))) < 1e-5 &&
        openLoop.Points[^1].AppliedMv is null && openLoop.SaturationSeconds == 7 && openLoop.RateLimitedSeconds == 1,
        "The open-loop step and fractional delay survive the process boundary without a PI tuning.");

    var trainingSecond = await SaveStepAsync(3, 2, ModelFamily.SOPDT);
    var validationSecond = await SaveStepAsync(4, -1.5, ModelFamily.SOPDT);
    var (second, secondVersion) = await engine.IdentifyAsync(ModelFamily.SOPDT, new StepImport(trainingSecond, columns),
        new StepImport(validationSecond, columns), 0.01, 0.01, default);
    Require(Math.Abs(second.Gain + 3) < 1e-5 && second.TauSeconds is { } slow && Math.Abs(slow - 5) < 1e-5 &&
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

    var trainingThird = await SaveStepAsync(5, 2, ModelFamily.Integrateur);
    var validationThird = await SaveStepAsync(6, -1.5, ModelFamily.Integrateur);
    var (third, thirdVersion) = await engine.IdentifyAsync(ModelFamily.Integrateur, new StepImport(trainingThird, columns),
        new StepImport(validationThird, columns), 0.01, 0.01, default);
    Require(Math.Abs(third.Gain + 0.3) < 1e-7 && Math.Abs(third.DelaySeconds - 2.5) < 1e-7 &&
        third.TauSeconds is null && third.Validation.Rmse < 1e-8,
        "Integrating model has the expected slope and delay through the process boundary.");
    var thirdStudy = study with
    {
        Id = Guid.NewGuid(), TrainingId = trainingThird.Summary.Id, ValidationId = validationThird.Summary.Id,
        Family = ModelFamily.Integrateur, Result = third, EngineVersion = thirdVersion
    };
    restored.SaveStudy(project.Id, thirdStudy);
    var reopenedThird = new SqliteProjectStore(legacyPath).ListStudies(project.Id).Single(item => item.Id == thirdStudy.Id);
    Require(reopenedThird.Family == ModelFamily.Integrateur && reopenedThird.Result.TauSeconds is null,
        "Integrating model persists without a finite time constant.");

    var (pi, _) = await engine.CalculatePiAsync(-2, 100, 3, 2, default);
    Require(pi.Imc == new PiCoefficients(-10, 100, -0.1) && pi.Simc == new PiCoefficients(-10, 20, -0.5) &&
        pi.Warnings.Contains("tight_response"), "PI settings match the signed analytical case.");
    var (linkedPi, piVersion) = await engine.CalculatePiAsync(fit.Gain, fit.TauSeconds!.Value, fit.DelaySeconds, 2, default);
    var piStudy = new PiStudy(Guid.NewGuid(), study.Id, DateTimeOffset.UtcNow, piVersion, linkedPi);
    restored.SavePi(project.Id, piStudy);
    Throws<InvalidDataException>(() => restored.SavePi(Guid.NewGuid(), piStudy with { Id = Guid.NewGuid() }));
    var reopenedPi = new SqliteProjectStore(legacyPath).ListPi(project.Id, study.Id).Single();
    Require(reopenedPi.ModelId == study.Id && reopenedPi.Result.Simc == linkedPi.Simc &&
        restored.ListPi(Guid.NewGuid(), study.Id).Count == 0, "PI settings stay linked to the source model and project.");

    var scenario = new PiScenario(0.1, 5, 1, -0.1, 0.1, 0.2, 3, 0.25);
    var (simulation, simulationVersion) = await engine.SimulatePiAsync(fit, linkedPi, scenario, default);
    Require(simulation.Imc.Points.Length == 51 && simulation.Imc.Points.All(p => p.Mv is null or >= -0.1 and <= 0.1) &&
        simulation.Imc.Points[^1].Mv is null && simulation.Imc.SaturationSeconds > 0 &&
        simulation.Imc.Points[0].Mv == -0.025 && simulation.Imc.RateLimitedSeconds > 0 &&
        simulation.Imc.SetpointResponse is { WindowEndSeconds: 3, OvershootPv: 0, SettlingSeconds: null },
        "The sampled simulation respects actuator bounds and reports saturation.");
    var simulationStudy = new PiSimulationStudy(Guid.NewGuid(), piStudy.Id, DateTimeOffset.UtcNow,
        simulationVersion, scenario, simulation);
    restored.SaveSimulation(project.Id, simulationStudy);
    var reopenedSimulation = new SqliteProjectStore(legacyPath).ListSimulations(project.Id, piStudy.Id).Single();
    Require(reopenedSimulation.Scenario == scenario && reopenedSimulation.Result.Imc.Points.SequenceEqual(simulation.Imc.Points) &&
        reopenedSimulation.Result.Imc.SetpointResponse == simulation.Imc.SetpointResponse &&
        restored.ListSimulations(Guid.NewGuid(), piStudy.Id).Count == 0, "Simulation series and scenario persist with provenance.");

    // The sensitivity matrix keeps fixed PI coefficients and survives SQLite reopening.
    var (sensitivity, robustnessVersion) = await engine.AnalyzeRobustnessAsync(
        fit, linkedPi, scenario, 20, default);
    Require(robustnessVersion == simulationVersion && sensitivity.Cases.Length == 7 &&
        sensitivity.Cases[0].Key == "nominal" &&
        Math.Abs(sensitivity.Cases[0].Imc.Iae - simulation.Imc.Iae) < 1e-10 &&
        Math.Abs(sensitivity.Cases[2].Gain - fit.Gain * 1.2) < 1e-10,
        "Seven FOPDT scenarios preserve the nominal simulation and explicit gain variation.");
    var robustStudy = simulationStudy with
    {
        Id = Guid.NewGuid(), Robustness = sensitivity, CalculatedAt = DateTimeOffset.UtcNow
    };
    restored.SaveSimulation(project.Id, robustStudy);
    var storedRobustness = new SqliteProjectStore(legacyPath).ListSimulations(project.Id, piStudy.Id)
        .Single(item => item.Id == robustStudy.Id);
    Require(storedRobustness.Robustness?.Cases.Length == 7 &&
        storedRobustness.Robustness.Cases[2].Imc.Iae == sensitivity.Cases[2].Imc.Iae &&
        storedRobustness.Scenario == scenario,
        "Sensitivity hypotheses and per-case metrics survive SQLite reopening.");

    // Combined 3×3×3 sensitivity has aligned pointwise envelopes and round-trips via SQLite.
    var (combined, combinedVersion) = await engine.AnalyzeCombinedAsync(
        fit, linkedPi, scenario, 20, default);
    var envelope = combined.Envelope ?? throw new InvalidOperationException("Missing combined PV envelope.");
    Require(combinedVersion == simulationVersion &&
        combined.Cases.Length == 27 &&
        envelope.Seconds.Length == simulation.Imc.Points.Length &&
        Math.Abs(combined.Cases[0].Imc.Iae - simulation.Imc.Iae) < 1e-10 &&
        combined.Cases.Select(x => x.Key).Distinct().Count() == 27 &&
        envelope.Imc.MinPv.Zip(envelope.Imc.MaxPv, (low, high) => low <= high).All(x => x),
        "27-case fixed-PI sensitivity returns finite aligned simulation envelopes.");
    var combinedStudy = simulationStudy with
    {
        Id = Guid.NewGuid(), Robustness = combined, CalculatedAt = DateTimeOffset.UtcNow
    };
    restored.SaveSimulation(project.Id, combinedStudy);
    var reloadedCombined = new SqliteProjectStore(legacyPath).ListSimulations(project.Id, piStudy.Id)
        .Single(x => x.Id == combinedStudy.Id);
    Require(reloadedCombined.Robustness?.Cases.Length == 27 &&
        reloadedCombined.Robustness.Envelope?.Seconds.Length == envelope.Seconds.Length &&
        reloadedCombined.Robustness.Envelope.Imc.MinPv.SequenceEqual(envelope.Imc.MinPv),
        "Combined sensitivity scenario and PV envelopes survive SQLite reopening.");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await engine.GetVersionAsync(cancellation.Token);
        throw new InvalidOperationException("Expected cancellation.");
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Project validation, SQLite migration/persistence, IPC round trip, CSV provenance, FOPDT/SOPDT/integrator holdout and persistence, PI settings, closed-loop simulation, seven-case PI sensitivity persistence and pre-cancellation passed.");
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
