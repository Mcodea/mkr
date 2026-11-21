using Mkr.Core;

var equipment = new LoopEquipment("E1", "Synthetic equipment");
var loop = new PhysicalLoop(new("Temperature", "°C", new(-20, 150)),
    new("Valve", "%", new(0, 100)), 50, equipment, equipment, equipment,
    new(new(0, 100)), new(new(10, 90), 5),
    new[] { new LoopDisturbance(new("Flow", "m³/h", new(0, 20)), "Feed") });

void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Reject(PhysicalLoop candidate, string path) =>
    Expect(PhysicalLoopValidator.Validate(candidate).Any(e => e.Contains(path)), $"Expected rejection: {path}");

Expect(PhysicalLoopValidator.Validate(loop).Count == 0, "Valid loop rejected");
Expect(PhysicalLoopValidator.Validate(null).Count != 0, "Null loop accepted");
Expect(PhysicalLoopValidator.Validate(loop with { Setpoint = 0 }).Count == 0, "Boundary rejected");
Reject(loop with { Setpoint = 101 }, "Setpoint");
Reject(loop with { Setpoint = double.NaN }, "Setpoint");
Reject(loop with { ControlledVariable = loop.ControlledVariable with { Unit = " " } }, "Unit");
Reject(loop with { ManipulatedVariable = loop.ManipulatedVariable with { Range = new(1, 1) } }, "Range");
Reject(loop with { ManipulatedVariable = loop.ManipulatedVariable with { Range = new(0, double.PositiveInfinity) } }, "Range");
Reject(loop with { ManipulatedConstraint = new(new(-1, 90)) }, "AllowedRange");
Reject(loop with { ManipulatedConstraint = new(new(10, 90), 0) }, "MaximumRate");
Reject(loop with { Process = null! }, "Process");
Reject(loop with { Disturbances = null! }, "Disturbances");
Reject(loop with { Disturbances = new[] { new LoopDisturbance(new("Feed", "", new(0, 1)), "Feed") } }, "Unit");
Console.WriteLine("Physical loop validation checks passed.");
