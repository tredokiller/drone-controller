namespace UForce.FpvFlight.RadioInput
{
	// Live readout of the radio with the calibration being built: normalized sticks and switch positions.
	// Unbound channels read as not bound. Reused every frame, so reading it allocates nothing.
	public sealed class RcCalibrationPreview
	{
		private const int AXIS_COUNT = 4;

		private readonly float[] r_axes = new float[AXIS_COUNT];
		private readonly bool[] r_axisBound = new bool[AXIS_COUNT];
		private readonly int[] r_switchPositions = new int[RcSwitchChannels.COUNT];

		public RcCalibrationPreview()
		{
			Clear();
		}

		// -1..1 for roll, pitch and yaw; 0..1 for throttle.
		public float GetAxis(RcAxisChannel channel) => r_axes[(int)channel];
		public bool IsAxisBound(RcAxisChannel channel) => r_axisBound[(int)channel];

		// 0 rest, 1 middle, 2 active; RcAuxState.NO_POSITION when the switch is not bound.
		public int GetSwitchPosition(RcSwitchChannel channel) => r_switchPositions[(int)channel];

		internal void SetAxis(RcAxisChannel channel, float value, bool isBound)
		{
			r_axes[(int)channel] = isBound ? value : 0f;
			r_axisBound[(int)channel] = isBound;
		}

		internal void SetSwitch(RcSwitchChannel channel, int position) => r_switchPositions[(int)channel] = position;

		internal void Clear()
		{
			for (int index = 0; index < AXIS_COUNT; index++)
			{
				r_axes[index] = 0f;
				r_axisBound[index] = false;
			}

			for (int index = 0; index < r_switchPositions.Length; index++)
			{
				r_switchPositions[index] = RcAuxState.NO_POSITION;
			}
		}
	}
}
