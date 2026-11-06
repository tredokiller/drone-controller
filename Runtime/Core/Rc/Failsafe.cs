namespace UForce.FpvFlight.Rc
{
	public sealed class Failsafe
	{
		private float _heldThrottle;

		public FailsafeStage Stage { get; private set; }

		public void Reset()
		{
			Stage = FailsafeStage.None;
			_heldThrottle = 0f;
		}

		// Returns the command the flight controller uses this step. requestDisarm is true while stage 2 is active and the drone is armed.
		public RcCommand Process(in RcCommand delivered, bool hasFrame, float timeSinceLastFrame, bool isArmed, FailsafeSettings settings, out bool requestDisarm)
		{
			requestDisarm = false;
			bool isLinkHealthy = hasFrame && timeSinceLastFrame < settings.RxLossTimeout;

			if (isLinkHealthy)
			{
				Stage = FailsafeStage.None;
				_heldThrottle = delivered.Throttle;
				return delivered;
			}

			if (!isArmed)
			{
				// Link loss while disarmed only blocks arming. A drop that already happened stays visible until the link returns.
				if (Stage != FailsafeStage.StageTwo)
				{
					Stage = FailsafeStage.None;
				}

				return delivered.WithCenteredSticks(0f);
			}

			float lostFor = timeSinceLastFrame - settings.RxLossTimeout;
			if (lostFor >= settings.FailsafeDelay)
			{
				Stage = FailsafeStage.StageTwo;
				requestDisarm = true;
				return delivered.WithCenteredSticks(0f);
			}

			Stage = FailsafeStage.StageOne;
			return delivered.WithCenteredSticks(ResolveStageOneThrottle(settings));
		}

		private float ResolveStageOneThrottle(FailsafeSettings settings)
		{
			switch (settings.StageOneThrottle)
			{
				case FailsafeThrottleMode.Hold:
					return _heldThrottle;
				case FailsafeThrottleMode.Fixed:
					return settings.StageOneFixedThrottle;
				default:
					return 0f;
			}
		}
	}
}
