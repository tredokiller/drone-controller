using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	// The actions an FPV radio needs. Bindings (radio, gamepad, keyboard) live in the project's .inputactions asset.
	// The flight actions are required; the payload, camera tilt and thermal actions are optional (null: no such channel).
	public sealed class RcInputActions
	{
		public const string ROLL = "Roll";
		public const string PITCH = "Pitch";
		public const string YAW = "Yaw";
		public const string THROTTLE = "Throttle";
		public const string ARM = "Arm";
		public const string FLIGHT_MODE = "FlightMode";
		public const string PAYLOAD_ARM = "PayloadArm";
		public const string PAYLOAD_DETONATE = "PayloadDetonate";
		public const string CAMERA_TILT = "CameraTilt";
		public const string THERMAL = "Thermal";

		public RcInputActions(
			InputAction roll,
			InputAction pitch,
			InputAction yaw,
			InputAction throttle,
			InputAction arm,
			InputAction flightMode,
			InputAction payloadArm = null,
			InputAction payloadDetonate = null,
			InputAction cameraTilt = null,
			InputAction thermal = null)
		{
			Roll = roll;
			Pitch = pitch;
			Yaw = yaw;
			Throttle = throttle;
			Arm = arm;
			FlightMode = flightMode;
			PayloadArm = payloadArm;
			PayloadDetonate = payloadDetonate;
			CameraTilt = cameraTilt;
			Thermal = thermal;
		}

		public InputAction Roll { get; }
		public InputAction Pitch { get; }
		public InputAction Yaw { get; }
		public InputAction Throttle { get; }
		public InputAction Arm { get; }
		public InputAction FlightMode { get; }
		public InputAction PayloadArm { get; }
		public InputAction PayloadDetonate { get; }
		public InputAction CameraTilt { get; }
		public InputAction Thermal { get; }

		public static RcInputActions FromMap(InputActionMap map) => new(
			map.FindAction(ROLL),
			map.FindAction(PITCH),
			map.FindAction(YAW),
			map.FindAction(THROTTLE),
			map.FindAction(ARM),
			map.FindAction(FLIGHT_MODE),
			map.FindAction(PAYLOAD_ARM),
			map.FindAction(PAYLOAD_DETONATE),
			map.FindAction(CAMERA_TILT),
			map.FindAction(THERMAL));

		public InputAction GetSwitchAction(RcSwitchChannel channel)
		{
			switch (channel)
			{
				case RcSwitchChannel.Arm:
					return Arm;
				case RcSwitchChannel.FlightMode:
					return FlightMode;
				case RcSwitchChannel.PayloadArm:
					return PayloadArm;
				case RcSwitchChannel.PayloadDetonate:
					return PayloadDetonate;
				case RcSwitchChannel.CameraTilt:
					return CameraTilt;
				default:
					return Thermal;
			}
		}

		public void Enable()
		{
			Roll?.Enable();
			Pitch?.Enable();
			Yaw?.Enable();
			Throttle?.Enable();
			for (int index = 0; index < RcSwitchChannels.COUNT; index++)
			{
				GetSwitchAction((RcSwitchChannel)index)?.Enable();
			}
		}

		public void Disable()
		{
			Roll?.Disable();
			Pitch?.Disable();
			Yaw?.Disable();
			Throttle?.Disable();
			for (int index = 0; index < RcSwitchChannels.COUNT; index++)
			{
				GetSwitchAction((RcSwitchChannel)index)?.Disable();
			}
		}
	}
}
