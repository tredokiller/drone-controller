namespace UForce.FpvFlight.RadioInput
{
	public static class RcSwitchChannels
	{
		public const int COUNT = 6;

		// Switches that hold a state. On an axis control the calibration wizard always treats them as latching,
		// as Interceptor Drones does; any other channel is latching only if it was not returned to its start.
		public static bool IsStateSwitch(RcSwitchChannel channel) =>
			channel == RcSwitchChannel.Arm
			|| channel == RcSwitchChannel.PayloadArm
			|| channel == RcSwitchChannel.CameraTilt
			|| channel == RcSwitchChannel.FlightMode;
	}
}
