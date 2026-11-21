namespace Mkr.Core;

// Values in a range, constraint or setpoint use the unit of their signal.
public sealed record PhysicalRange(double Minimum, double Maximum);
public sealed record LoopSignal(string Name, string Unit, PhysicalRange Range);
public sealed record SignalConstraint(PhysicalRange AllowedRange, double? MaximumRatePerSecond = null);
public sealed record LoopEquipment(string Name, string Description);
public sealed record LoopDisturbance(LoopSignal Signal, string Description);
public sealed record PhysicalLoop(
    LoopSignal ControlledVariable,
    LoopSignal ManipulatedVariable,
    double Setpoint,
    LoopEquipment Process,
    LoopEquipment Instrument,
    LoopEquipment Actuator,
    SignalConstraint ControlledConstraint,
    SignalConstraint ManipulatedConstraint,
    IReadOnlyList<LoopDisturbance> Disturbances)
{
    public SensorProfile? SensorProfile { get; init; }
    public ActuatorProfile? ActuatorProfile { get; init; }
}

/// <summary>Checks a description before use; constructing a DTO does not validate it.</summary>
public static class PhysicalLoopValidator
{
    public static IReadOnlyList<string> Validate(PhysicalLoop? loop)
    {
        var errors = new List<string>();
        if (loop is null) { errors.Add("Loop is required."); return errors; }

        CheckSignal(loop.ControlledVariable, "ControlledVariable", errors);
        CheckSignal(loop.ManipulatedVariable, "ManipulatedVariable", errors);
        CheckEquipment(loop.Process, "Process", errors);
        CheckEquipment(loop.Instrument, "Instrument", errors);
        CheckEquipment(loop.Actuator, "Actuator", errors);
        CheckConstraint(loop.ControlledConstraint, loop.ControlledVariable, "ControlledConstraint", errors);
        CheckConstraint(loop.ManipulatedConstraint, loop.ManipulatedVariable, "ManipulatedConstraint", errors);
        EquipmentProfileValidator.Validate(loop.SensorProfile, loop.ActuatorProfile, errors);

        if (!double.IsFinite(loop.Setpoint)) errors.Add("Setpoint must be finite.");
        else if (loop.ControlledVariable is not null && ValidRange(loop.ControlledVariable.Range) &&
                 (loop.Setpoint < loop.ControlledVariable.Range.Minimum || loop.Setpoint > loop.ControlledVariable.Range.Maximum))
            errors.Add("Setpoint must be inside the controlled signal range.");
        if (double.IsFinite(loop.Setpoint) && loop.ControlledConstraint is not null && ValidRange(loop.ControlledConstraint.AllowedRange) &&
            (loop.Setpoint < loop.ControlledConstraint.AllowedRange.Minimum || loop.Setpoint > loop.ControlledConstraint.AllowedRange.Maximum))
            errors.Add("Setpoint must be inside the controlled constraint.");

        if (loop.Disturbances is null) errors.Add("Disturbances is required; use an empty list if there are none.");
        else for (var i = 0; i < loop.Disturbances.Count; i++)
        {
            var disturbance = loop.Disturbances[i];
            if (disturbance is null) { errors.Add($"Disturbances[{i}] is required."); continue; }
            CheckSignal(disturbance.Signal, $"Disturbances[{i}].Signal", errors);
            CheckText(disturbance.Description, $"Disturbances[{i}].Description", errors);
        }
        return errors;
    }

    private static void CheckText(string? value, string path, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{path} is required.");
    }

    private static bool ValidRange(PhysicalRange? range) => range is not null &&
        double.IsFinite(range.Minimum) && double.IsFinite(range.Maximum) && range.Minimum < range.Maximum;

    private static void CheckRange(PhysicalRange? range, string path, List<string> errors)
    {
        if (!ValidRange(range)) errors.Add($"{path} requires finite bounds with minimum less than maximum.");
    }

    private static void CheckSignal(LoopSignal? signal, string path, List<string> errors)
    {
        if (signal is null) { errors.Add($"{path} is required."); return; }
        CheckText(signal.Name, path + ".Name", errors);
        CheckText(signal.Unit, path + ".Unit", errors);
        CheckRange(signal.Range, path + ".Range", errors);
    }

    private static void CheckEquipment(LoopEquipment? equipment, string path, List<string> errors)
    {
        if (equipment is null) { errors.Add($"{path} is required."); return; }
        CheckText(equipment.Name, path + ".Name", errors);
        CheckText(equipment.Description, path + ".Description", errors);
    }

    private static void CheckConstraint(SignalConstraint? constraint, LoopSignal? signal, string path, List<string> errors)
    {
        if (constraint is null) { errors.Add($"{path} is required."); return; }
        CheckRange(constraint.AllowedRange, path + ".AllowedRange", errors);
        if (constraint.MaximumRatePerSecond is double rate && (!double.IsFinite(rate) || rate <= 0))
            errors.Add($"{path}.MaximumRatePerSecond must be finite and positive.");
        if (signal is not null && ValidRange(constraint.AllowedRange) && ValidRange(signal.Range) &&
            (constraint.AllowedRange.Minimum < signal.Range.Minimum || constraint.AllowedRange.Maximum > signal.Range.Maximum))
            errors.Add($"{path}.AllowedRange must be inside the signal range.");
    }
}
