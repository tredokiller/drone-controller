namespace UForce.FpvFlight.Rc
{
	// Betaflight-like arming: the drone arms only on the arm switch's off-to-on edge, with throttle low,
	// a healthy link and no other block. Any block at that moment means the switch must be cycled.
	public sealed class ArmingLogic
	{
		private bool _hasPreviousSwitch;
		private bool _previousSwitch;
		private bool _isWaitingForSwitchOff;

		public bool IsArmed { get; private set; }
		public ArmingBlockReason BlockReason { get; private set; }

		public void Reset()
		{
			IsArmed = false;
			BlockReason = ArmingBlockReason.None;
			_hasPreviousSwitch = false;
			_previousSwitch = false;
			_isWaitingForSwitchOff = false;
		}

		public void ForceDisarm()
		{
			IsArmed = false;
		}

		public void Update(bool armSwitch, float throttle, float armThrottleMax, bool isLinkHealthy, ArmingBlockReason externalBlocks)
		{
			if (!_hasPreviousSwitch)
			{
				// A switch that is already on at start must be cycled, so the first sample is never an edge.
				_hasPreviousSwitch = true;
				_previousSwitch = armSwitch;
			}

			// After rx loss the switch must be seen off on a healthy link before an edge counts (Betaflight bad rx recovery).
			if (!isLinkHealthy)
			{
				_isWaitingForSwitchOff = true;
			}
			else if (!armSwitch)
			{
				_isWaitingForSwitchOff = false;
			}

			bool isRisingEdge = armSwitch && !_previousSwitch;
			_previousSwitch = armSwitch;

			if (IsArmed)
			{
				if (!armSwitch)
				{
					IsArmed = false;
				}

				BlockReason = ArmingBlockReason.None;
				return;
			}

			ArmingBlockReason blocks = externalBlocks;
			if (throttle > armThrottleMax)
			{
				blocks |= ArmingBlockReason.ThrottleNotLow;
			}

			if (!isLinkHealthy)
			{
				blocks |= ArmingBlockReason.RxLoss;
			}

			if (armSwitch && (!isRisingEdge || _isWaitingForSwitchOff))
			{
				blocks |= ArmingBlockReason.SwitchMustCycle;
			}

			BlockReason = blocks;

			if (isRisingEdge && blocks == ArmingBlockReason.None)
			{
				IsArmed = true;
				BlockReason = ArmingBlockReason.None;
			}
		}
	}
}
