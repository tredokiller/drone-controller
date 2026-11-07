using UForce.FpvFlight.RadioInput;
using UForce.FpvFlight.Rc;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UForce.FpvFlight.Samples
{
	// Debug readout for the demo scene, in the bottom right corner so it stays clear of the OSD. F1 hides it.
	// Not an OSD: the OSD module is a separate assembly.
	public sealed class FpvTelemetryOverlay : MonoBehaviour
	{
		private const float WIDTH = 700f;
		private const float HEIGHT = 260f;
		private const float MARGIN = 12f;

		[SerializeField] private FpvFlightDemoDriver _driver;
		[SerializeField] private bool _isVisible = true;

		private GUIStyle _style;

		private void Update()
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
			{
				_isVisible = !_isVisible;
			}
		}

		private void OnGUI()
		{
			FpvFlightController controller = _driver ? _driver.Controller : null;
			if (!_isVisible || !controller || !controller.IsInitialized || _driver.IsOsdEditorOpen)
			{
				return;
			}

			_style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.LowerRight };
			FlightTelemetry telemetry = controller.Telemetry;
			RcAuxState aux = _driver.Reader.Aux;

			string state = telemetry.IsArmed
				? $"<color=#ff5555>ARMED</color>  {telemetry.Mode.ToString().ToUpperInvariant()}"
				: $"DISARMED  {telemetry.ArmingBlockReason}";
			string failsafe = telemetry.FailsafeStage == FailsafeStage.None ? string.Empty : $"\n<color=#ffaa00>FAILSAFE {telemetry.FailsafeStage}</color>";
			string crash = _driver.IsCrashed ? "\n<color=#ffaa00>CRASHED: press R</color>" : _driver.IsDetonated ? "\n<color=#ff5555>DETONATED</color>" : string.Empty;
			string type = _driver.CurrentType ? _driver.CurrentType.DisplayName : "?";
			string tilt = aux.CameraTiltPosition == RcAuxState.NO_POSITION ? "button" : aux.CameraTiltPosition.ToString();

			string text =
				$"<b>{type}</b>   {state}{failsafe}{crash}\n" +
				$"Input {_driver.Reader.Source}   throttle {telemetry.Throttle:P0}   hover ≈ {FormatThrottle(telemetry.HoverThrottleEstimate)}\n" +
				$"{telemetry.BatteryVoltage:0.0} V  {telemetry.BatteryCellVoltage:0.00} V/cell  {telemetry.BatteryCurrent:0} A  {telemetry.BatteryUsedMah:0} mAh\n" +
				$"{telemetry.GroundSpeed * 3.6f:0} km/h   {telemetry.VerticalSpeed:+0.0;-0.0} m/s   tilt {telemetry.TiltAngle:0}°\n" +
				$"rate R {telemetry.RollRate:0}  P {telemetry.PitchRate:0}  Y {telemetry.YawRate:0} deg/s\n" +
				$"payload {(aux.IsPayloadArmed ? "<color=#ff5555>ARMED</color>" : "safe")}   camera tilt {tilt}   link {telemetry.LinkQuality:P0}\n" +
				"<size=12>1-5 drone type, R respawn. Arm X / gamepad North with throttle down. Mode M / West: angle, horizon, acro.\n" +
				"Camera tilt V / LT, payload B / LB, detonate G / RT, thermal T / RB. Keyboard: W/S throttle, A/D yaw, arrows. F1 hide\n" +
				$"O OSD editor, Shift+O OSD mode ({OsdModeName()})</size>";

			GUI.Label(new Rect(Screen.width - WIDTH - MARGIN, Screen.height - HEIGHT - MARGIN, WIDTH, HEIGHT), text, _style);
		}

		private static string FormatThrottle(float throttle) => float.IsPositiveInfinity(throttle) ? "none" : $"{throttle:P0}";

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
		private string OsdModeName() => _driver.OsdMode switch
		{
			Osd.FpvOsdMode.Custom => "own",
			Osd.FpvOsdMode.Off => "off",
			_ => "standard"
		};
#else
		private string OsdModeName() => "standard";
#endif
	}
}
