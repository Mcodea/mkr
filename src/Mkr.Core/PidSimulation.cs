namespace Mkr.Core;

/// <summary>
/// Ideal/dependent PID coefficients for *experimental simulation only*.
/// The derivative acts on PV, with a backward-Euler first-order filter.
/// No manufacturer's PLC block parameters are implied by these values.
/// </summary>
public sealed record PidPrototypeCoefficients(double Kp, double TiSeconds,
    double TdSeconds, double FilterSeconds);

public sealed record PidPrototypePoint(double Seconds, double Pv, double? Cv,
    double? Mv, double? Disturbance, double? Derivative, double? MeasuredPv = null);

public sealed record PidPrototypeResult(string AlgorithmVersion, string ControllerForm,
    string[] Warnings, double Iae, double Ise, double FinalError,
    double SaturationSeconds, double RateLimitedSeconds, int FrozenSteps,
    PidPrototypePoint[] Points, SetpointResponse SetpointResponse);

public interface IPidPrototypeEngine
{
    Task<(PidPrototypeResult Result, string EngineVersion)> SimulatePidPrototypeAsync(
        IdentificationResult model, PidPrototypeCoefficients coefficients,
        PiScenario scenario, CancellationToken token);
}
