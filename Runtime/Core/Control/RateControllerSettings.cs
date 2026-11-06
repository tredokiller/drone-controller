using UnityEngine;

namespace UForce.FpvFlight.Control
{
	// Body axes: x pitch (nose down +), y yaw (nose right +), z roll (left +). Units: s, rad/s².
	public readonly struct RateControllerSettings
	{
		public readonly Vector3 ResponseTime;
		public readonly Vector3 MaxAcceleration;
		public readonly Vector3 IntegralLimit;
		public readonly float IntegralTime;
		public readonly float FeedForwardDrag;

		public RateControllerSettings(Vector3 responseTime, Vector3 maxAcceleration, Vector3 integralLimit, float integralTime, float feedForwardDrag)
		{
			ResponseTime = responseTime;
			MaxAcceleration = maxAcceleration;
			IntegralLimit = integralLimit;
			IntegralTime = integralTime;
			FeedForwardDrag = feedForwardDrag;
		}
	}
}
