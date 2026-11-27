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
    var reviewRun = Guid.NewGuid();
    store.RecordReview(project.Id, reviewRun, ReviewStage.Model, null, ReviewState.Calculated,
        "Relecteur", "Identification terminée", "essai-1");
    var reopened = new SqliteProjectStore(path);
    Throws<InvalidOperationException>(() => reopened.RecordReview(project.Id, reviewRun, ReviewStage.Model,
        ReviewState.Calculated, ReviewState.Approved, "Relecteur", "Saut de vérification", "essai-1"));
    reopened.RecordReview(project.Id, reviewRun, ReviewStage.Model, ReviewState.Calculated,
        ReviewState.Verified, "Relecteur", "Comparaison indépendante", "essai-2");
    Throws<InvalidOperationException>(() => store.RecordReview(project.Id, reviewRun, ReviewStage.Model,
        ReviewState.Calculated, ReviewState.Rejected, "Relecteur", "Vue obsolète", "essai-1"));
    var history = reopened.ReadReviewHistory(project.Id, reviewRun);
    Require(history.Count == 2 && history[1].State == ReviewState.Verified &&
        history[1].Evidence == "essai-2", "Only committed review events survive reopening.");
    Require(reopened.ReadReviewHistory(project.Id, Guid.NewGuid()).Count == 0,
        "Review events are isolated by run.");
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
    Require(await engine.GetVersionAsync(default) == "0.10.0", "Python round trip returns the expected version.");
    await engine.VerifyContractAsync(default);                         // the real engine speaks the contract revision the application requires (LOT 1)
    {
        // An engine that predates the contract description is refused with an explicit message, not later in a screen.
        var oldEngine = Path.Combine(Path.GetTempPath(), "mkr-old-engine-" + Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(oldEngine, "import sys, json\nr = json.loads(sys.stdin.read())\nprint(json.dumps({'version': 1, 'requestId': r['requestId'], 'engineVersion': '0.2.0', 'error': {'code': 'unsupported_operation'}}))\n");
        try
        {
            await new PythonDiagnostics(interpreter, oldEngine).VerifyContractAsync(default);
            throw new InvalidOperationException("An engine without contract description must be refused.");
        }
        catch (InvalidDataException contractRefusal)
        {
            Require(contractRefusal.Message.Contains("trop ancien"), "An engine without contract description is refused with an explicit message.");
        }
        finally { File.Delete(oldEngine); }
    }

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
    {
        // LOT 1: the old database is copied BEFORE the migration (online backup), and the copy is the original schema with the original rows.
        var backups = directory.GetFiles("legacy.db.backup-v1-*");
        Require(backups.Length == 1, "A project database older than the current schema is backed up before its migration.");
        using var backupConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backups[0].FullName, Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString());
        backupConnection.Open();
        using var backupCommand = backupConnection.CreateCommand();
        backupCommand.CommandText = "PRAGMA user_version";
        Require((long)backupCommand.ExecuteScalar()! == 1, "The backup keeps the original schema version.");
        backupCommand.CommandText = "SELECT name FROM projects";
        Require((string)backupCommand.ExecuteScalar()! == "Projet initial", "The backup keeps the original rows.");
    }
    migrated.SaveDescription(project.Id, "Procédé d’essai");
    var brief = migrated.ReadLoopBrief(project.Id);
    Require(brief == new LoopBrief("", "", "", "", "Procédé d’essai"), "Legacy description remains available.");
    brief = brief with { Tag = "PIC-101", ControlledQuantity = "Pression", OperatingPoint = "4 bar", Objective = "Tenir la pression aval" };
    migrated.SaveLoopBrief(project.Id, brief);
    Require(new SqliteProjectStore(legacyPath).ReadLoopBrief(project.Id) == brief, "Loop definition survives reopening.");
    Require(directory.GetFiles("legacy.db.backup-*").Length == 1, "Reopening a database already at the current schema makes no further backup.");
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

    async Task<CsvImport> SaveStepAsync(int day, double amplitude, ModelFamily family = ModelFamily.FOPDT, double delaySeconds = 2.5, int secondsPerSample = 1, int samples = 60, double tau = 4, double noise = 0)
    {
        var seed = (uint)(day * 2654435761u + 12345);
        double Gauss()
        {
            // Deterministic pseudo-noise (sum of twelve uniforms, mean 0, standard deviation 1): reproducible demonstration data, never used by a check.
            var sum = 0.0;
            for (var k = 0; k < 12; k++) { seed = seed * 1664525u + 1013904223u; sum += seed / 4294967296.0; }
            return sum - 6;
        }
        var csv = new StringBuilder("timestamp;mv;pv\n");
        var start = new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < samples; i++)
        {
            var t = i - 6;
            var x = Math.Max(0, t - delaySeconds / secondsPerSample);
            var response = family switch
            {
                ModelFamily.SOPDT => 1 - (5 * Math.Exp(-x / 5) - 2 * Math.Exp(-x / 2)) / 3,
                ModelFamily.Integrateur => x / 10,
                _ => 1 - Math.Exp(-x / tau)
            };
            var y = 8 - 3 * amplitude * response + (noise > 0 ? noise * Gauss() : 0);
            csv.Append(FormattableString.Invariant($"{start.AddSeconds(i * secondsPerSample):O};{(t >= 0 ? amplitude : 0)};{y:G17}\n"));
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
    var guided = await engine.RecommendTuningAsync(ModelFamily.FOPDT, fit, true, DesignObjectives.Balanced, null, default);     // the fixture fit is exact (RMSE < 1e-6) and accepted by the independent holdout
    await ThrowsAsync<CsvInputException>(() => engine.RecommendTuningAsync(ModelFamily.FOPDT, fit, false, DesignObjectives.Balanced, null, default));     // the engine itself refuses a model without proof of validation (LOT 0b, P0-4)
    Require(guided.IsRecommended && guided.Recommendation is { Controller: { Structure: "PI", Kp: < 0, TiSeconds: > 0 } } &&
            guided.Recommendation.WorstPeakSensitivity <= 1.7 + 1e-9 && guided.Qualification == "model_based_unqualified",
        "Guided tuning through the process boundary: a signed, bounded, unqualified PI for the balanced objective.");
    var comparison = await engine.CompareAsync(new(training, columns), new(validation, columns),
        0.01, 0.01, 1, 1, default);
    Require(Math.Abs(comparison.Candidates["FOPDT"].ValidationRmse - fit.Validation.Rmse) < 1e-9 &&
        comparison.Candidates.ContainsKey("SOPDT"), "Model comparison uses the selected independent trial.");
    var suggestion = await engine.SuggestCriteriaAsync(new(training, columns), new(validation, columns), 0.01, 0.01, default);
    Require(suggestion is { BaselineNoise: 0, Basis: "pv_tolerance", MaximumRmse: 0.005 } &&
            Math.Abs(suggestion.MaximumResidualLagOne - Math.Min(1.0, 2.576 / Math.Sqrt(fit.Validation.Samples))) < 1e-9,
        "Noise-free plateaus propose half the declared PV tolerance, and the lag bound is Ljung's 99 % whiteness bound for the validation samples.");
    Require(comparison.Decision == "lowest_acceptable_order" && comparison.HoldoutTest is null,
        "Exact first-order trials keep the lowest order without a holdout test.");
    var study = new IdentificationStudy(Guid.NewGuid(), training.Summary.Id, validation.Summary.Id, columns, columns,
        "%", "bar", 0.01, 0.01, DateTimeOffset.UtcNow, fitVersion, fit, Comparison: comparison);
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
            ALTER TABLE projects DROP COLUMN loop_tag;
            ALTER TABLE projects DROP COLUMN controlled_quantity;
            ALTER TABLE projects DROP COLUMN operating_point;
            DROP TABLE arx_studies;
            DROP TABLE audit_events;
            ALTER TABLE projects DROP COLUMN control_objective;
            DROP TABLE study_reviews;
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

    Require(persisted.Comparison is { } storedComparison &&
        storedComparison.Criteria == comparison.Criteria && storedComparison.Selected == comparison.Selected &&
        storedComparison.AlgorithmVersion == comparison.AlgorithmVersion &&
        storedComparison.Candidates["SOPDT"].ValidationRmse == comparison.Candidates["SOPDT"].ValidationRmse &&
        storedComparison.Candidates["SOPDT"].RejectionReasons.SequenceEqual(comparison.Candidates["SOPDT"].RejectionReasons),
        "Model choice and holdout criteria survive SQLite reopening.");
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

    // Broïda: a distinct 28%/40% estimator, with explicit crossings and holdout.
    var (broida, broidaVersion) = await engine.IdentifyAsync(ModelFamily.BroidaFOPDT,
        new StepImport(training, columns), new StepImport(validation, columns),
        0.01, 0.01, default);
    var evidence = broida.BroidaEvidence ??
        throw new InvalidOperationException("Broïda crossing evidence missing.");
    Require(broidaVersion == fitVersion &&
        broida.StandardErrors is null &&
        Math.Abs(broida.Gain + 3) < 0.05 &&
        broida.TauSeconds is { } broidaTau && Math.Abs(broidaTau - 4) < 0.45 &&
        Math.Abs(broida.DelaySeconds - 2.5) < 0.4 &&
        evidence.Crossing28Seconds > 0 &&
        evidence.Crossing40Seconds > evidence.Crossing28Seconds &&
        evidence.TerminalSamples == 6,
        "Broïda 28/40 FOPDT identification returns physical parameters and recorded evidence.");
    var broidaStudy = new IdentificationStudy(Guid.NewGuid(), training.Summary.Id,
        validation.Summary.Id, columns, columns, "%", "bar", 0.01, 0.01,
        DateTimeOffset.UtcNow, broidaVersion, broida, ModelFamily.BroidaFOPDT);
    restored.SaveStudy(project.Id, broidaStudy);
    var reopenedBroida = new SqliteProjectStore(legacyPath).ListStudies(project.Id)
        .Single(item => item.Id == broidaStudy.Id);
    Require(reopenedBroida.Family == ModelFamily.BroidaFOPDT &&
        reopenedBroida.TrainingId == training.Summary.Id &&
        reopenedBroida.ValidationId == validation.Summary.Id &&
        reopenedBroida.Result.BroidaEvidence is { } restoredEvidence &&
        restoredEvidence.Crossing28Seconds == evidence.Crossing28Seconds &&
        restoredEvidence.Crossing40Seconds == evidence.Crossing40Seconds &&
        restoredEvidence.TerminalPv == evidence.TerminalPv &&
        restoredEvidence.ThresholdFractions.SequenceEqual(evidence.ThresholdFractions),
        "Broïda crossing provenance and independent source references survive SQLite reopening.");

    var (openLoop, openLoopVersion) = await engine.SimulateOpenLoopAsync(persisted.Result,
        new OpenLoopScenario(1, 8, 4, 1, -2, 2, 1), default);
    Require(openLoopVersion == "0.10.0" && openLoop.Points.Length == 9 &&
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

    var theoreticalPid = await engine.CalculateSopdtPidAsync(second, 2, default);
    Require(theoreticalPid.Series.Kc < 0 && theoreticalPid.Ideal.Kp < 0 &&
        theoreticalPid.Series.TdSeconds == second.SecondTauSeconds &&
        Math.Abs(theoreticalPid.Ideal.TiSeconds - theoreticalPid.Series.TiSeconds - theoreticalPid.Series.TdSeconds) < 1e-10,
        "SOPDT PID retains the fitted fast pole and the series-to-ideal integral time.");
    var (pi, _) = await engine.CalculatePiAsync(-2, 100, 3, 2, default);
    Require((pi.Imc with { Margins = null }) == new PiCoefficients(-10, 100, -0.1) &&
        (pi.Simc with { Margins = null }) == new PiCoefficients(-10, 20, -0.5) &&
        pi.Warnings.Contains("tight_response"), "PI settings match the signed analytical case.");
    // Published IMC-PI (Rivera, Morari and Skogestad 1986): Kc = (tau + theta/2) / (K (lambda + theta/2)), Ti = tau + theta/2, here (101.5) / (3.5 * -2).
    Require(pi.ImcPublished is { } published && Math.Abs(published.Kp - (101.5 / (3.5 * -2))) < 1e-9 && Math.Abs(published.TiSeconds - 101.5) < 1e-9 &&
        Math.Abs(published.ParallelKi - published.Kp / 101.5) < 1e-12, "The published IMC-PI travels through the process boundary with the signed analytical values.");
    // NC-17: the margins of the nominal loop travel with each tuning and with the SOPDT PID.
    Require(pi.Simc.Margins is { PhaseMarginDegrees: > 0, GainMargin: > 1, PeakSensitivity: >= 1 } simcMargins &&
        pi.Imc.Margins is { PhaseMarginDegrees: > 0 } && simcMargins.Describe().Contains("marge de phase"),
        "Nominal margins are reported for both PI tunings.");
    Require(theoreticalPid.Margins is { PhaseMarginDegrees: > 0, GainMargin: > 1 }, "Nominal margins are reported for the SOPDT PID.");
    var (aggressive, _) = await engine.CalculatePiAsync(1, 5, 2, 0.2, default);
    // Phase margin 37.9° passes the 30° guidance, but the gain margin 1.73 and Ms 2.68 do not.
    Require(aggressive.Simc.Margins!.Warnings.SequenceEqual(new[] { "low_gain_margin", "high_sensitivity" }) &&
        aggressive.Simc.Margins.Describe().Contains("sensibilité élevée"), "A tight target on a long delay is flagged, not hidden.");
    var (linkedPi, piVersion) = await engine.CalculatePiAsync(fit.Gain, fit.TauSeconds!.Value, fit.DelaySeconds, 2, default);
    var piStudy = new PiStudy(Guid.NewGuid(), study.Id, DateTimeOffset.UtcNow, piVersion, linkedPi);
    restored.SavePi(project.Id, piStudy);
    Throws<InvalidDataException>(() => restored.SavePi(Guid.NewGuid(), piStudy with { Id = Guid.NewGuid() }));
    var reopenedPi = new SqliteProjectStore(legacyPath).ListPi(project.Id, study.Id).Single();
    Require(reopenedPi.ModelId == study.Id && reopenedPi.Result.Simc == linkedPi.Simc &&
        restored.ListPi(Guid.NewGuid(), study.Id).Count == 0, "PI settings stay linked to the source model and project.");

    var scenario = new PiScenario(0.1, 5, 1, -0.1, 0.1, 0.2, 3, 0.25);
    var transfer = await engine.SimulateTransferAsync(fit, linkedPi.Simc,
        scenario with { Disturbance = 0 }, 2, 1, default);
    Require(transfer.Points[9].Mode == "manual" && transfer.Points[10].Mode == "automatic" &&
        transfer.Points[9].Mv == scenario.MaximumMv && transfer.Points[10].Mv == transfer.Points[9].Mv &&
        transfer.TransferBumpMv == 0, "PI manual-to-automatic transfer uses the actual limited output.");
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

    var csvPath = Path.Combine(directory.FullName, "series.csv");
    SimulationCsv.Export(csvPath, reopenedSimulation);
    var savedBytes = File.ReadAllBytes(csvPath);
    var shiftedPoints = simulation.Simc.Points.ToArray();
    shiftedPoints[^1] = shiftedPoints[^1] with { Seconds = shiftedPoints[^1].Seconds + 1 };
    foreach (var invalidPoints in new[] { shiftedPoints, shiftedPoints[..^1] })
    {
        var invalidStudy = simulationStudy with
        {
            Result = simulation with { Simc = simulation.Simc with { Points = invalidPoints } }
        };
        Throws<InvalidDataException>(() => SimulationCsv.Export(csvPath, invalidStudy));
        Require(File.ReadAllBytes(csvPath).SequenceEqual(savedBytes), "Failed series export preserves the existing CSV.");
    }
    using (File.Open(csvPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        try
        {
            SimulationCsv.Export(csvPath, reopenedSimulation);
            throw new InvalidOperationException("A locked Windows destination must reject replacement.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    Require(!Directory.EnumerateFiles(directory.FullName, ".*.tmp").Any() &&
        File.ReadAllBytes(csvPath).SequenceEqual(savedBytes), "Locked destination preserves the original and releases staging files.");
    SimulationCsv.Export(csvPath, reopenedSimulation);
    Require(File.ReadAllBytes(csvPath).SequenceEqual(savedBytes), "Export succeeds on retry after unlocking.");
    var sheetPath = Path.Combine(directory.FullName, "study.pdf");
    StudySheet.Export(sheetPath, project.Name, study, reopenedPi, reopenedSimulation,
        training.Summary, validation.Summary, Array.Empty<ReviewEvent>());
    using (var sheet = PdfSharp.Pdf.IO.PdfReader.Open(sheetPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
        Require(sheet.PageCount >= 2, "A reopened saved simulation produces a readable multipage study PDF.");
    var originalSheet = File.ReadAllBytes(sheetPath);
    var duplicatedImc = simulation.Imc.Points.ToArray();
    var duplicatedSimc = simulation.Simc.Points.ToArray();
    duplicatedImc[^1] = duplicatedImc[^1] with { Seconds = duplicatedImc[^2].Seconds };
    duplicatedSimc[^1] = duplicatedSimc[^1] with { Seconds = duplicatedSimc[^2].Seconds };
    var duplicateStudy = simulationStudy with
    {
        Result = simulation with
        {
            Imc = simulation.Imc with { Points = duplicatedImc },
            Simc = simulation.Simc with { Points = duplicatedSimc }
        }
    };
    Throws<InvalidDataException>(() => SimulationCsv.Export(csvPath, duplicateStudy));
    Throws<InvalidDataException>(() => StudySheet.Export(sheetPath, project.Name, study, reopenedPi,
        duplicateStudy, training.Summary, validation.Summary, Array.Empty<ReviewEvent>()));
    Require(File.ReadAllBytes(sheetPath).SequenceEqual(originalSheet) &&
        File.ReadAllBytes(csvPath).SequenceEqual(savedBytes), "Duplicate timestamps preserve both previous exports.");
    var artifact = Environment.GetEnvironmentVariable("MKR_SAMPLE_PDF");
    if (!string.IsNullOrWhiteSpace(artifact)) File.Copy(sheetPath, artifact, overwrite: true);

    var acquisitionDb = Path.Combine(directory.FullName, "acquisition.db");
    new SqliteProjectStore(acquisitionDb).Add(project);
    await AcquisitionChecks.Run(acquisitionDb, project.Id, engine);
    {
        var (ticks, step) = ChartMath.NiceTicks(0.3, 9.7, 5);
        Require(step == 2 && ticks.SequenceEqual(new[] { 2.0, 4.0, 6.0, 8.0 }), "Ticks are round multiples of 1, 2 or 5 powers of ten.");
        Require(ChartMath.NiceTicks(5, 5, 5).Ticks.SequenceEqual(new[] { 5.0 }) && ChartMath.NiceTicks(double.NaN, 1, 5).Ticks.Length == 0, "Degenerate and non-finite ranges give safe ticks.");
        var xs = Enumerable.Range(0, 100_000).Select(i => i * 0.01).ToArray();
        var ys = xs.Select(v => Math.Sin(v)).ToArray();
        ys[54_321] = 50;                                                        // a one-sample spike
        var picked = ChartMath.Decimate(xs, ys, 0, xs[^1], 500);
        Require(picked.Length <= 2000 && picked.Contains(54_321), "Decimation bounds the points but never loses an extremum.");
        Require(ChartMath.Decimate(xs, ys, 10, 10.05, 500).Length <= 8, "A narrow window keeps its samples.");
        Require(ChartMath.NearestIndex(xs, 3.004) == 300 && ChartMath.NearestIndex(xs, -5) == 0 && ChartMath.NearestIndex(xs, 1e9) == xs.Length - 1 &&
            ChartMath.NearestIndex(Array.Empty<double>(), 1) == -1, "Cursor lookup picks the nearest sample and survives out-of-range values.");
        var (zlo, zhi) = ChartMath.Zoom(0, 100, 25, 0.5, 0, 100);
        Require(Math.Abs(zlo - 12.5) < 1e-9 && Math.Abs(zhi - 62.5) < 1e-9, "Zoom keeps the point under the cursor fixed.");
        var (plo, phi) = ChartMath.Pan(80, 100, 50, 0, 100);
        Require(plo == 80 && phi == 100, "Pan cannot leave the data.");
        var (flo, fhi) = ChartMath.Zoom(10, 20, 15, 100, 0, 100);
        Require(flo == 0 && fhi == 100, "Zooming out stops at the full range.");
        {
            // zoom in, slide right, zoom out again: the exact full range comes back (no 5.7E-14 residue on the left edge)
            var (lo, hi) = ChartMath.Zoom(0, 693.37, 346.685, 0.7, 0, 693.37);
            (lo, hi) = ChartMath.Pan(lo, hi, 1000, 0, 693.37);
            for (var i = 0; i < 3; i++) (lo, hi) = ChartMath.Zoom(lo, hi, (lo + hi) / 2, 1 / 0.7, 0, 693.37);
            Require(lo == 0 && hi == 693.37, "Zooming out after a pan returns exactly to the full range.");
        }
    }

    {
        // Central texts: every English text has its French reference, with the same placeholders; a gap falls back instead of breaking the screen.
        var french = Loc.FrenchTexts; var english = Loc.EnglishTexts;
        Require(french.Count > 1000 && english.Count > 1000, "The embedded text files are loaded (not left in satellite assemblies).");
        Require(english.Keys.All(french.ContainsKey), "Every English text has a French reference.");
        static int Placeholders(string text) => System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Distinct().Count();
        Require(english.All(pair => Placeholders(pair.Value) == Placeholders(french[pair.Key])), "French and English texts use the same placeholders.");
        Require(french.Values.All(value => !string.IsNullOrWhiteSpace(value)), "No empty French text.");
        Require(french.Keys.Where(key => !english.ContainsKey(key)).All(key => Loc.Lookup(Loc.English, key) == french[key]), "A text without English version falls back to French.");
        Require(Loc.Lookup(Loc.English, "product.description") != Loc.Lookup(Loc.French, "product.description"), "The About description has an English version.");
        Require(Loc.Lookup(Loc.English, "nav.next") == "Next: {0}" && Loc.Lookup(Loc.French, "nav.next") == "Suivant : {0}", "The language selects the text.");
        Require(Loc.Lookup(Loc.French, "no.such.key") == "no.such.key", "An unknown key is shown as itself.");
    }

    {
        // The journey is derived from stored facts only; tuning is closed until a model is accepted.
        var none = StudyJourney.Evaluate(null);
        Require(none.Count == 10 && none.All(s => s.State == StepState.Blocked), "Without a project every step is closed and says why.");
        var empty = StudyJourney.Evaluate(new ProjectFacts(false, 0, 0, 0));
        Require(empty[0].State == StepState.Ready && empty[1].State == StepState.Ready && empty[2].State == StepState.Blocked &&
            empty[3].State == StepState.Blocked && empty[7].State == StepState.Blocked, "An empty project can only be described and fed with data.");
        var data = StudyJourney.Evaluate(new ProjectFacts(true, 2, 0, 0));
        Require(data[0].State == StepState.Done && data[1].State == StepState.Done && data[2].State == StepState.Done && data[3].State == StepState.Ready &&
            data[4].State == StepState.Blocked, "Data open the identification and nothing after it.");
        var failed = StudyJourney.Evaluate(new ProjectFacts(true, 2, 1, 0));
        Require(failed[3].State == StepState.Done && failed[4].State == StepState.Ready && failed[7].State == StepState.Blocked &&
            failed[7].Detail.Contains("non validé"), "A model that failed validation never opens the tuning.");
        var accepted = StudyJourney.Evaluate(new ProjectFacts(true, 2, 1, 1));
        Require(StudyJourney.NextStep(StudyJourney.Evaluate(null)) is null, "No project: no next step, the blocked reason is shown instead.");
        Require(StudyJourney.NextStep(empty)?.Number == 1, "An empty project starts with the description of the process.");
        Require(StudyJourney.NextStep(StudyJourney.Evaluate(new ProjectFacts(true, 0, 0, 0)))?.Number == 2, "A described project without data goes to the acquisition.");
        Require(StudyJourney.NextStep(data)?.Number == 4, "With data and no study the next step is the identification.");
        Require(StudyJourney.NextStep(failed)?.Number == 5, "A refused identification leads to the validation step, never to a tuning.");
        Require(StudyJourney.NextStep(accepted)?.Number == 6, "An accepted model opens the strategy step.");
        Require(accepted[4].State == StepState.Done && accepted.Skip(5).All(s => s.State == StepState.Ready), "An accepted model opens steps 6 to 10.");
    }


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

    // PID prototype must reduce exactly to PI when its derivative action is disabled.
    var prototypePi = new PidPrototypeCoefficients(
        linkedPi.Imc.Kp, linkedPi.Imc.TiSeconds, 0, 0);
    var (pidZero, pidVersion) = await engine.SimulatePidPrototypeAsync(
        fit, prototypePi, scenario, default);
    Require(pidVersion == simulationVersion &&
        pidZero.ControllerForm == "ideal_dependent_pid_derivative_on_pv" &&
        Math.Abs(pidZero.Iae - simulation.Imc.Iae) < 1e-10 &&
        pidZero.Points.Length == simulation.Imc.Points.Length &&
        pidZero.Points.Zip(simulation.Imc.Points,
            (pid, pi) => Math.Abs(pid.Pv - pi.Pv) < 1e-10 &&
                pid.Cv == pi.Cv && pid.Mv == pi.Mv).All(ok => ok),
        "Zero-derivative PID exactly reproduces the established PI simulation.");

    var derivativePi = new PidPrototypeCoefficients(
        linkedPi.Imc.Kp, linkedPi.Imc.TiSeconds, 0.4, 0.1);
    var (pidFiltered, _) = await engine.SimulatePidPrototypeAsync(
        fit, derivativePi, scenario, default);
    Require(pidFiltered.Points[0].Derivative == 0 &&
        pidFiltered.Points[^1].Derivative is null &&
        pidFiltered.Points.Any(p => p.Derivative is { } d && Math.Abs(d) > 1e-10) &&
        pidFiltered.Points.All(p => p.Mv is null or >= -0.1 and <= 0.1),
        "Filtered derivative acts on PV with valid actuator bounds.");

    // Qualified synthetic sensor fixture: verify noise stays separate from plant PV.
    // This does not demonstrate compatibility with an actual sensor or PLC block.
    var noiseSamples = (int)Math.Round(scenario.DurationSeconds / scenario.SampleSeconds);
    var deterministicSensorNoise = Enumerable.Range(0, noiseSamples)
        .Select(index => index == 0 ? 0.1 : 0.0).ToArray();
    var (pidWithNoise, noiseEngineVersion) = await engine.SimulatePidPrototypeAsync(
        fit, derivativePi, scenario, default, deterministicSensorNoise);
    Require(noiseEngineVersion == pidVersion &&
        pidWithNoise.AlgorithmVersion == "0.2.0" &&
        pidWithNoise.Warnings.Contains("deterministic_measurement_noise") &&
        pidWithNoise.Points[0].Pv == 0 &&
        pidWithNoise.Points[0].MeasuredPv == 0.1 &&
        Math.Abs(pidWithNoise.Points[0].Derivative!.Value +
            derivativePi.Kp * derivativePi.TdSeconds /
            (derivativePi.FilterSeconds + scenario.SampleSeconds) * 0.1) < 1e-10 &&
        pidWithNoise.Points[^1].MeasuredPv is null,
        "Deterministic sensor-noise fixture is distinct from process PV and validates D filter.");
    var (zeroNoisePid, _) = await engine.SimulatePidPrototypeAsync(
        fit, derivativePi, scenario, default, new double[noiseSamples]);
    Require(zeroNoisePid.Points.Zip(pidFiltered.Points,
        (a, z) => a.Pv == z.Pv && a.Cv == z.Cv &&
            a.Mv == z.Mv && a.Derivative == z.Derivative).All(ok => ok),
        "Explicit zero noise reproduces every previously simulated PID point.");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await engine.GetVersionAsync(cancellation.Token);
        throw new InvalidOperationException("Expected cancellation.");
    }
    catch (OperationCanceledException) { }

    // NC-02: every estimate carries a validation decision; a refused model is kept but cannot be tuned.
    var gate = new Mkr.Desktop.IdentificationWorkspace(project, restored, restored, engine, restored, engine, engine, engine);
    var strict = new ComparisonCriteria(0.001, 0.5);
    var refused = await gate.CalculateAsync(ModelFamily.FOPDT, trainingSecond.Summary, columns, validationSecond.Summary, columns,
        "%", "bar", 0.01, 0.01, strict, default);
    Require(refused.Assessment is { Accepted: false } decision && decision.Reasons.Contains("validation_rmse") &&
        !refused.IsAcceptedForTuning && refused.Label.EndsWith("refusée"), "A first-order fit of a second-order holdout is refused.");
    var reopenedRefusal = new SqliteProjectStore(legacyPath).ListStudies(project.Id).Single(item => item.Id == refused.Id).Assessment;
    Require(reopenedRefusal is { Accepted: false } r && r.Criteria == strict && r.AlgorithmVersion == refused.Assessment!.AlgorithmVersion &&
        r.Reasons.SequenceEqual(refused.Assessment.Reasons), "The refusal survives SQLite reopening.");
    await ThrowsAsync<ModelNotAcceptedException>(() => gate.CalculatePiAsync(refused, 2, default));
    await ThrowsAsync<ModelNotAcceptedException>(() => gate.CalculatePiAsync(refused with { Assessment = null }, 2, default));
    var acceptedSecond = await gate.CalculateAsync(ModelFamily.SOPDT, trainingSecond.Summary, columns, validationSecond.Summary, columns,
        "%", "bar", 0.01, 0.01, strict, default);
    Require(acceptedSecond.IsAcceptedForTuning, "The second-order model of the same trials is accepted.");
    Require((await gate.CalculateSopdtPidAsync(acceptedSecond, 2, default)).Series.Kc != 0, "An accepted model can be tuned.");
    await ThrowsAsync<ModelNotAcceptedException>(() => gate.CalculateSopdtPidAsync(acceptedSecond with
    {
        Assessment = acceptedSecond.Assessment! with { Accepted = false, Reasons = ["validation_rmse"] }
    }, 2, default));

    // History mode: two steps of ONE long recording (up at 300 s, down at 1500 s) are located, paired and identified.
    // (Not wrapped in braces: the French-export check below compares against these results.)
        var longCsv = new StringBuilder("timestamp;mv;pv\n");
        var longStart = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 600; i++)
        {
            double t = 5.0 * i, mv = t >= 300 && t < 1500 ? 2 : 0, level = 0;
            if (t >= 310) level += 2 * (1 - Math.Exp(-(t - 310) / 40));
            if (t >= 1510) level -= 2 * (1 - Math.Exp(-(t - 1510) / 40));
            longCsv.Append(FormattableString.Invariant($"{longStart.AddSeconds(t):yyyy-MM-ddTHH:mm:ss}Z;{mv};{8 - 1.5 * level:G17}\n"));
        }
        var longRaw = Encoding.UTF8.GetBytes(longCsv.ToString());
        var longMapping = new CsvMapping("timestamp", "pv", "PV", "bar");
        var (longResult, longVersion) = await engine.AnalyzeAsync(longRaw, ";", longMapping, default);
        var longSummary = new ImportSummary(Guid.NewGuid(), "historique-long.csv", Convert.ToHexString(SHA256.HashData(longRaw)), DateTimeOffset.UtcNow);
        restored.SaveImport(project.Id, new CsvImport(longSummary, ";", longMapping, longResult, longVersion, longRaw));

        var search = await gate.FindStepsAsync(longSummary, columns, "%", "bar", 0.01, 0.01, default);
        Require(search.Candidates.Select(c => c.StepRow).SequenceEqual(new[] { 60, 300 }) && search.Rejected.Length == 0,
            "Both steps of the recording are located.");
        var pair = search.Pairs[0];
        Require(pair.OppositeDirection && pair.FirstSettled && pair.SecondSettled && pair.FirstWindow.RowEnd < pair.SecondWindow.RowStart,
            "The proposed pair has opposite directions and row-disjoint windows.");
        var fromHistory = await gate.CalculateAsync(ModelFamily.FOPDT, longSummary, columns, longSummary, columns, "%", "bar",
            0.01, 0.01, new ComparisonCriteria(0.01, 0.5), default, pair.FirstWindow, pair.SecondWindow);
        Require(fromHistory.IsAcceptedForTuning && Math.Abs(fromHistory.Result.Gain + 1.5) < 1e-3 &&
            fromHistory.Result.TauSeconds is { } historyTau && Math.Abs(historyTau - 40) < 0.1 &&
            fromHistory.TrainingId == fromHistory.ValidationId,
            $"Two windows of one recording identify and validate the model. accepted={fromHistory.IsAcceptedForTuning} " +
            $"reasons={string.Join(',', fromHistory.Assessment?.Reasons ?? [])} gain={fromHistory.Result.Gain} tau={fromHistory.Result.TauSeconds}");
        Require(fromHistory.TrainingWindow == pair.FirstWindow && fromHistory.ValidationWindow == pair.SecondWindow,
            "The study keeps the exact source ranges.");
        using (var verify = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath, Pooling = false, ForeignKeys = true }.ToString()))
        {
            verify.Open();
            using var query = verify.CreateCommand();
            query.CommandText = "PRAGMA user_version";
            Require((long)query.ExecuteScalar()! == 11, "The database was migrated to version 11 (study table of version 9, audit log of version 10, ARX studies of version 11).");
            query.CommandText = "PRAGMA foreign_key_check";
            using var violations = query.ExecuteReader();
            Require(!violations.Read(), "The migrated database has no orphan reference.");
        }
        // Defense in depth: even bypassing the application rule, the database refuses one source with identical or missing ranges.
        Throws<SqliteException>(() => restored.SaveStudy(project.Id, fromHistory with { Id = Guid.NewGuid(), ValidationWindow = fromHistory.TrainingWindow }));
        Throws<SqliteException>(() => restored.SaveStudy(project.Id, fromHistory with { Id = Guid.NewGuid(), TrainingWindow = null }));
        // ARX studies (migration 11, ADR 0007): round trip, refusals kept, same independence rule enforced by the database itself.
        var arxResult = new ArxIdentification("1.0.0", 1.0, "refused", null,
            [new ArxCandidate(1, 1, 2, 3, "OE", 0.4, 0.1, ["validation_residuals"], false)], ["validation_residuals"], ["Prévoir un essai de validation plus informatif."]);
        var arxStudy = new ArxStudy(Guid.NewGuid(), fromHistory.TrainingId, fromHistory.ValidationId, columns, columns, "%", "bar", 0.01, 0.1,
            DateTimeOffset.UtcNow, "1.0.0", arxResult, fromHistory.TrainingWindow, fromHistory.ValidationWindow);
        restored.SaveArxStudy(project.Id, arxStudy);
        var arxBack = restored.ListArxStudies(project.Id).Single(item => item.Id == arxStudy.Id);
        Require(!arxBack.IsAccepted && arxBack.Result.Reasons is { Length: 1 } && arxBack.Result.Candidates[0].Estimator == "OE" &&
                arxBack.TrainingWindow == arxStudy.TrainingWindow && arxBack.MaximumRmse == 0.1, "A refused ARX study is saved and read back whole.");
        Throws<SqliteException>(() => restored.SaveArxStudy(project.Id, arxStudy with { Id = Guid.NewGuid(), ValidationWindow = arxStudy.TrainingWindow }));
        Throws<SqliteException>(() => restored.SaveArxStudy(project.Id, arxStudy with { Id = Guid.NewGuid(), TrainingWindow = null }));
        Throws<InvalidDataException>(() => restored.SaveArxStudy(Guid.NewGuid(), arxStudy with { Id = Guid.NewGuid() }));
        Require(restored.ListArxStudies(project.Id).Count == 1, "Rejected ARX studies leave no trace.");
        Throws<CsvInputException>(() => gate.CalculateAsync(ModelFamily.FOPDT, longSummary, columns, longSummary, columns, "%", "bar",
            0.01, 0.01, new ComparisonCriteria(0.01, 0.5), default).GetAwaiter().GetResult());
        var overlapping = pair.FirstWindow with { RowEnd = pair.SecondWindow.RowStart + 3 };
        Require(TrialIndependence.Check(longSummary, pair.FirstWindow, longSummary, pair.SecondWindow) is null &&
            TrialIndependence.Check(longSummary, null, longSummary, pair.SecondWindow) == "fit_same_source_window" &&
            TrialIndependence.Check(longSummary, overlapping, longSummary, pair.SecondWindow) == "fit_same_source_overlap" &&
            TrialIndependence.Check(longSummary, pair.FirstWindow, longSummary with { Id = Guid.NewGuid() }, pair.SecondWindow) == "fit_duplicate_source",
            "Same-source trials need explicit, non-overlapping ranges; identical bytes under another import stay refused.");
        await ThrowsAsync<CsvInputException>(() => gate.CalculateAsync(ModelFamily.FOPDT, longSummary, columns, longSummary, columns,
            "%", "bar", 0.01, 0.01, new ComparisonCriteria(0.01, 0.5), default, pair.FirstWindow, overlapping));

    // NC-16: a French historian export (semicolon, decimal comma, local date and time in Europe/Paris) is read
    // exactly like the same recording written in ISO 8601: same steps, same model.
    {
        var france = new CsvFormat(",", new CsvTimeFormat("%d/%m/%Y %H:%M:%S", "Europe/Paris"));
        var frenchCsv = new StringBuilder("Horodatage;Commande;Mesure\n");
        var localStart = new DateTime(2026, 7, 1, 8, 0, 0);
        for (var i = 0; i < 600; i++)
        {
            double t = 5.0 * i, mv = t >= 300 && t < 1500 ? 2 : 0, level = 0;
            if (t >= 310) level += 2 * (1 - Math.Exp(-(t - 310) / 40));
            if (t >= 1510) level -= 2 * (1 - Math.Exp(-(t - 1510) / 40));
            frenchCsv.Append(FormattableString.Invariant($"{localStart.AddSeconds(t):dd/MM/yyyy HH:mm:ss};{mv};{8 - 1.5 * level:G17}\n").Replace('.', ','));
        }
        var frenchRaw = Encoding.UTF8.GetBytes(frenchCsv.ToString());
        var inspected = await engine.InspectAsync(frenchRaw, ";", default);
        Require(inspected.SuggestedFormat == new CsvFormatSuggestion(",", "Horodatage", "%d/%m/%Y %H:%M:%S", false),
            "The first rows propose the French format without applying it.");
        var frenchMapping = new CsvMapping("Horodatage", "Mesure", "PV", "bar");
        var unread = (await engine.AnalyzeAsync(frenchRaw, ";", frenchMapping, default)).Result.Quality!;
        // Every timestamp and every decimal-comma value is refused; only plain integers such as "8" remain valid.
        Require(unread.InvalidTimestamps == 600 && unread.InvalidValues > 0 && unread.ValidValues + unread.InvalidValues == 600,
            "Without the declared format the French export is refused, never misread.");
        var (frenchResult, frenchVersion) = await engine.AnalyzeAsync(frenchRaw, ";", frenchMapping, default, france);
        Require(frenchResult.Quality is { ValidValues: 600, InvalidValues: 0, InvalidTimestamps: 0, MinPositiveStepSeconds: 5, MaxPositiveStepSeconds: 5 },
            "The declared format reads all 600 rows with a uniform 5 s period.");
        var frenchSummary = new ImportSummary(Guid.NewGuid(), "export-historien.csv", Convert.ToHexString(SHA256.HashData(frenchRaw)), DateTimeOffset.UtcNow);
        restored.SaveImport(project.Id, new CsvImport(frenchSummary, ";", frenchMapping, frenchResult, frenchVersion, frenchRaw, Format: france));
        var reopenedFrench = new SqliteProjectStore(legacyPath).ReadImport(project.Id, frenchSummary.Id);
        Require(reopenedFrench.Format == france && reopenedFrench.Original.SequenceEqual(frenchRaw) &&
            reopenedFrench.Format.Describe().Contains("Europe/Paris"), "The format and the unchanged original survive SQLite reopening.");
        var frenchColumns = new StepColumns("Horodatage", "Commande", "Mesure");
        var frenchSearch = await gate.FindStepsAsync(frenchSummary, frenchColumns, "%", "bar", 0.01, 0.01, default);
        var isoSearch = await gate.FindStepsAsync(longSummary, columns, "%", "bar", 0.01, 0.01, default);
        Require(frenchSearch.Candidates.Select(c => (c.StepRow, c.Amplitude, c.RowStart, c.RowEnd)).SequenceEqual(
                isoSearch.Candidates.Select(c => (c.StepRow, c.Amplitude, c.RowStart, c.RowEnd))) &&
            frenchSearch.Candidates[1].StartUtc - frenchSearch.Candidates[0].StartUtc ==
                isoSearch.Candidates[1].StartUtc - isoSearch.Candidates[0].StartUtc,
            "The French export yields the same steps, at the same spacing, as the ISO recording.");
        var frenchPair = frenchSearch.Pairs[0];
        var frenchStudy = await gate.CalculateAsync(ModelFamily.FOPDT, frenchSummary, frenchColumns, frenchSummary, frenchColumns, "%", "bar",
            0.01, 0.01, new ComparisonCriteria(0.01, 0.5), default, frenchPair.FirstWindow, frenchPair.SecondWindow);
        Require(frenchStudy.IsAcceptedForTuning && Math.Abs(frenchStudy.Result.Gain - fromHistory.Result.Gain) < 1e-9 &&
            Math.Abs(frenchStudy.Result.TauSeconds!.Value - fromHistory.Result.TauSeconds!.Value) < 1e-9,
            "Identification on the French export equals identification on the ISO recording.");
        var frenchPreview = await engine.PreviewTrialAsync(new StepImport(reopenedFrench, frenchColumns), default);
        Require(frenchPreview.SourceRows == 600 && frenchPreview.FirstUtc == new DateTimeOffset(2026, 7, 1, 6, 0, 0, TimeSpan.Zero),
            "Preview converts 08:00 Europe/Paris in July to 06:00 UTC.");
        var badZone = new CsvFormat(",", new CsvTimeFormat("%d/%m/%Y %H:%M:%S", "Europe/Atlantis"));
        await ThrowsAsync<CsvInputException>(() => engine.AnalyzeAsync(frenchRaw, ";", frenchMapping, default, badZone));
        await ThrowsAsync<CsvInputException>(() => engine.AnalyzeAsync(frenchRaw, ",", frenchMapping, default, france));
    }

    // Audit log: append-only, hash-chained, tamper-evident (migration 10).
    {
        var auditPath = Path.Combine(directory.FullName, "audit.db");
        var auditStore = new SqliteProjectStore(auditPath);
        var audited = Project.Create("Journal");
        auditStore.Add(audited);
        Require(auditStore.Verify(audited.Id) is { Intact: true, Entries: 0 } empty && empty.HeadHash == AuditHash.Genesis, "An empty log is intact.");
        var entryOne = auditStore.Record(audited.Id, "operateur", "identification.saved", "etude-1", "FOPDT accepté");
        var entryTwo = auditStore.Record(audited.Id, "operateur", "block.values.shown", "PIDFF", "non qualifié");
        var entryThree = auditStore.Record(audited.Id, "relecteur", "model.drift.checked", "etude-1", "consistent");
        Require(entryOne.Sequence == 1 && entryTwo.Sequence == 2 && entryThree.Sequence == 3 && entryOne.PreviousHash == AuditHash.Genesis &&
            entryTwo.PreviousHash == entryOne.Hash && entryThree.PreviousHash == entryTwo.Hash, "Entries are chained in order.");
        var listed = new SqliteProjectStore(auditPath).List(audited.Id);
        Require(listed.Count == 3 && listed[2].Hash == entryThree.Hash && listed[0].At == entryOne.At, "The log survives reopening unchanged.");
        Require(auditStore.Verify(audited.Id) is { Intact: true, Entries: 3 } ok && ok.HeadHash == entryThree.Hash, "An untouched log verifies; its head hash identifies it.");
        Throws<ArgumentException>(() => auditStore.Record(audited.Id, " ", "x", "y", "z"));
        Throws<InvalidDataException>(() => auditStore.Record(Guid.NewGuid(), "operateur", "x", "y", "z"));

        // Concurrent writers never share a sequence number.
        await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() =>
            new SqliteProjectStore(auditPath).Record(audited.Id, "operateur", "acquisition.saved", $"capture-{i}", "ok"))));
        Require(auditStore.Verify(audited.Id) is { Intact: true, Entries: 19 }, "16 concurrent entries leave a continuous, intact chain.");

        // Export: verified by .NET AND by the independent Python script; any edit of the file is detected by both.
        var exported = AuditExport.Write(audited.Id, "Journal", auditStore.List(audited.Id), DateTimeOffset.UtcNow);
        Require(AuditExport.Verify(exported) is { Intact: true, Entries: 19 } && AuditExport.Verify(exported).HeadHash == auditStore.Verify(audited.Id).HeadHash,
            "An exported log verifies and carries the same head hash.");
        var exportFile = Path.Combine(directory.FullName, "journal.jsonl");
        File.WriteAllText(exportFile, exported);
        var verifier = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "verify-audit-export.py"));
        if (!File.Exists(verifier)) verifier = Environment.GetEnvironmentVariable("MKR_AUDIT_VERIFIER") ?? verifier;
        Require(File.Exists(verifier) || Environment.GetEnvironmentVariable("CI") != "true", "In CI the independent verifier script must be found, not silently skipped.");
        if (File.Exists(verifier))
        {
            int RunVerifier(string file, string? head = null)
            {
                var info = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("MKR_PYTHON") ?? "python") { RedirectStandardOutput = true };
                foreach (var argument in new[] { "-I", verifier, file }.Concat(head is null ? [] : [head])) info.ArgumentList.Add(argument);
                using var process = System.Diagnostics.Process.Start(info)!;
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
            Require(RunVerifier(exportFile) == 0, "The independent Python verifier accepts the export.");
            Require(RunVerifier(exportFile, auditStore.Verify(audited.Id).HeadHash) == 0, "...and the head hash noted elsewhere.");
            Require(RunVerifier(exportFile, new string('a', 64)) == 1, "...but refuses a different head hash (a rewritten project).");
            var tamperedFile = Path.Combine(directory.FullName, "journal-modifie.jsonl");
            File.WriteAllText(tamperedFile, exported.Replace("capture-3", "capture-X"));
            Require(RunVerifier(tamperedFile) == 1, "The Python verifier detects an edited entry.");
        }
        var tamperedText = exported.Replace("capture-3", "capture-X");
        Require(AuditExport.Verify(tamperedText) is { Intact: false }, "The .NET verifier detects an edited entry.");
        Require(AuditExport.Verify(string.Join('\n', exported.Split('\n').Where(l => !l.Contains("capture-5")))) is { Intact: false }, "A removed line is detected.");
        Require(AuditExport.Verify("pas du json\n") is { Intact: false } && AuditExport.Verify("") is { Intact: false }, "Malformed files are refused, not skipped.");
        // Characters outside the Basic Multilingual Plane are length-prefixed in UTF-16 code units by both implementations.
        var emojiProject = Project.Create("Émoji");
        auditStore.Add(emojiProject);
        auditStore.Record(emojiProject.Id, "opérateur", "x", "étude 🙂", "détail 🙂 « guillemets » \"json\" \\");
        var emojiExport = AuditExport.Write(emojiProject.Id, "Émoji", auditStore.List(emojiProject.Id), DateTimeOffset.UtcNow);
        Require(AuditExport.Verify(emojiExport).Intact, "Non-ASCII text round-trips through the export.");
        if (File.Exists(verifier))
        {
            var emojiFile = Path.Combine(directory.FullName, "emoji.jsonl");
            File.WriteAllText(emojiFile, emojiExport);
            var info = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("MKR_PYTHON") ?? "python") { RedirectStandardOutput = true };
            foreach (var argument in new[] { "-I", verifier, emojiFile }) info.ArgumentList.Add(argument);
            using var run = System.Diagnostics.Process.Start(info)!;
            run.StandardOutput.ReadToEnd();
            run.WaitForExit();
            Require(run.ExitCode == 0, "The Python verifier agrees with .NET on non-ASCII and astral characters.");
        }

        // The triggers refuse an edit or a deletion through SQL.
        using var auditRaw = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = auditPath, Pooling = false }.ToString());
        auditRaw.Open();
        void Execute(string sql) { using var c = auditRaw.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
        Throws<SqliteException>(() => Execute("UPDATE audit_events SET detail='modifié' WHERE sequence=2"));
        Throws<SqliteException>(() => Execute("DELETE FROM audit_events WHERE sequence=2"));
        // Someone with full access removes the triggers and edits an entry: the chain shows where.
        Execute("DROP TRIGGER audit_events_no_update");
        Execute("UPDATE audit_events SET detail='modifié' WHERE sequence=2");
        Require(auditStore.Verify(audited.Id) is { Intact: false, FirstBrokenSequence: 2 }, "An edited entry is located by the chain.");
        Execute("UPDATE audit_events SET detail='non qualifié' WHERE sequence=2");
        Require(auditStore.Verify(audited.Id).Intact, "Restoring the exact content restores the chain (the hash binds the content).");
        Execute("DROP TRIGGER audit_events_no_delete");
        Execute("DELETE FROM audit_events WHERE sequence=3");
        Require(auditStore.Verify(audited.Id) is { Intact: false, FirstBrokenSequence: 4 }, "A removed entry breaks the chain at the next one.");
    }

    // PIDFF documentary conversion (Schneider 33002536, p. 173-183): mixed structure, never qualified, never ready to load.
    {
        var negative = await engine.ConvertToPidffAsync(-1.5, 12.5, 2.0, 0.5, default);
        Require(negative is { Converted: true, Conversion: { Qualification: "non_qualified", ReadyToLoad: false, Structure: "mixed" } } &&
            negative.Conversion.Parameters is { Kp: 1.5, RevDir: true, MixPar: false, PvDev: false, Kd: 4.0 } &&
            negative.Conversion.Parameters.Ti.Iec == "T#12500ms" && negative.Conversion.Parameters.Td.Iec == "T#2000ms",
            "A negative Kp becomes |kp| with rev_dir = 1, and the times become IEC TIME literals.");
        var positive = await engine.ConvertToPidffAsync(0.9, 6, 0, null, default);
        Require(positive.Conversion is { Parameters: { Kp: 0.9, RevDir: false, Kd: null } }, "A positive Kp gives rev_dir = 0 and no derivative.");
        var text = positive.Conversion!.Describe("SIMC", "%", "bar");
        Require(text.Contains("NON QUALIFIÉ") && text.Contains("rev_dir = 0") && text.Contains("p. 173") && text.Contains("ovs_att = 0") &&
            text.Contains("outrate"), "The displayed values state that they are unqualified and cite their pages.");
        foreach (var (reason, call) in new (string, Func<Task<BlockConversionResult>>)[]
        {
            ("conversion_filter_required", () => engine.ConvertToPidffAsync(1, 5, 2, null, default)),
            ("conversion_kd_below_one", () => engine.ConvertToPidffAsync(1, 5, 2, 3, default)),
            ("conversion_parameters", () => engine.ConvertToPidffAsync(0, 5, 0, null, default))
        })
        {
            var refusal = await call();
            Require(refusal is { Converted: false, Conversion: null } && refusal.Reason == reason && refusal.RefusalText.Length > 10,
                $"The conversion is refused with the reason {reason}.");
        }
        var piOnModel = await gate.ConvertToPidffAsync(fromHistory, new PiStudy(Guid.NewGuid(), fromHistory.Id, DateTimeOffset.UtcNow, "t",
            (await engine.CalculatePiAsync(fromHistory.Result.Gain, fromHistory.Result.TauSeconds!.Value, fromHistory.Result.DelaySeconds, 5, default)).Result), true, default);
        Require(piOnModel.Conversion!.Parameters.RevDir && piOnModel.Conversion.Parameters.Kp > 0,
            "A negative-gain model (K = -1.5) gives a PIDFF with rev_dir = 1.");
        var refusedTuning = new PiStudy(Guid.NewGuid(), refused.Id, DateTimeOffset.UtcNow, "t", (await engine.CalculatePiAsync(-2, 100, 3, 2, default)).Result);
        await ThrowsAsync<ModelNotAcceptedException>(() => gate.ConvertToPidffAsync(refused, refusedTuning, false, default));
    }

    // Contract hardening: the real engine's reply with one value corrupted (missing, null, wrong type or shape, absurd number).
    // Every violation must surface as InvalidDataException or a coded CsvInputException, never as another exception type.
    {
        var fakes = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fakes", "mutating_engine.py"));
        if (!File.Exists(fakes)) fakes = Environment.GetEnvironmentVariable("MKR_FAKE_SCRIPT") ?? fakes;
        Require(File.Exists(fakes) || Environment.GetEnvironmentVariable("CI") != "true", "In CI the mutating fake engine must be found.");
        if (File.Exists(fakes))
        {
            Environment.SetEnvironmentVariable("MKR_FAKE_ENGINE_DIR", Path.Combine(AppContext.BaseDirectory, "engine"));
            var mutated = new PythonDiagnostics(interpreter, fakes);
            var csvBytes = Encoding.UTF8.GetBytes("timestamp;value\n2026-01-01T00:00:00Z;1\n2026-01-01T00:00:01Z;2\n2026-01-01T00:00:02Z;3\n");
            var calls = new (string Name, Func<Task> Call)[]
            {
                ("GetVersion", () => mutated.GetVersionAsync(default)),
                ("Inspect", () => mutated.InspectAsync(csvBytes, ";", default)),
                ("Analyze", () => mutated.AnalyzeAsync(csvBytes, ";", new CsvMapping("timestamp", "value", "PV", "bar"), default)),
                ("Compare", () => mutated.CompareAsync(new(training, columns), new(validation, columns), 0.01, 0.01, 1, 1, default)),
                ("FindSteps", () => mutated.FindStepsAsync(new(training, columns), 0.01, 0.01, default)),
                ("CheckDrift", () => mutated.CheckModelDriftAsync(ModelFamily.FOPDT, fit, new(training, columns), 0.01, 0.01, new ComparisonCriteria(1, 1), default)),
                ("SuggestCriteria", () => mutated.SuggestCriteriaAsync(new(training, columns), new(validation, columns), 0.01, 0.01, default)),
                ("Identify", () => mutated.IdentifyAsync(ModelFamily.FOPDT, new(training, columns), new(validation, columns), 0.01, 0.01, default)),
                ("Assess", () => mutated.AssessAsync(ModelFamily.FOPDT, new(training, columns), new(validation, columns), 0.01, 0.01, new ComparisonCriteria(1, 1), default)),
                ("PreviewTrial", () => mutated.PreviewTrialAsync(new(training, columns), default)),
                ("SopdtPid", () => mutated.CalculateSopdtPidAsync(second, 2, default)),
                ("IntegratorPi", () => mutated.CalculateIntegratorPiAsync(-0.3, 2.5, 5, default)),
                ("Pidff", () => mutated.ConvertToPidffAsync(-1.5, 12.5, 2.0, 0.5, default)),
                ("Block", () => mutated.ConvertToBlockAsync("PID_INT", new Dictionary<string, object>
                    { ["kp"] = 2.0, ["tiSeconds"] = 12.5, ["tdSeconds"] = 0.0, ["tsSeconds"] = 0.2, ["pvSpan"] = 200.0, ["mvSpan"] = 100.0 }, default)),
                ("CalculatePi", () => mutated.CalculatePiAsync(-2, 100, 3, 2, default)),
                ("Transfer", () => mutated.SimulateTransferAsync(fit, linkedPi.Simc, scenario with { Disturbance = 0 }, 2, 1, default)),
                ("SimulatePi", () => mutated.SimulatePiAsync(fit, linkedPi, scenario, default)),
                ("Robustness", () => mutated.AnalyzeRobustnessAsync(fit, linkedPi, scenario, 20, default)),
                ("Combined", () => mutated.AnalyzeCombinedAsync(fit, linkedPi, scenario, 20, default)),
                ("PidPrototype", () => mutated.SimulatePidPrototypeAsync(fit, prototypePi, scenario, default)),
                ("OpenLoop", () => mutated.SimulateOpenLoopAsync(persisted.Result, new OpenLoopScenario(1, 8, 4, 1, -2, 2, 1), default)),
            };
            var surprises = new SortedDictionary<string, string>();
            int violations = 0, harmless = 0;
            foreach (var (name, call) in calls)
                for (var seed = 1; seed <= int.Parse(Environment.GetEnvironmentVariable("MKR_FAKE_SEEDS") ?? "8"); seed++)
                {
                    Environment.SetEnvironmentVariable("MKR_FAKE_SEED", seed.ToString());
                    try { await call(); harmless++; }
                    catch (InvalidDataException) { violations++; }
                    catch (CsvInputException) { violations++; }
                    catch (Exception error) { surprises.TryAdd($"{name}", $"{error.GetType().Name} (graine {seed}): {error.Message.Split('\n')[0]}"); }
                }
            Environment.SetEnvironmentVariable("MKR_FAKE_SEED", null);
            Require(surprises.Count == 0, "Contract violations must end as InvalidDataException or CsvInputException. Surprises: " +
                string.Join(" | ", surprises.Select(kv => kv.Key + " -> " + kv.Value)));
            Require(violations > calls.Length, $"The corruption is actually exercised ({violations} refused, {harmless} harmless).");
        }
    }

    // Loop monitoring: a validated FOPDT model against a new open-loop step.
    {
        var criteria = new ComparisonCriteria(0.05, 0.5);
        var same = await SaveStepAsync(9, 1.8);
        var consistent = await engine.CheckModelDriftAsync(ModelFamily.FOPDT, fit, new StepImport(same, columns), 0.01, 0.01, criteria, default);
        Require(consistent.Verdict == "consistent" && consistent.DriftedParameters.Length == 0 && consistent.Changes.Count == 3,
            "A new step of the same process is consistent with the validated model.");
        Require(consistent.Describe("%", "bar").Contains("reste cohérent"), "The verdict is explained in French.");
        var other = await engine.CheckModelDriftAsync(ModelFamily.FOPDT, fit, new StepImport(trainingSecond, columns), 0.01, 0.01, criteria, default);
        Require(other.Verdict != "consistent" && other.Reasons.Length > 0, "A second-order response is not consistent with the first-order model.");
        // The other families: the same process is consistent; a model of another family is not.
        var secondSame = await SaveStepAsync(12, 1.8, ModelFamily.SOPDT);
        var secondReport = await engine.CheckModelDriftAsync(ModelFamily.SOPDT, second, new StepImport(secondSame, columns), 0.01, 0.01, criteria, default);
        Require(secondReport.Verdict == "consistent" && secondReport.Changes.Count == 4, "A new second-order step is consistent with the SOPDT model.");
        var tankSame = await SaveStepAsync(13, 1.8, ModelFamily.Integrateur);
        var tankReport = await engine.CheckModelDriftAsync(ModelFamily.Integrateur, third, new StepImport(tankSame, columns), 0.01, 0.01, criteria, default);
        Require(tankReport.Verdict == "consistent" && tankReport.Changes.Count == 2, "A new integrator step is consistent with the integrating model.");
        var wrongFamily = await engine.CheckModelDriftAsync(ModelFamily.FOPDT, fit, new StepImport(tankSame, columns), 0.01, 0.01, criteria, default);
        Require(wrongFamily.Verdict != "consistent", "A tank response is not consistent with a first-order model.");
        await ThrowsAsync<InvalidOperationException>(() => engine.CheckModelDriftAsync(ModelFamily.BroidaFOPDT, fit, new StepImport(same, columns), 0.01, 0.01, criteria, default));
        await ThrowsAsync<ModelNotAcceptedException>(() => gate.CheckModelDriftAsync(refused, same.Summary, columns, "%", "bar", 0.01, 0.01, null, default));
        await ThrowsAsync<InvalidOperationException>(() => gate.CheckModelDriftAsync(acceptedSecond with { Family = ModelFamily.BroidaFOPDT }, same.Summary, columns, "%", "bar", 0.01, 0.01, null, default));
    }

    // PI for an integrating process (SIMC rule, Skogestad 2003): Kc = 1 / (k' (tau_c + theta)), Ti = 4 (tau_c + theta).
    {
        var level = await engine.CalculateIntegratorPiAsync(-0.3, 2.5, 5, default);
        Require(level.Rule == "SIMC_integrating" && Math.Abs(level.Kp - 1 / (-0.3 * 7.5)) < 1e-12 && Math.Abs(level.TiSeconds - 30) < 1e-12 &&
            level.Margins.PhaseMarginDegrees is > 30 && !level.Warnings.Contains("tight_response"),
            "The integrator PI follows the SIMC rule with the sign of the slope.");
        var tight = await engine.CalculateIntegratorPiAsync(0.05, 10, 2, default);
        Require(tight.Warnings.Contains("tight_response") && tight.Warnings.Contains("high_sensitivity"), "A response faster than the delay is announced.");
        var block = await engine.ConvertToPidffAsync(level.Kp, level.TiSeconds, 0, null, default);
        Require(block.Conversion is { Parameters: { RevDir: true, Kd: null } } && Math.Abs(block.Conversion.Parameters.Kp - 0.4444444) < 1e-6,
            "A negative-slope integrator gives a PIDFF with rev_dir = 1 and |kp|.");
        await ThrowsAsync<InvalidOperationException>(() => gate.CalculateIntegratorPiAsync(refused, 2, default));
    }

    // PID_INT and PID_Compact documentary conversions (33002520 p. 141; A5E35300227-AJ p. 88/91): never qualified, never ready to load.
    {
        var pidInt = await engine.ConvertToBlockAsync("PID_INT", new Dictionary<string, object>
            { ["kp"] = 2.0, ["tiSeconds"] = 12.5, ["tdSeconds"] = 0.0, ["tsSeconds"] = 0.2, ["pvSpan"] = 200.0, ["mvSpan"] = 100.0 }, default);
        Require(pidInt.Settings is { Qualification: "non_qualified", ReadyToLoad: false, Block: "PID_INT" } &&
            pidInt.Settings.Parameters["KP"].GetInt32() == 400 && pidInt.Settings.Parameters["TI"].GetInt32() == 125 &&
            pidInt.Settings.Parameters["TS"].GetInt32() == 20, "PID_INT registers follow KP x100, TI in 0.1 s, TS in 0.01 s.");
        Require(pidInt.Settings!.Describe("SIMC").Contains("NON QUALIFIÉ") && pidInt.Settings.Describe("SIMC").Contains("KP = 400"),
            "The PID_INT values are displayed as unqualified.");
        var derivative = await engine.ConvertToBlockAsync("PID_INT", new Dictionary<string, object>
            { ["kp"] = 1.0, ["tiSeconds"] = 10.0, ["tdSeconds"] = 2.0, ["tsSeconds"] = 0.1, ["pvSpan"] = 100.0, ["mvSpan"] = 100.0 }, default);
        Require(derivative is { Converted: false, Reason: "conversion_derivative_undocumented" } && derivative.RefusalText.Length > 10,
            "PID_INT refuses a derivative action: its equation is not in the manual.");
        var compact = await engine.ConvertToBlockAsync("PID_Compact", new Dictionary<string, object>
            { ["version"] = "V3.0", ["kp"] = -0.5, ["tiSeconds"] = 15.0, ["tdSeconds"] = 0.0, ["outputPercentPerMvUnit"] = 2.0,
              ["cpuFamily"] = "S7-1500", ["firmware"] = "3.1" }, default);
        Require(compact.Settings is { Block: "PID_Compact", ReadyToLoad: false } && compact.Settings.Parameters["Gain"].GetDouble() == 1.0 &&
            compact.Settings.Parameters["InvertControl"].GetBoolean() && compact.Settings.Parameters["DeadZone"].GetDouble() == 0.0,
            "PID_Compact: negative Kp gives InvertControl with a positive Gain, the V3 dead zone is 0.");
        var incompatible = await engine.ConvertToBlockAsync("PID_Compact", new Dictionary<string, object>
            { ["version"] = "V3.0", ["kp"] = 1.0, ["tiSeconds"] = 15.0, ["tdSeconds"] = 0.0, ["outputPercentPerMvUnit"] = 1.0,
              ["cpuFamily"] = "S7-1500", ["firmware"] = "3.0" }, default);
        Require(incompatible is { Converted: false, Reason: "conversion_hardware_incompatible" }, "V3.0 is refused on an S7-1500 with firmware 3.0.");
        await ThrowsAsync<ArgumentException>(() => engine.ConvertToBlockAsync("PIDFF", new Dictionary<string, object>(), default));
        var gated = await gate.ConvertToBlockAsync(fromHistory, new PiStudy(Guid.NewGuid(), fromHistory.Id, DateTimeOffset.UtcNow, "t",
            (await engine.CalculatePiAsync(fromHistory.Result.Gain, fromHistory.Result.TauSeconds!.Value, fromHistory.Result.DelaySeconds, 5, default)).Result), true,
            "PID_Compact", new Dictionary<string, object> { ["version"] = "V2.4", ["outputPercentPerMvUnit"] = 1.0 }, default);
        Require(gated.Settings!.Parameters["InvertControl"].GetBoolean(), "A negative-gain model gives InvertControl = TRUE.");
        var refusedPi = new PiStudy(Guid.NewGuid(), refused.Id, DateTimeOffset.UtcNow, "t", (await engine.CalculatePiAsync(-2, 100, 3, 2, default)).Result);
        await ThrowsAsync<ModelNotAcceptedException>(() => gate.ConvertToBlockAsync(refused, refusedPi, false,
            "PID_Compact", new Dictionary<string, object>(), default));
    }

    // The workspace traces sensitive actions in the project's audit log, and the chain stays intact.
    {
        var actions = restored.List(project.Id).Select(e => e.Action).ToHashSet();
        Require(actions.Contains("identification.saved") && actions.Contains("block.values.shown"),
            "Saved identifications and displayed block values are traced: " + string.Join(", ", actions));
        Require(restored.List(project.Id).All(e => e.Actor.Length > 0 && !e.Detail.Contains("Exception")), "Entries name an actor and carry a readable detail.");
        Require(restored.Verify(project.Id).Intact, "The audit chain of the working project is intact.");
        Require(restored.List(project.Id).Where(e => e.Action == "block.values.shown").All(e => e.Detail.Contains("NON QUALIFIÉES") || e.Detail.Contains("refusé")),
            "A displayed block value is always traced as unqualified or refused.");
    }

    // Identity from the session and the four-eyes rule on reviews.
    {
        var original = CurrentUser.Provider;
        try
        {
            var author = new UserIdentity("USINE\\dupont", "S-1-5-21-1-2-3-1001", "POSTE-01");
            var reviewer = new UserIdentity("USINE\\martin", "S-1-5-21-1-2-3-1002", "POSTE-02");
            var run = Guid.NewGuid();
            CurrentUser.Provider = new FixedIdentity(author);
            var calculated = gate.RecordReview(run, ReviewStage.Model, null, ReviewState.Calculated, "Identification terminée", "essai-1");
            Require(calculated.Reviewer == author.Label && author.Label.Contains("S-1-5-21") && author.Label.Contains("@POSTE-01"),
                "The reviewer is the session identity (account, SID, machine), never typed text.");
            Throws<ReviewSeparationException>(() => gate.RecordReview(run, ReviewStage.Model, ReviewState.Calculated, ReviewState.Verified, "Relu", "essai-1"));
            var self = gate.RecordReview(run, ReviewStage.Model, ReviewState.Calculated, ReviewState.Verified, "Seul sur ce projet", "essai-1", acknowledgeSelfReview: true);
            Require(self.Reviewer == author.Label + ReviewSeparation.SelfReviewMark, "A declared self-review is recorded as such.");
            CurrentUser.Provider = new FixedIdentity(reviewer);
            var approved = gate.RecordReview(run, ReviewStage.Model, ReviewState.Verified, ReviewState.Approved, "Approuvé par un tiers", "essai-1");
            Require(approved.Reviewer == reviewer.Label, "Another account approves without declaration.");
            // Self-review stays visible: a later check by the author of an already self-reviewed stage is still the same account.
            var entries = restored.List(project.Id).Where(e => e.Action == "review.recorded").ToArray();
            Require(entries.Length == 3 && entries[0].Actor == author.Label && entries[2].Actor == reviewer.Label &&
                entries[1].Detail.Contains("AUTO-REVUE"), "The audit log names the session identity and flags the self-review.");
            Require(restored.Verify(project.Id).Intact, "The chain stays intact.");
            Require(new EnvironmentIdentityProvider().Current.Label.Contains("@"), "The fallback identity still names a machine.");
        }
        finally { CurrentUser.Provider = original; }
    }

    var fixture = Environment.GetEnvironmentVariable("MKR_TRANSFER_FIXTURE");
    if (!string.IsNullOrEmpty(fixture))
    {
        var rapidTraining = await SaveStepAsync(7, 2, delaySeconds: 0.5);
        var rapidValidation = await SaveStepAsync(8, -1.5, delaySeconds: 0.5);
        var (rapidFit, rapidVersion) = await engine.IdentifyAsync(ModelFamily.FOPDT,
            new StepImport(rapidTraining, columns), new StepImport(rapidValidation, columns), 0.01, 0.01, default);
        Require(rapidFit.DelaySeconds > 0 && rapidFit.DelaySeconds < 1 && rapidFit.Warnings.Contains("delay_resolution"),
            "A positive half-sample delay must raise the resolution warning.");
        var (_, rapidAssessment, _) = await engine.AssessAsync(ModelFamily.FOPDT, new StepImport(rapidTraining, columns),
            new StepImport(rapidValidation, columns), 0.01, 0.01, new ComparisonCriteria(0.001, 1), default);
        Require(rapidAssessment.Accepted, "The short-delay fixture passes its independent validation.");
        var rapidStudy = new IdentificationStudy(Guid.NewGuid(), rapidTraining.Summary.Id, rapidValidation.Summary.Id,
            columns, columns, "%", "bar", 0.01, 0.01, DateTimeOffset.UtcNow, rapidVersion, rapidFit, Assessment: rapidAssessment);
        restored.SaveStudy(project.Id, rapidStudy);
        // Demonstration study with measurement noise (screens and captures only): a first-order process with a realistic time constant, noisy like a real transmitter.
        var noisyTraining = await SaveStepAsync(20, 2, delaySeconds: 10, samples: 700, tau: 60, noise: 0.03);
        var noisyValidation = await SaveStepAsync(21, -1.5, delaySeconds: 10, samples: 700, tau: 60, noise: 0.03);
        var (noisyFit, noisyVersion) = await engine.IdentifyAsync(ModelFamily.FOPDT,
            new StepImport(noisyTraining, columns), new StepImport(noisyValidation, columns), 0.01, 0.2, default);
        var (_, noisyAssessment, _) = await engine.AssessAsync(ModelFamily.FOPDT, new StepImport(noisyTraining, columns),
            new StepImport(noisyValidation, columns), 0.01, 0.2, new ComparisonCriteria(0.2, 0.8), default);
        var noisyComparison = await engine.CompareAsync(new StepImport(noisyTraining, columns), new StepImport(noisyValidation, columns), 0.01, 0.2, 0.2, 0.8, default);
        var noisyStudy = new IdentificationStudy(Guid.NewGuid(), noisyTraining.Summary.Id, noisyValidation.Summary.Id,
            columns, columns, "%", "bar", 0.01, 0.2, DateTimeOffset.UtcNow, noisyVersion, noisyFit, Assessment: noisyAssessment, Comparison: noisyComparison);
        restored.SaveStudy(project.Id, noisyStudy);
        Directory.CreateDirectory(fixture);
        {
            // Synthetic import with known faults, to check on screen that the diagnostic really detects them:
            // 3 missing values (rows 10-12), 2 non-numeric values (rows 20 and 45), a 6 s hole in the sampling (rows 30-35 absent).
            var faulty = new StringBuilder("timestamp;mv;pv\n");
            var origin = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 60; i++)
            {
                if (i is >= 30 and < 36) continue;
                var value = i is 10 or 11 or 12 ? "" : i is 20 or 45 ? "n/a" : (8 + 0.05 * i).ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
                faulty.Append(origin.AddSeconds(i).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)).Append(';').Append(i >= 5 ? 1 : 0).Append(';').Append(value).Append('\n');
            }
            var faultyRaw = Encoding.UTF8.GetBytes(faulty.ToString());
            var faultyMapping = new CsvMapping("timestamp", "pv", "PV", "bar");
            var (faultyResult, faultyVersion) = await engine.AnalyzeAsync(faultyRaw, ";", faultyMapping, default);
            Require(faultyResult.Quality is { MissingValues: 3, InvalidValues: 2 }, "The synthetic faulty import carries exactly the faults written into it.");
            var faultySummary = new ImportSummary(Guid.NewGuid(), "defauts-synthetiques.csv", Convert.ToHexString(SHA256.HashData(faultyRaw)), DateTimeOffset.UtcNow);
            restored.SaveImport(project.Id, new CsvImport(faultySummary, ";", faultyMapping, faultyResult, faultyVersion, faultyRaw));
        }
        File.WriteAllText(Path.Combine(fixture, "noisy-demo-study.txt"), noisyStudy.Id.ToString()[..8]);
        File.WriteAllText(Path.Combine(fixture, "short-delay-study.txt"), rapidStudy.Id.ToString()[..8]);
        // Fixtures for the block, integrator and drift windows (tests/windows/blocks.ps1).
        var integratorCriteria = new ComparisonCriteria(0.001, 1);
        var (integratorFit, integratorVersion) = await engine.IdentifyAsync(ModelFamily.Integrateur, new StepImport(trainingThird, columns),
            new StepImport(validationThird, columns), 0.01, 0.01, default);
        var (_, integratorAssessment, _) = await engine.AssessAsync(ModelFamily.Integrateur, new StepImport(trainingThird, columns),
            new StepImport(validationThird, columns), 0.01, 0.01, integratorCriteria, default);
        Require(integratorAssessment.Accepted, "The integrator fixture passes its independent validation.");
        var integratorStudy = new IdentificationStudy(Guid.NewGuid(), trainingThird.Summary.Id, validationThird.Summary.Id, columns, columns,
            "%", "bar", 0.01, 0.01, DateTimeOffset.UtcNow, integratorVersion, integratorFit, ModelFamily.Integrateur, Assessment: integratorAssessment);
        restored.SaveStudy(project.Id, integratorStudy);
        File.WriteAllText(Path.Combine(fixture, "integrator-study.txt"), integratorStudy.Id.ToString()[..8]);
        await SaveStepAsync(11, 1.8, delaySeconds: 0.5);      // a NEW step of the short-delay process: essai-11.csv
        const double transportDelay = 34 * 60;
        const int samplePeriod = 90;
        var transportTraining = await SaveStepAsync(9, 2, delaySeconds: transportDelay, secondsPerSample: samplePeriod);
        var transportValidation = await SaveStepAsync(10, -1.5, delaySeconds: transportDelay, secondsPerSample: samplePeriod);
        var (transportFit, transportVersion) = await engine.IdentifyAsync(ModelFamily.FOPDT,
            new StepImport(transportTraining, columns), new StepImport(transportValidation, columns), 0.01, 0.01, default);
        Require(Math.Abs(transportFit.DelaySeconds - transportDelay) < 0.01 &&
            transportFit.TauSeconds is { } transportTau && Math.Abs(transportTau - 4 * samplePeriod) < 0.01 &&
            Math.Abs(transportFit.Gain + 3) < 1e-5 && transportFit.Validation.Rmse < 1e-5 &&
            !transportFit.Warnings.Contains("delay_resolution"), "Long fractional-sample delay and holdout response.");
        var (_, transportAssessment, _) = await engine.AssessAsync(ModelFamily.FOPDT, new StepImport(transportTraining, columns),
            new StepImport(transportValidation, columns), 0.01, 0.01, new ComparisonCriteria(0.001, 1), default);
        Require(transportAssessment.Accepted, "The transport-delay fixture passes its independent validation.");
        var transportStudy = new IdentificationStudy(Guid.NewGuid(), transportTraining.Summary.Id, transportValidation.Summary.Id,
            columns, columns, "%", "bar", 0.01, 0.01, DateTimeOffset.UtcNow, transportVersion, transportFit, Assessment: transportAssessment);
        restored.SaveStudy(project.Id, transportStudy);
        File.WriteAllText(Path.Combine(fixture, "transport-study.txt"), transportStudy.Id.ToString()[..8]);
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath, Pooling = false }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(fixture, "projects.db"), Pooling = false
        }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
        File.WriteAllText(Path.Combine(fixture, "study.txt"), study.Id.ToString()[..8]);
    }
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

static async Task ThrowsAsync<T>(Func<Task> operation) where T : Exception
{
    try { await operation(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Throws<T>(Action operation) where T : Exception
{
    try { operation(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

internal sealed class FixedIdentity(UserIdentity identity) : IIdentityProvider
{
    public UserIdentity Current => identity;
}
