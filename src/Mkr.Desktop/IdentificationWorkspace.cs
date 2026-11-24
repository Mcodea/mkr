using System.IO;
using Mkr.Core;

namespace Mkr.Desktop;

internal sealed class IdentificationWorkspace(Project project, IImportStore imports, IIdentificationStore studies,
    IIdentificationEngine engine, IPiStore piStore, IPiEngine piEngine, IOpenLoopEngine openLoopEngine,
    IPidPrototypeEngine pidEngine)
{
    public void ExportSheet(string path, IdentificationStudy model, PiStudy tuning, PiSimulationStudy simulation)
    {
        var training = imports.ReadImport(project.Id, model.TrainingId);
        var validation = imports.ReadImport(project.Id, model.ValidationId);
        var reviewStore = studies as Mkr.Infrastructure.SqliteProjectStore
            ?? throw new InvalidOperationException("Historique des validations indisponible.");
        var reviews = new[] { model.Id, tuning.Id, simulation.Id }
            .SelectMany(id => reviewStore.ReadReviewHistory(project.Id, id)).OrderBy(r => r.RecordedAt).ToArray();
        Mkr.Infrastructure.StudySheet.Export(path, project.Name, model, tuning, simulation,
            training.Summary, validation.Summary, reviews, training.Acquisition, validation.Acquisition,
            imports.ReadLoopBrief(project.Id));
        Audit("study.sheet.exported", Short(model.Id), $"fiche PDF {Path.GetFileName(path)} ; réglage {Short(tuning.Id)} ; simulation {Short(simulation.Id)}");
    }

    /// <summary>
    /// Traces a sensitive action in the project's append-only audit log. A failure to trace is NOT swallowed: the caller must know that
    /// an action could not be recorded. Unavailable only for stores that are not the SQLite project store (tests of other layers).
    /// </summary>
    private void Audit(string action, string subject, string detail)
    {
        if (studies is IAuditLog log) log.Record(project.Id, Environment.UserName, action, subject, detail);
    }

    private async Task<T> Traced<T>(Task<T> task, string action, string subject, Func<T, string> detail)
    {
        var result = await task.ConfigureAwait(false);
        Audit(action, subject, detail(result));
        return result;
    }

    private static string Short(Guid id) => id.ToString()[..8];

    public IReadOnlyList<ImportSummary> Sources() => imports.ListImports(project.Id);
    public IReadOnlyList<IdentificationStudy> Studies() => studies.ListStudies(project.Id);
    public CsvImport ReadSource(ImportSummary source) => imports.ReadImport(project.Id, source.Id);

    public Task<TrialPreview> PreviewTrialAsync(ImportSummary source, StepColumns columns,
        CancellationToken token)
    {
        var original = imports.ReadImport(project.Id, source.Id);
        if (original.Summary.Sha256 != source.Sha256)
            throw new CsvInputException("preview_provenance");
        return engine.PreviewTrialAsync(new StepImport(original, columns), token);
    }

    private (StepImport, StepImport) ReadTrials(ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        StepWindow? trainingWindow, StepWindow? validationWindow)
    {
        var train = new StepImport(imports.ReadImport(project.Id, training.Id), trainColumns, trainingWindow);
        var check = new StepImport(imports.ReadImport(project.Id, validation.Id), valColumns, validationWindow);
        if (TrialIndependence.Check(train.Import.Summary, trainingWindow, check.Import.Summary, validationWindow) is { } refusal)
            throw new CsvInputException(refusal);
        CheckRecordedUnit(train, mvUnit, pvUnit);
        CheckRecordedUnit(check, mvUnit, pvUnit);
        return (train, check);
    }

    /// <summary>Lists the open-loop steps of one stored recording that the estimators would accept.</summary>
    public Task<StepSearch> FindStepsAsync(ImportSummary source, StepColumns columns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, CancellationToken token)
    {
        var recording = new StepImport(imports.ReadImport(project.Id, source.Id), columns);
        CheckRecordedUnit(recording, mvUnit, pvUnit);
        return engine.FindStepsAsync(recording, mvTolerance, pvTolerance, token);
    }

    /// <summary>
    /// Confronts an ACCEPTED FOPDT model with a new open-loop step. The trial must be independent of both trials that built the model
    /// (another recording, or disjoint windows of the same one), and the model's own acceptance criteria are used.
    /// </summary>
    public Task<ModelDriftReport> CheckModelDriftAsync(IdentificationStudy model, ImportSummary trial, StepColumns columns,
        string mvUnit, string pvUnit, double mvTolerance, double pvTolerance, StepWindow? window, CancellationToken token)
    {
        if (model.Family is not (ModelFamily.FOPDT or ModelFamily.SOPDT or ModelFamily.Integrateur))
            throw new InvalidOperationException("Le contrôle de dérive couvre les modèles FOPDT, SOPDT et intégrateur.");
        if (!model.IsAcceptedForTuning || model.Assessment is not { } assessment)
            throw new ModelNotAcceptedException("Seul un modèle accepté à la validation peut être suivi.");
        var recorded = imports.ReadImport(project.Id, trial.Id);
        if (recorded.Summary.Sha256 != trial.Sha256) throw new CsvInputException("preview_provenance");
        foreach (var (id, previousWindow) in new[] { (model.TrainingId, model.TrainingWindow), (model.ValidationId, model.ValidationWindow) })
        {
            var previous = imports.ReadImport(project.Id, id).Summary;
            if (TrialIndependence.Check(previous, previousWindow, recorded.Summary, window) is { } refusal)
                throw new CsvInputException(refusal);
        }
        var step = new StepImport(recorded, columns, window);
        CheckRecordedUnit(step, mvUnit, pvUnit);
        return Traced(engine.CheckModelDriftAsync(model.Family, model.Result, step, mvTolerance, pvTolerance, assessment.Criteria, token),
            "model.drift.checked", Short(model.Id),
            r => $"{model.Family} sur {trial.FileName} : {r.Verdict}" + (r.DriftedParameters.Length > 0 ? " (" + string.Join(", ", r.DriftedParameters) + ")" : ""));
    }

    public Task<CriteriaSuggestion> SuggestCriteriaAsync(ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, CancellationToken token, StepWindow? trainingWindow, StepWindow? validationWindow)
    {
        var (train, check) = ReadTrials(training, trainColumns, validation, valColumns, mvUnit, pvUnit, trainingWindow, validationWindow);
        return engine.SuggestCriteriaAsync(train, check, mvTolerance, pvTolerance, token);
    }

    public Task<ModelComparison> CompareAsync(ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, double maximumRmse, double maximumLag,
        CancellationToken token, StepWindow? trainingWindow, StepWindow? validationWindow)
    {
        var (train, check) = ReadTrials(training, trainColumns, validation, valColumns, mvUnit, pvUnit, trainingWindow, validationWindow);
        return engine.CompareAsync(train, check, mvTolerance, pvTolerance, maximumRmse, maximumLag, token);
    }

    public async Task<IdentificationStudy> CalculateAsync(ModelFamily family, ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, ComparisonCriteria criteria, CancellationToken token,
        StepWindow? trainingWindow = null, StepWindow? validationWindow = null, ModelComparison? comparison = null)
    {
        if (comparison is not null && (comparison.Selected != family.ToString() || comparison.Criteria != criteria))
            throw new InvalidOperationException("La famille ou les critères à enregistrer diffèrent de ceux de la comparaison.");
        var (train, check) = ReadTrials(training, trainColumns, validation, valColumns, mvUnit, pvUnit, trainingWindow, validationWindow);
        // Every study carries the validation decision; a refused study is kept for traceability but cannot be tuned.
        var (result, assessment, version) = await engine.AssessAsync(family, train, check, mvTolerance, pvTolerance, criteria, token);
        token.ThrowIfCancellationRequested();
        var study = new IdentificationStudy(Guid.NewGuid(), training.Id, validation.Id, trainColumns, valColumns,
            mvUnit, pvUnit, mvTolerance, pvTolerance, DateTimeOffset.UtcNow, version, result,
            family, trainingWindow, validationWindow, comparison, assessment);
        studies.SaveStudy(project.Id, study);
        Audit("identification.saved", Short(study.Id),
            $"{family} sur {training.FileName} / {validation.FileName} : " + (assessment.Accepted ? "accepté" : "REFUSÉ (" + string.Join(", ", assessment.Reasons) + ")"));
        return study;
    }

    public IReadOnlyList<PiStudy> PiStudies(Guid modelId) => piStore.ListPi(project.Id, modelId);

    /// <summary>PI for an accepted integrator model: the model gain is the slope in PV units per second per MV unit.</summary>
    public Task<IntegratorPi> CalculateIntegratorPiAsync(IdentificationStudy model, double responseSeconds, CancellationToken token)
    {
        if (model.Family != ModelFamily.Integrateur)
            throw new InvalidOperationException("Sélectionnez une étude d'intégrateur.");
        model.RequireAcceptedForTuning();
        return Traced(piEngine.CalculateIntegratorPiAsync(model.Result.Gain, model.Result.DelaySeconds, responseSeconds, token),
            "integrator.pi.calculated", Short(model.Id), r => $"τc = {responseSeconds:G6} s : Kp = {r.Kp:G6}, Ti = {r.TiSeconds:G6} s (théorique)");
    }

    /// <summary>Documentary PIDFF values of an integrator PI (PI only, no derivative, no filter). Never qualified.</summary>
    public Task<BlockConversionResult> ConvertIntegratorToPidffAsync(IdentificationStudy model, IntegratorPi tuning, CancellationToken token)
    {
        if (model.Family != ModelFamily.Integrateur) throw new InvalidOperationException("Sélectionnez une étude d'intégrateur.");
        model.RequireAcceptedForTuning();
        return Traced(piEngine.ConvertToPidffAsync(tuning.Kp, tuning.TiSeconds, 0, null, token),
            "block.values.shown", "PIDFF", r => $"intégrateur {Short(model.Id)} : " + (r.Converted ? "valeurs affichées, NON QUALIFIÉES" : "refusé " + r.Reason));
    }

    public Task<SopdtPid> CalculateSopdtPidAsync(IdentificationStudy model, double responseSeconds, CancellationToken token)
    {
        if (model.Family != ModelFamily.SOPDT)
            throw new InvalidOperationException("Sélectionnez une étude du second ordre.");
        model.RequireAcceptedForTuning();
        return Traced(piEngine.CalculateSopdtPidAsync(model.Result, responseSeconds, token),
            "sopdt.pid.calculated", Short(model.Id), r => $"τc = {responseSeconds:G6} s : Kp = {r.Ideal.Kp:G6} (théorique)");
    }

    /// <summary>Documentary PIDFF values for one method of a saved PI tuning; only for a model accepted at validation.</summary>
    public Task<BlockConversionResult> ConvertToPidffAsync(IdentificationStudy model, PiStudy tuning, bool simc, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La conversion exige le réglage PI du modèle FOPDT sélectionné.");
        model.RequireAcceptedForTuning();
        var coefficients = simc ? tuning.Result.Simc : tuning.Result.Imc;
        return Traced(piEngine.ConvertToPidffAsync(coefficients.Kp, coefficients.TiSeconds, 0, null, token),
            "block.values.shown", "PIDFF", r => $"{(simc ? "SIMC" : "IMC")} {Short(tuning.Id)} : " + (r.Converted ? "valeurs affichées, NON QUALIFIÉES" : "refusé " + r.Reason));
    }

    /// <summary>Documentary PID_INT / PID_Compact values for a saved PI tuning (PI only); the caller supplies every scaling value.</summary>
    public Task<BlockSettingsResult> ConvertToBlockAsync(IdentificationStudy model, PiStudy tuning, bool simc, string block,
        IReadOnlyDictionary<string, object> installation, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La conversion exige le réglage PI du modèle FOPDT sélectionné.");
        model.RequireAcceptedForTuning();
        var coefficients = simc ? tuning.Result.Simc : tuning.Result.Imc;
        var parameters = new Dictionary<string, object>(installation)
        {
            ["kp"] = coefficients.Kp, ["tiSeconds"] = coefficients.TiSeconds, ["tdSeconds"] = 0.0
        };
        return Traced(piEngine.ConvertToBlockAsync(block, parameters, token),
            "block.values.shown", block, r => $"{(simc ? "SIMC" : "IMC")} {Short(tuning.Id)} : " + (r.Converted ? "valeurs affichées, NON QUALIFIÉES" : "refusé " + r.Reason));
    }

    public async Task<PiStudy> CalculatePiAsync(IdentificationStudy model, double responseSeconds, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || model.Result.TauSeconds is not { } tau)
            throw new InvalidOperationException("Ce calcul PI exige une étude FOPDT.");
        model.RequireAcceptedForTuning();
        var (result, version) = await piEngine.CalculatePiAsync(model.Result.Gain, tau,
            model.Result.DelaySeconds, responseSeconds, token);
        token.ThrowIfCancellationRequested();
        var study = new PiStudy(Guid.NewGuid(), model.Id, DateTimeOffset.UtcNow, version, result);
        piStore.SavePi(project.Id, study);
        Audit("pi.saved", Short(study.Id), $"réglage PI théorique du modèle {Short(model.Id)}, τc = {responseSeconds:G6} s");
        return study;
    }

    public IReadOnlyList<PiSimulationStudy> Simulations(Guid piId) => piStore.ListSimulations(project.Id, piId);

    public async Task<PiSimulationStudy> SimulateAsync(IdentificationStudy model, PiStudy tuning,
        PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La comparaison PI doit référencer le modèle FOPDT sélectionné.");
        model.RequireAcceptedForTuning();
        var (result, version) = await piEngine.SimulatePiAsync(model.Result, tuning.Result, scenario, token);
        token.ThrowIfCancellationRequested();
        var study = new PiSimulationStudy(Guid.NewGuid(), tuning.Id, DateTimeOffset.UtcNow, version, scenario, result);
        piStore.SaveSimulation(project.Id, study);
        Audit("pi.simulation.saved", Short(study.Id), $"simulation ou analyse du réglage {Short(tuning.Id)}");
        return study;
    }

    public async Task<PiSimulationStudy> AnalyzeCombinedAsync(IdentificationStudy model,
        PiStudy tuning, PiScenario scenario, double variationPercent, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La comparaison combinée exige le modèle FOPDT associé au réglage PI.");
        model.RequireAcceptedForTuning();
        var (nominal, nominalVersion) = await piEngine.SimulatePiAsync(
            model.Result, tuning.Result, scenario, token);
        token.ThrowIfCancellationRequested();
        var (combined, combinedVersion) = await piEngine.AnalyzeCombinedAsync(
            model.Result, tuning.Result, scenario, variationPercent, token);
        token.ThrowIfCancellationRequested();
        if (nominalVersion != combinedVersion)
            throw new InvalidDataException("Versions moteur différentes pour le nominal et la matrice.");
        var study = new PiSimulationStudy(Guid.NewGuid(), tuning.Id, DateTimeOffset.UtcNow,
            nominalVersion, scenario, nominal, combined);
        piStore.SaveSimulation(project.Id, study);
        Audit("pi.simulation.saved", Short(study.Id), $"simulation ou analyse du réglage {Short(tuning.Id)}");
        return study;
    }

    public async Task<PiSimulationStudy> AnalyzeRobustnessAsync(IdentificationStudy model,
        PiStudy tuning, PiScenario scenario, double variationPercent, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("L'analyse de robustesse exige le modèle FOPDT associé au réglage PI.");
        model.RequireAcceptedForTuning();
        // Nominal simulation and sensitivity are part of one saved study:
        // no partial SQLite write if either calculation fails.
        var (nominal, nominalVersion) = await piEngine.SimulatePiAsync(
            model.Result, tuning.Result, scenario, token);
        token.ThrowIfCancellationRequested();
        var (sensitivity, sensitivityVersion) = await piEngine.AnalyzeRobustnessAsync(
            model.Result, tuning.Result, scenario, variationPercent, token);
        token.ThrowIfCancellationRequested();
        if (nominalVersion != sensitivityVersion)
            throw new InvalidDataException("Versions moteur différentes pour la simulation et la robustesse.");
        var study = new PiSimulationStudy(Guid.NewGuid(), tuning.Id, DateTimeOffset.UtcNow,
            nominalVersion, scenario, nominal, sensitivity);
        piStore.SaveSimulation(project.Id, study);
        Audit("pi.simulation.saved", Short(study.Id), $"simulation ou analyse du réglage {Short(tuning.Id)}");
        return study;
    }

    public Task<PiTransferResult> TransferAsync(IdentificationStudy model, PiStudy tuning, bool simc,
        PiScenario scenario, double manual, double at, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || model.Id != tuning.ModelId)
            throw new InvalidOperationException("Le transfert PI exige le modèle FOPDT associé au réglage.");
        model.RequireAcceptedForTuning();
        return piEngine.SimulateTransferAsync(model.Result, simc ? tuning.Result.Simc : tuning.Result.Imc,
            scenario, manual, at, token);
    }

    public Task<(PiSimulationResult Result, string EngineVersion)> PreviewPiAsync(
        IdentificationStudy model, PiStudy tuning, PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La référence PI doit provenir du même modèle FOPDT.");
        model.RequireAcceptedForTuning();
        return piEngine.SimulatePiAsync(model.Result, tuning.Result, scenario, token);
    }

    public Task<(PidPrototypeResult Result, string EngineVersion)> SimulatePidPrototypeAsync(
        IdentificationStudy model, PiStudy tuning, PidPrototypeCoefficients controller,
        PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id ||
            model.Result.TauSeconds is not > 0)
            throw new InvalidOperationException("L'essai PID exige un modèle FOPDT et le réglage PI associés.");
        model.RequireAcceptedForTuning();
        // Explicitly offline, non-persistent prototype. The source model and tune are immutable.
        return pidEngine.SimulatePidPrototypeAsync(model.Result, controller, scenario, token);
    }

    public Task<(OpenLoopResult Result, string EngineVersion)> SimulateOpenLoopAsync(
        IdentificationStudy model, OpenLoopScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT)
            throw new InvalidOperationException("La simulation en boucle ouverte exige une étude FOPDT.");
        return openLoopEngine.SimulateOpenLoopAsync(model.Result, scenario, token);
    }

    private static void CheckRecordedUnit(StepImport source, string mvUnit, string pvUnit)
    {
        if (source.Import.Acquisition is { } acquisition &&
            (source.Columns.TimeColumn != source.Import.Mapping.TimeColumn || source.Columns.MvColumn != "MV" || source.Columns.PvColumn != "PV" ||
             mvUnit != acquisition.CommandUnit || pvUnit != acquisition.ProcessUnit))
            throw new CsvInputException("fit_recorded_mapping");
        var recorded = source.Import.Mapping;
        if ((source.Columns.MvColumn == recorded.ValueColumn && (recorded.Role != "MV" || recorded.Unit != mvUnit)) ||
            (source.Columns.PvColumn == recorded.ValueColumn && (recorded.Role != "PV" || recorded.Unit != pvUnit)))
            throw new CsvInputException("fit_recorded_mapping");
    }
}
