namespace Mkr.Core;

// Null means unknown, including for zero-capable characteristics.
public sealed record SensorProfile(
    double? Resolution = null,
    double? SamplingFrequencyHz = null,
    double? NoiseStandardDeviation = null,
    double? DelaySeconds = null);

public sealed record ActuatorProfile(
    PhysicalRange? Saturation = null,
    double? MaximumRatePerSecond = null,
    double? Deadband = null,
    double? Hysteresis = null);

public static class EquipmentProfileValidator
{
    public static IReadOnlyList<string> Validate(SensorProfile? sensor, ActuatorProfile? actuator)
    {
        var errors = new List<string>();
        Validate(sensor, actuator, errors);
        return errors;
    }

    internal static void Validate(SensorProfile? sensor, ActuatorProfile? actuator, List<string> errors)
    {
        if (sensor is not null)
        {
            Check(sensor.Resolution, true, "SensorProfile.Resolution", errors);
            Check(sensor.SamplingFrequencyHz, true, "SensorProfile.SamplingFrequencyHz", errors);
            Check(sensor.NoiseStandardDeviation, false, "SensorProfile.NoiseStandardDeviation", errors);
            Check(sensor.DelaySeconds, false, "SensorProfile.DelaySeconds", errors);
        }
        if (actuator is not null)
        {
            Check(actuator.MaximumRatePerSecond, true, "ActuatorProfile.MaximumRatePerSecond", errors);
            Check(actuator.Deadband, false, "ActuatorProfile.Deadband", errors);
            Check(actuator.Hysteresis, false, "ActuatorProfile.Hysteresis", errors);
            var range = actuator.Saturation;
            if (range is not null && (!double.IsFinite(range.Minimum) || !double.IsFinite(range.Maximum) || range.Minimum >= range.Maximum))
                errors.Add("ActuatorProfile.Saturation requires finite, increasing bounds.");
        }
    }

    private static void Check(double? value, bool positive, string path, List<string> errors)
    {
        if (value is double number && (!double.IsFinite(number) || (positive ? number <= 0 : number < 0)))
            errors.Add($"{path} must be finite and {(positive ? "positive" : "nonnegative")} when known.");
    }
}
