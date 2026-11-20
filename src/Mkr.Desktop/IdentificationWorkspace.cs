using Mkr.Core;

namespace Mkr.Desktop;

internal sealed class IdentificationWorkspace(Project project, IImportStore imports, IIdentificationStore studies,
    IIdentificationEngine engine, IPiStore piStore, IPiEngine piEngine)
{
    public IReadOnlyList<ImportSummary> Sources() => imports.ListImports(project.Id);
    public IReadOnlyList<IdentificationStudy> Studies() => studies.ListStudies(project.Id);
    public string[] Columns(ImportSummary source) => imports.ReadImport(project.Id, source.Id).Result.Headers;

    public async Task<IdentificationStudy> CalculateAsync(ModelFamily family, ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, CancellationToken token)
    {
        var train = new StepImport(imports.ReadImport(project.Id, training.Id), trainColumns);
        var check = new StepImport(imports.ReadImport(project.Id, validation.Id), valColumns);
        if (train.Import.Summary.Sha256 == check.Import.Summary.Sha256)
            throw new CsvInputException("fit_independence");
        CheckRecordedUnit(train, mvUnit, pvUnit);
        CheckRecordedUnit(check, mvUnit, pvUnit);
        var (result, version) = await engine.IdentifyAsync(family, train, check, mvTolerance, pvTolerance, token);
        token.ThrowIfCancellationRequested();
        var study = new IdentificationStudy(Guid.NewGuid(), training.Id, validation.Id, trainColumns, valColumns,
            mvUnit, pvUnit, mvTolerance, pvTolerance, DateTimeOffset.UtcNow, version, result, family);
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

    private static void CheckRecordedUnit(StepImport source, string mvUnit, string pvUnit)
    {
        var recorded = source.Import.Mapping;
        if ((source.Columns.MvColumn == recorded.ValueColumn && (recorded.Role != "MV" || recorded.Unit != mvUnit)) ||
            (source.Columns.PvColumn == recorded.ValueColumn && (recorded.Role != "PV" || recorded.Unit != pvUnit)))
            throw new CsvInputException("fit_recorded_mapping");
    }
}
