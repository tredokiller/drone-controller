using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	// The six actions an FPV radio needs. Bindings (radio, gamepad, keyboard) live in the project's .inputactions asset.
	public sealed class RcInputActions
	{
		public const string ROLL = "Roll";
		public const string PITCH = "Pitch";
		public const string YAW = "Yaw";
		public const string THROTTLE = "Throttle";
		public const string ARM = "Arm";
		public const string FLIGHT_MODE = "FlightMode";

		public RcInputActions(InputAction roll, InputAction pitch, InputAction yaw, InputAction throttle, InputAction arm, InputAction flightMode)
		{
			Roll = roll;
			Pitch = pitch;
			Yaw = yaw;
			Throttle = throttle;
			Arm = arm;
			FlightMode = flightMode;
		}

		public InputAction Roll { get; }
		public InputAction Pitch { get; }
		public InputAction Yaw { get; }
		public InputAction Throttle { get; }
		public InputAction Arm { get; }
		public InputAction FlightMode { get; }

		public static RcInputActions FromMap(InputActionMap map) => new(
			map.FindAction(ROLL),
			map.FindAction(PITCH),
			map.FindAction(YAW),
			map.FindAction(THROTTLE),
			map.FindAction(ARM),
			map.FindAction(FLIGHT_MODE));

		public void Enable()
		{
			Roll?.Enable();
			Pitch?.Enable();
			Yaw?.Enable();
			Throttle?.Enable();
			Arm?.Enable();
			FlightMode?.Enable();
		}

		public void Disable()
		{
			Roll?.Disable();
			Pitch?.Disable();
			Yaw?.Disable();
			Throttle?.Disable();
			Arm?.Disable();
			FlightMode?.Disable();
		}
	}
}
