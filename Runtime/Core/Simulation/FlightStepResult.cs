using UnityEngine;

namespace UForce.FpvFlight
{
	// What one simulation step asks the rigid body to do. Forces in N, angular acceleration in rad/s², all in world space.
	public readonly struct FlightStepResult
	{
		public readonly Vector3 ThrustForce;
		public readonly Vector3 DragForce;
		public readonly Vector3 AngularAcceleration;

		public FlightStepResult(Vector3 thrustForce, Vector3 dragForce, Vector3 angularAcceleration)
		{
			ThrustForce = thrustForce;
			DragForce = dragForce;
			AngularAcceleration = angularAcceleration;
		}
	}
}
