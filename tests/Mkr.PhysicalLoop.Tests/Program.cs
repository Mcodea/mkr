using Mkr.Core;
using System.Text.Json;

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
var profiles = loop with
{
    SensorProfile = new(0.1, 2, 0, null),
    ActuatorProfile = new(new(0, 100), 5, null, 0)
};
Expect(PhysicalLoopValidator.Validate(profiles).Count == 0, "Valid profiles rejected");
var json = JsonSerializer.Serialize(profiles);
var restored = JsonSerializer.Deserialize<PhysicalLoop>(json)!;
Expect(restored.SensorProfile!.DelaySeconds is null && restored.SensorProfile.NoiseStandardDeviation == 0,
    "JSON must distinguish unknown from zero");
Expect(restored.ActuatorProfile!.Deadband is null && restored.ActuatorProfile.Hysteresis == 0 &&
    restored.ActuatorProfile.Saturation == new PhysicalRange(0, 100), "Actuator JSON round trip failed");
Expect(PhysicalLoopValidator.Validate(restored).Count == 0, "Round trip rejected");
Expect(JsonSerializer.Deserialize<SensorProfile>("{}")!.Resolution is null, "Missing values must be unknown");
Reject(profiles with { SensorProfile = new(Resolution: 0) }, "Resolution");
Reject(profiles with { SensorProfile = new(SamplingFrequencyHz: -1) }, "SamplingFrequencyHz");
Reject(profiles with { SensorProfile = new(NoiseStandardDeviation: -1) }, "NoiseStandardDeviation");
Reject(profiles with { SensorProfile = new(DelaySeconds: double.NaN) }, "DelaySeconds");
Reject(profiles with { ActuatorProfile = new(Saturation: new(10, 0)) }, "Saturation");
Reject(profiles with { ActuatorProfile = new(Saturation: new(0, double.PositiveInfinity)) }, "Saturation");
Reject(profiles with { ActuatorProfile = new(MaximumRatePerSecond: 0) }, "MaximumRatePerSecond");
Reject(profiles with { ActuatorProfile = new(Deadband: -1) }, "Deadband");
Reject(profiles with { ActuatorProfile = new(Hysteresis: double.NegativeInfinity) }, "Hysteresis");
Console.WriteLine("Physical loop validation checks passed.");
