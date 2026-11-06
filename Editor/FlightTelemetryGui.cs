using UForce.FpvFlight.Rc;
using UnityEditor;

namespace UForce.FpvFlight.Editor
{
	public static class FlightTelemetryGui
	{
		public static void Draw(in FlightTelemetry telemetry)
		{
			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.TextField("State", telemetry.IsArmed ? $"ARMED, {telemetry.Mode}" : $"DISARMED ({telemetry.ArmingBlockReason})");
				EditorGUILayout.TextField("Failsafe", telemetry.FailsafeStage == FailsafeStage.None ? "none" : telemetry.FailsafeStage.ToString());
				EditorGUILayout.TextField("Link", $"{(telemetry.IsLinkHealthy ? "ok" : "LOST")}, quality {telemetry.LinkQuality:P0}, latency {telemetry.LinkLatency * 1000f:0} ms");
				EditorGUILayout.TextField("Throttle / motors", $"{telemetry.Throttle:P0} / {telemetry.MotorOutput:P0}, hover ≈ {FormatThrottle(telemetry.HoverThrottleEstimate)}");
				EditorGUILayout.TextField("Thrust", $"{telemetry.Thrust:0.0} N, T/W {telemetry.ThrustToWeight:0.00}, mass {telemetry.TotalMass:0.00} kg");
				EditorGUILayout.TextField("Battery", $"{telemetry.BatteryVoltage:0.00} V ({telemetry.BatteryCellVoltage:0.00} V/cell), {telemetry.BatteryCurrent:0.0} A, {telemetry.BatteryUsedMah:0} mAh, {telemetry.BatteryStateOfCharge:P0}");
				EditorGUILayout.TextField("Rates target", $"R {telemetry.TargetRollRate:0}  P {telemetry.TargetPitchRate:0}  Y {telemetry.TargetYawRate:0} deg/s");
				EditorGUILayout.TextField("Rates measured", $"R {telemetry.RollRate:0}  P {telemetry.PitchRate:0}  Y {telemetry.YawRate:0} deg/s");
				EditorGUILayout.TextField("Motion", $"tilt {telemetry.TiltAngle:0}°, {telemetry.GroundSpeed * 3.6f:0} km/h ground, {telemetry.Airspeed * 3.6f:0} km/h air, {telemetry.VerticalSpeed:0.0} m/s vertical");
			}
		}

		private static string FormatThrottle(float throttle) => float.IsPositiveInfinity(throttle) ? "none" : $"{throttle:P0}";
	}
}
