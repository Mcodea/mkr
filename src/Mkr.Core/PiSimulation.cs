namespace Mkr.Core;

public sealed record PiScenario(double SampleSeconds, double DurationSeconds, double Setpoint, double MinimumMv,
    double MaximumMv, double Disturbance, double DisturbanceAtSeconds);

public sealed record SimulationPoint(double Seconds, double Pv, double? Cv, double? Mv, double? Disturbance);

public sealed record SimulationRun(double Iae, double Ise, double SaturationSeconds, int FrozenSteps,
    double FinalError, SimulationPoint[] Points);

public sealed record PiSimulationResult(string AlgorithmVersion, string[] Warnings, SimulationRun Imc, SimulationRun Simc);

public sealed record PiSimulationStudy(Guid Id, Guid PiId, DateTimeOffset CalculatedAt, string EngineVersion,
    PiScenario Scenario, PiSimulationResult Result)
{
    public string Label => $"{CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}";
}
