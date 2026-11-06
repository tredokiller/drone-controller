using UnityEngine;

namespace UForce.FpvFlight.Control
{
	// Rate loop based on Interceptor's ApplyInstantAttitude: the stick sets a body rate and the loop drives the
	// measured rate toward it with an angular acceleration. A released stick commands zero rate, so attitude holds.
	public sealed class RateController
	{
		// With at least 1.5 steps per time constant the discrete loop cannot overshoot.
		public const float MIN_RESPONSE_STEPS = 1.5f;

		// I-term relax: the I-term only accumulates while the rate error is small (disturbances), not during stick moves,
		// so it cannot wind up during a flip and keep rotating the drone after the stick is released. rad/s (40 deg/s).
		private const float INTEGRAL_RELAX_ERROR = 0.698f;

		private Vector3 _integral;

		public Vector3 Integral => _integral;

		public void Reset()
		{
			_integral = Vector3.zero;
		}

		// Returns the body angular acceleration in rad/s².
		public Vector3 Compute(Vector3 targetRate, Vector3 currentRate, in RateControllerSettings settings, bool isIntegrating, float deltaTime)
		{
			Vector3 output;
			output.x = ComputeAxis(targetRate.x, currentRate.x, settings.ResponseTime.x, settings.MaxAcceleration.x, settings.IntegralLimit.x, settings, isIntegrating, deltaTime, ref _integral.x);
			output.y = ComputeAxis(targetRate.y, currentRate.y, settings.ResponseTime.y, settings.MaxAcceleration.y, settings.IntegralLimit.y, settings, isIntegrating, deltaTime, ref _integral.y);
			output.z = ComputeAxis(targetRate.z, currentRate.z, settings.ResponseTime.z, settings.MaxAcceleration.z, settings.IntegralLimit.z, settings, isIntegrating, deltaTime, ref _integral.z);
			return output;
		}

		private static float ComputeAxis(
			float target,
			float current,
			float responseTime,
			float maxAcceleration,
			float integralLimit,
			in RateControllerSettings settings,
			bool isIntegrating,
			float deltaTime,
			ref float integral)
		{
			float timeConstant = Mathf.Max(responseTime, deltaTime * MIN_RESPONSE_STEPS);
			float error = target - current;
			float proportional = error / timeConstant;
			float feedForward = target * settings.FeedForwardDrag;

			float previousIntegral = integral;
			if (isIntegrating && settings.IntegralTime > 0f)
			{
				float relax = Mathf.Clamp01(1f - Mathf.Abs(error) / INTEGRAL_RELAX_ERROR);
				integral = Mathf.Clamp(integral + proportional * relax * deltaTime / settings.IntegralTime, -integralLimit, integralLimit);
			}

			float acceleration = proportional + integral + feedForward;

			// Anti-windup: do not grow the I-term while the output is saturated in the same direction.
			if (Mathf.Abs(acceleration) > maxAcceleration && Mathf.Sign(acceleration) == Mathf.Sign(error))
			{
				integral = previousIntegral;
				acceleration = proportional + integral + feedForward;
			}

			return Mathf.Clamp(acceleration, -maxAcceleration, maxAcceleration);
		}
	}
}
