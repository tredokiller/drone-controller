using UnityEngine;

namespace UForce.FpvFlight
{
	// Rigid body state in world space. AngularVelocity is in rad/s.
	public readonly struct FlightBodyState
	{
		public readonly Vector3 Position;
		public readonly Quaternion Rotation;
		public readonly Vector3 Velocity;
		public readonly Vector3 AngularVelocity;

		public FlightBodyState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
		{
			Position = position;
			Rotation = rotation;
			Velocity = velocity;
			AngularVelocity = angularVelocity;
		}
	}
}
