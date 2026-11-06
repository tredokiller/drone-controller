using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	public static class ThrustModel
	{
		// Throttle 0..1 to motor command with Betaflight-style idle.
		public static float MotorCommand(float throttle, float motorIdle) => motorIdle + (1f - motorIdle) * Mathf.Clamp01(throttle);

		public static float ThrottleFromMotorCommand(float motorCommand, float motorIdle) =>
			motorIdle >= 1f ? 0f : (motorCommand - motorIdle) / (1f - motorIdle);

		// Fraction of max thrust produced at a motor output: k·o + (1 − k)·o².
		public static float ThrustFraction(float motorOutput, float linearity)
		{
			float output = Mathf.Clamp01(motorOutput);
			return linearity * output + (1f - linearity) * output * output;
		}

		public static float MotorOutputFromThrustFraction(float fraction, float linearity)
		{
			float value = Mathf.Clamp01(fraction);
			float quadratic = 1f - linearity;
			if (quadratic < 1e-5f)
			{
				return value;
			}

			return (-linearity + Mathf.Sqrt(linearity * linearity + 4f * quadratic * value)) / (2f * quadratic);
		}
	}
}
