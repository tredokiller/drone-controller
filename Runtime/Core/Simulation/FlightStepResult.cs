using UnityEngine;

namespace UForce.FpvFlight
{
	// What one simulation step asks the rigid body to do. Forces in N, accelerations in m/s² and rad/s², all in world space.
	// The Interceptor flight model also asks for an extra acceleration and hard limits on speed and on body rates.
	public readonly struct FlightStepResult
	{
		public readonly Vector3 ThrustForce;
		public readonly Vector3 DragForce;
		public readonly Vector3 AngularAcceleration;
		public readonly Vector3 Acceleration;
		// m/s; positive infinity: no limit.
		public readonly float MaxSpeed;
		// Body rates, rad/s, x pitch, y yaw, z roll; positive infinity: no limit.
		public readonly Vector3 MaxBodyRates;

		public FlightStepResult(Vector3 thrustForce, Vector3 dragForce, Vector3 angularAcceleration)
			: this(thrustForce, dragForce, angularAcceleration, Vector3.zero, float.PositiveInfinity, Vector3.positiveInfinity)
		{
		}

		public FlightStepResult(Vector3 thrustForce, Vector3 dragForce, Vector3 angularAcceleration, Vector3 acceleration, float maxSpeed, Vector3 maxBodyRates)
		{
			ThrustForce = thrustForce;
			DragForce = dragForce;
			AngularAcceleration = angularAcceleration;
			Acceleration = acceleration;
			MaxSpeed = maxSpeed;
			MaxBodyRates = maxBodyRates;
		}

		public bool HasLimits => !float.IsPositiveInfinity(MaxSpeed) || !float.IsPositiveInfinity(MaxBodyRates.x);
	}
}
