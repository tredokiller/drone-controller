namespace UForce.FpvFlight.Rc
{
	public enum FlightMode
	{
		Acro = 0,
		Angle = 1,
		// Betaflight horizon: self-levels around centre stick, acro rates towards full stick, so flips still work.
		Horizon = 2
	}
}
