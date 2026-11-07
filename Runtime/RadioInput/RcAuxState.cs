namespace UForce.FpvFlight.RadioInput
{
	// Radio functions outside the flight core: payload, camera tilt, thermal. Button presses are counters that only grow,
	// so a reader in FixedUpdate and one in Update both see every press exactly once: remember the last value you handled.
	public readonly struct RcAuxState
	{
		public const int NO_POSITION = -1;

		public RcAuxState(bool isPayloadArmed, int detonateCount, int cameraTiltPosition, int cameraTiltCycleCount, int thermalCycleCount)
		{
			IsPayloadArmed = isPayloadArmed;
			DetonateCount = detonateCount;
			CameraTiltPosition = cameraTiltPosition;
			CameraTiltCycleCount = cameraTiltCycleCount;
			ThermalCycleCount = thermalCycleCount;
		}

		public bool IsPayloadArmed { get; }
		public int DetonateCount { get; }
		// 0..2 from a latching camera tilt switch (SC); NO_POSITION when tilt comes from a button instead.
		public int CameraTiltPosition { get; }
		public int CameraTiltCycleCount { get; }
		public int ThermalCycleCount { get; }

		public static RcAuxState None => new(false, 0, NO_POSITION, 0, 0);
	}
}
