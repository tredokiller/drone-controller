using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// Tracks the range a control has moved through during calibration. Ported from Interceptor's ControlCapture.
	public sealed class RcControlCapture
	{
		private const float DETECTION_SPAN = 0.5f;
		private const float END_STOP_TOLERANCE = 0.15f;

		public RcControlCapture(float start)
		{
			Start = start;
			Min = start;
			Max = start;
		}

		public float Start { get; }
		public float Min { get; private set; }
		public float Max { get; private set; }
		public float Span => Max - Min;
		public bool IsDetected => Span >= DETECTION_SPAN;
		public bool IsStartedTowardsMax { get; private set; }

		public void Add(float value)
		{
			bool wasDetected = IsDetected;
			Min = Mathf.Min(Min, value);
			Max = Mathf.Max(Max, value);

			if (IsDetected && !wasDetected)
			{
				IsStartedTowardsMax = value > Start;
			}
		}

		public float FarthestFrom(float reference) => Max - reference >= reference - Min ? Max : Min;

		public bool IsInverted(float reference) => FarthestFrom(reference) < reference;

		public bool IsInvertedAroundCenter(float center) => CoversBothSides(center) ? !IsStartedTowardsMax : IsInverted(center);

		public bool HasReturnedToStart(float value) => Mathf.Abs(value - Start) <= Span * END_STOP_TOLERANCE;

		public bool IsAtEndStop(float value) => Mathf.Min(value - Min, Max - value) <= Span * END_STOP_TOLERANCE;

		public bool CoversBothSides(float center) => Mathf.Min(center - Min, Max - center) >= DETECTION_SPAN * 0.5f;

		public float GetSideBalance(float center) => Mathf.Min(center - Min, Max - center) / Mathf.Max(1e-5f, Mathf.Max(center - Min, Max - center));
	}
}
