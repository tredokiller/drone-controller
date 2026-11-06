using UnityEngine;

namespace UForce.FpvFlight.Rc
{
	// Radio convention: Roll + rolls right, Pitch + is stick forward (nose down), Yaw + turns the nose right.
	public readonly struct RcCommand
	{
		public readonly float Roll;
		public readonly float Pitch;
		public readonly float Yaw;
		public readonly float Throttle;
		public readonly bool ArmSwitch;
		public readonly FlightMode Mode;

		public RcCommand(float roll, float pitch, float yaw, float throttle, bool armSwitch, FlightMode mode)
		{
			Roll = Mathf.Clamp(roll, -1f, 1f);
			Pitch = Mathf.Clamp(pitch, -1f, 1f);
			Yaw = Mathf.Clamp(yaw, -1f, 1f);
			Throttle = Mathf.Clamp01(throttle);
			ArmSwitch = armSwitch;
			Mode = mode;
		}

		public static RcCommand Neutral => new(0f, 0f, 0f, 0f, false, FlightMode.Acro);

		public RcCommand WithCenteredSticks(float throttle) => new(0f, 0f, 0f, throttle, ArmSwitch, Mode);

		public RcCommand WithArmSwitch(bool armSwitch) => new(Roll, Pitch, Yaw, Throttle, armSwitch, Mode);

		public RcCommand WithMode(FlightMode mode) => new(Roll, Pitch, Yaw, Throttle, ArmSwitch, mode);
	}
}
