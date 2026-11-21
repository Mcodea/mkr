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
            training.Summary, validation.Summary, reviews, training.Acquisition, validation.Acquisition);
    }

    public IReadOnlyList<ImportSummary> Sources() => imports.ListImports(project.Id);
    public IReadOnlyList<IdentificationStudy> Studies() => studies.ListStudies(project.Id);
    public string[] Columns(ImportSummary source) => imports.ReadImport(project.Id, source.Id).Result.Headers;

    public Task<TrialPreview> PreviewTrialAsync(ImportSummary source, StepColumns columns,
        CancellationToken token)
    {
        var original = imports.ReadImport(project.Id, source.Id);
        if (original.Summary.Sha256 != source.Sha256)
            throw new CsvInputException("preview_provenance");
        return engine.PreviewTrialAsync(new StepImport(original, columns), token);
    }

    public async Task<IdentificationStudy> CalculateAsync(ModelFamily family, ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, CancellationToken token,
        StepWindow? trainingWindow = null, StepWindow? validationWindow = null)
    {
        var train = new StepImport(imports.ReadImport(project.Id, training.Id), trainColumns, trainingWindow);
        var check = new StepImport(imports.ReadImport(project.Id, validation.Id), valColumns, validationWindow);
        if (train.Import.Summary.Sha256 == check.Import.Summary.Sha256)
            throw new CsvInputException("fit_independence");
        CheckRecordedUnit(train, mvUnit, pvUnit);
        CheckRecordedUnit(check, mvUnit, pvUnit);
        var (result, version) = await engine.IdentifyAsync(family, train, check, mvTolerance, pvTolerance, token);
        token.ThrowIfCancellationRequested();
        var study = new IdentificationStudy(Guid.NewGuid(), training.Id, validation.Id, trainColumns, valColumns,
            mvUnit, pvUnit, mvTolerance, pvTolerance, DateTimeOffset.UtcNow, version, result,
            family, trainingWindow, validationWindow);
        studies.SaveStudy(project.Id, study);
        return study;
    }

    public IReadOnlyList<PiStudy> PiStudies(Guid modelId) => piStore.ListPi(project.Id, modelId);

    public async Task<PiStudy> CalculatePiAsync(IdentificationStudy model, double responseSeconds, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || model.Result.TauSeconds is not { } tau)
            throw new InvalidOperationException("Ce calcul PI exige une étude FOPDT.");
        var (result, version) = await piEngine.CalculatePiAsync(model.Result.Gain, tau,
            model.Result.DelaySeconds, responseSeconds, token);
        token.ThrowIfCancellationRequested();
        var study = new PiStudy(Guid.NewGuid(), model.Id, DateTimeOffset.UtcNow, version, result);
        piStore.SavePi(project.Id, study);
        return study;
    }

    public IReadOnlyList<PiSimulationStudy> Simulations(Guid piId) => piStore.ListSimulations(project.Id, piId);

    public async Task<PiSimulationStudy> SimulateAsync(IdentificationStudy model, PiStudy tuning,
        PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La comparaison PI doit référencer le modèle FOPDT sélectionné.");
        var (result, version) = await piEngine.SimulatePiAsync(model.Result, tuning.Result, scenario, token);
        token.ThrowIfCancellationRequested();
        var study = new PiSimulationStudy(Guid.NewGuid(), tuning.Id, DateTimeOffset.UtcNow, version, scenario, result);
        piStore.SaveSimulation(project.Id, study);
        return study;
    }

    public async Task<PiSimulationStudy> AnalyzeCombinedAsync(IdentificationStudy model,
        PiStudy tuning, PiScenario scenario, double variationPercent, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La comparaison combinée exige le modèle FOPDT associé au réglage PI.");
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
        return study;
    }

    public async Task<PiSimulationStudy> AnalyzeRobustnessAsync(IdentificationStudy model,
        PiStudy tuning, PiScenario scenario, double variationPercent, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("L'analyse de robustesse exige le modèle FOPDT associé au réglage PI.");
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
        return study;
    }

    public Task<(PiSimulationResult Result, string EngineVersion)> PreviewPiAsync(
        IdentificationStudy model, PiStudy tuning, PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id)
            throw new InvalidOperationException("La référence PI doit provenir du même modèle FOPDT.");
        return piEngine.SimulatePiAsync(model.Result, tuning.Result, scenario, token);
    }

    public Task<(PidPrototypeResult Result, string EngineVersion)> SimulatePidPrototypeAsync(
        IdentificationStudy model, PiStudy tuning, PidPrototypeCoefficients controller,
        PiScenario scenario, CancellationToken token)
    {
        if (model.Family != ModelFamily.FOPDT || tuning.ModelId != model.Id ||
            model.Result.TauSeconds is not > 0)
            throw new InvalidOperationException("L'essai PID exige un modèle FOPDT et le réglage PI associés.");
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
            (source.Columns.MvColumn != "MV" || source.Columns.PvColumn != "PV" ||
             mvUnit != acquisition.Source.CommandUnit || pvUnit != acquisition.Source.ProcessUnit))
            throw new CsvInputException("fit_recorded_mapping");
        var recorded = source.Import.Mapping;
        if ((source.Columns.MvColumn == recorded.ValueColumn && (recorded.Role != "MV" || recorded.Unit != mvUnit)) ||
            (source.Columns.PvColumn == recorded.ValueColumn && (recorded.Role != "PV" || recorded.Unit != pvUnit)))
            throw new CsvInputException("fit_recorded_mapping");
    }
}
