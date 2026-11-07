using UnityEngine;

namespace UForce.FpvFlight
{
	// Rigid body state in world space. AngularVelocity is in rad/s. IsGrounded is only used by the Interceptor flight model.
	public readonly struct FlightBodyState
	{
		public readonly Vector3 Position;
		public readonly Quaternion Rotation;
		public readonly Vector3 Velocity;
		public readonly Vector3 AngularVelocity;
		public readonly bool IsGrounded;

		public FlightBodyState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, bool isGrounded = false)
		{
			Position = position;
			Rotation = rotation;
			Velocity = velocity;
			AngularVelocity = angularVelocity;
			IsGrounded = isGrounded;
		}
	}
}
