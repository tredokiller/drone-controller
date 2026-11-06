namespace UForce.FpvFlight.Rc
{
	public enum FailsafeThrottleMode
	{
		// Betaflight rxfail "auto": throttle goes to minimum.
		Low = 0,
		Hold = 1,
		Fixed = 2
	}
}
