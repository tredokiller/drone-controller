using UnityEngine;

namespace UForce.FpvFlight.Environment
{
	public readonly struct WindSample
	{
		// Air velocity in world space, m/s.
		public readonly Vector3 Velocity;
		// 0..1. Scales random attitude disturbances.
		public readonly float Turbulence;

		public WindSample(Vector3 velocity, float turbulence)
		{
			Velocity = velocity;
			Turbulence = Mathf.Clamp01(turbulence);
		}

		public static WindSample Calm => new(Vector3.zero, 0f);
	}
}
