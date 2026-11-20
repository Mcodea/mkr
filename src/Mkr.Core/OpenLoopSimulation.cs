namespace Mkr.Core;

public sealed record OpenLoopScenario(double SampleSeconds, double DurationSeconds, double StepMv,
    double StepAtSeconds, double MinimumMv, double MaximumMv);

public sealed record OpenLoopPoint(double Seconds, double Pv, double? RequestedMv, double? AppliedMv);

public sealed record OpenLoopResult(string AlgorithmVersion, string[] Warnings, double SteadyPv,
    double SaturationSeconds, OpenLoopPoint[] Points);

public interface IOpenLoopEngine
{
    Task<(OpenLoopResult Result, string EngineVersion)> SimulateOpenLoopAsync(
        IdentificationResult model, OpenLoopScenario scenario, CancellationToken token);
}
