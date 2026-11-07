using System;

namespace UForce.FpvFlight.Rc
{
	[Flags]
	public enum ArmingBlockReason
	{
		None = 0,
		ThrottleNotLow = 1 << 0,
		RxLoss = 1 << 1,
		// The arm switch is on but was not switched on with every condition met: switch it off and on again.
		SwitchMustCycle = 1 << 2,
		Failsafe = 1 << 3,
		BatteryEmpty = 1 << 4,
		External = 1 << 5,
		// As in Interceptor Drones: motors cannot be armed while the payload is armed.
		PayloadArmed = 1 << 6,
		// After a crash, until the drone is recovered.
		Crashed = 1 << 7,
		// Held on the ground after a respawn: 1 s, then until throttle is at minimum.
		Recovering = 1 << 8
	}
}
