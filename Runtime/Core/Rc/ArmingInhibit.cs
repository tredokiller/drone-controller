using System;

namespace UForce.FpvFlight.Rc
{
	// Arming blocks the project sets on the flight core. Each one shows up as its ArmingBlockReason.
	[Flags]
	public enum ArmingInhibit
	{
		None = 0,
		External = 1 << 0,
		PayloadArmed = 1 << 1,
		Crashed = 1 << 2,
		// Set by FpvFlightController.Recover while the drone is held on the ground.
		Recovering = 1 << 3
	}
}
