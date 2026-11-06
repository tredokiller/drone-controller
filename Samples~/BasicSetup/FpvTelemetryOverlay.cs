using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Samples
{
	// Debug readout for the demo scene. Not an OSD: the OSD module is a separate assembly.
	public sealed class FpvTelemetryOverlay : MonoBehaviour
	{
		[SerializeField] private FpvFlightController _controller;
		[SerializeField] private FpvFlightDemoDriver _driver;

		private GUIStyle _style;

		private void OnGUI()
		{
			if (!_controller || !_controller.IsInitialized)
			{
				return;
			}

			_style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
			FlightTelemetry telemetry = _controller.Telemetry;

			string state = telemetry.IsArmed
				? $"<color=#ff5555>ARMED</color>  {telemetry.Mode.ToString().ToUpperInvariant()}"
				: $"DISARMED  {telemetry.ArmingBlockReason}";
			string failsafe = telemetry.FailsafeStage == FailsafeStage.None ? string.Empty : $"\n<color=#ffaa00>FAILSAFE {telemetry.FailsafeStage}</color>";
			string source = _driver && _driver.Reader != null ? _driver.Reader.Source.ToString() : "?";

			string text =
				$"{state}{failsafe}\n" +
				$"Input {source}   throttle {telemetry.Throttle:P0}   hover ≈ {FormatThrottle(telemetry.HoverThrottleEstimate)}\n" +
				$"{telemetry.BatteryVoltage:0.0} V  {telemetry.BatteryCellVoltage:0.00} V/cell  {telemetry.BatteryCurrent:0} A  {telemetry.BatteryUsedMah:0} mAh\n" +
				$"{telemetry.GroundSpeed * 3.6f:0} km/h   {telemetry.VerticalSpeed:+0.0;-0.0} m/s   tilt {telemetry.TiltAngle:0}°\n" +
				$"rate R {telemetry.RollRate:0}  P {telemetry.PitchRate:0}  Y {telemetry.YawRate:0} deg/s\n" +
				$"link {telemetry.LinkQuality:P0}  {telemetry.LinkLatency * 1000f:0} ms\n" +
				"<size=12>Keyboard: X arm, M mode, W/S throttle, A/D yaw, arrows pitch/roll</size>";

			GUI.Label(new Rect(12f, 12f, 640f, 200f), text, _style);
		}

		private static string FormatThrottle(float throttle) => float.IsPositiveInfinity(throttle) ? "none" : $"{throttle:P0}";
	}
}
