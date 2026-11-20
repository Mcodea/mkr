namespace Mkr.Core;

public sealed record PiScenario(double SampleSeconds, double DurationSeconds, double Setpoint, double MinimumMv,
    double MaximumMv, double Disturbance, double DisturbanceAtSeconds, double? MaximumMvRate = null);

public sealed record SimulationPoint(double Seconds, double Pv, double? Cv, double? Mv, double? Disturbance);

public sealed record SetpointResponse(double WindowEndSeconds, double? OvershootPv, double? SettlingSeconds);

public sealed record SimulationRun(double Iae, double Ise, double SaturationSeconds, int FrozenSteps,
    double FinalError, SimulationPoint[] Points, double RateLimitedSeconds = 0, SetpointResponse? SetpointResponse = null);

public sealed record PiSimulationResult(string AlgorithmVersion, string[] Warnings, SimulationRun Imc, SimulationRun Simc);

// Local one-factor sensitivity; not a statistical confidence interval or stability certificate.
public sealed record PiSensitivityMetrics(double Iae, double Ise, double FinalError,
    double SaturationSeconds, double RateLimitedSeconds, int FrozenSteps,
    double? OvershootPv, double? SettlingSeconds);

public sealed record PiSensitivityCase(string Key, double Gain, double TauSeconds,
    double DelaySeconds, PiSensitivityMetrics Imc, PiSensitivityMetrics Simc, string[] Warnings);

public sealed record PiRobustnessResult(string AlgorithmVersion, double VariationPercent,
    PiSensitivityCase[] Cases, string[] Warnings);

public sealed record PiSimulationStudy(Guid Id, Guid PiId, DateTimeOffset CalculatedAt, string EngineVersion,
    PiScenario Scenario, PiSimulationResult Result, PiRobustnessResult? Robustness = null)
{
    public string Label => $"{CalculatedAt.ToLocalTime():g} · {Id.ToString()[..8]}";
}
