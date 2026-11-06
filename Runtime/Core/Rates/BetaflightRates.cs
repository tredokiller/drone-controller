using UnityEngine;

namespace UForce.FpvFlight.Rates
{
	// Betaflight "BETAFLIGHT" rates type (rc.c applyBetaflightRates).
	public static class BetaflightRates
	{
		public const float DEFAULT_RATE_LIMIT = 1998f;
		public const float MAX_RC_RATE = 2.55f;

		private const float RC_RATE_INCREMENTAL = 14.54f;

		// Returns the commanded rotation rate in degrees per second for a stick position in [-1, 1].
		public static float Evaluate(float stick, float rcRate, float superRate, float expo, float rateLimit = DEFAULT_RATE_LIMIT)
		{
			float command = Mathf.Clamp(stick, -1f, 1f);
			float commandAbs = Mathf.Abs(command);

			if (expo != 0f)
			{
				command = command * commandAbs * commandAbs * commandAbs * expo + command * (1f - expo);
			}

			float rate = rcRate;
			if (rate > 2f)
			{
				rate += RC_RATE_INCREMENTAL * (rate - 2f);
			}

			float angleRate = 200f * rate * command;

			if (superRate != 0f)
			{
				float superFactor = 1f / Mathf.Clamp(1f - commandAbs * superRate, 0.01f, 1f);
				angleRate *= superFactor;
			}

			return Mathf.Clamp(angleRate, -rateLimit, rateLimit);
		}
	}
}
