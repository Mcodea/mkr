using Mkr.Core;

namespace Mkr.Desktop;

internal sealed class FopdtWorkspace(Project project, IImportStore imports, IFopdtStore studies, IFopdtEngine engine)
{
    public IReadOnlyList<ImportSummary> Sources() => imports.ListImports(project.Id);
    public IReadOnlyList<FopdtStudy> Studies() => studies.ListFopdt(project.Id);
    public string[] Columns(ImportSummary source) => imports.ReadImport(project.Id, source.Id).Result.Headers;

    public async Task<FopdtStudy> CalculateAsync(ImportSummary training, StepColumns trainColumns,
        ImportSummary validation, StepColumns valColumns, string mvUnit, string pvUnit,
        double mvTolerance, double pvTolerance, CancellationToken token)
    {
        var train = new StepImport(imports.ReadImport(project.Id, training.Id), trainColumns);
        var check = new StepImport(imports.ReadImport(project.Id, validation.Id), valColumns);
        if (train.Import.Summary.Sha256 == check.Import.Summary.Sha256)
            throw new CsvInputException("fit_independence");
        CheckRecordedUnit(train, mvUnit, pvUnit);
        CheckRecordedUnit(check, mvUnit, pvUnit);
        var (result, version) = await engine.IdentifyAsync(train, check, mvTolerance, pvTolerance, token);
        token.ThrowIfCancellationRequested();
        var study = new FopdtStudy(Guid.NewGuid(), training.Id, validation.Id, trainColumns, valColumns,
            mvUnit, pvUnit, mvTolerance, pvTolerance, DateTimeOffset.UtcNow, version, result);
        studies.SaveFopdt(project.Id, study);
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
