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
		External = 1 << 5
	}
}
