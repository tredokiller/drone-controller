using UnityEngine;

namespace UForce.FpvFlight.Control
{
	// Self-levelling for angle mode. It turns the tilt error into body roll and pitch rates for the same rate loop
	// acro uses, so switching modes in flight is bumpless and it works from any attitude, including inverted.
	public static class AngleController
	{
		private const float MIN_ERROR_ANGLE = 1e-5f;

		// Returns the body rate in rad/s: x pitch (nose down +), z roll (left +); y is always 0 (yaw stays rate-based).
		public static Vector3 ComputeTiltRate(Quaternion rotation, float rollStick, float pitchStick, float angleLimitDegrees, float strength)
		{
			Vector3 up = rotation * Vector3.up;
			Vector3 desiredUp = ComputeDesiredUp(rotation, rollStick, pitchStick, angleLimitDegrees);

			float angle = Vector3.Angle(up, desiredUp) * Mathf.Deg2Rad;
			if (angle < MIN_ERROR_ANGLE)
			{
				return Vector3.zero;
			}

			Vector3 axis = Vector3.Cross(up, desiredUp);
			if (axis.sqrMagnitude < 1e-8f)
			{
				// Exactly inverted: roll out about the body's forward axis.
				axis = rotation * Vector3.forward;
			}

			Vector3 bodyAxis = Quaternion.Inverse(rotation) * axis.normalized;
			Vector3 rate = bodyAxis * (angle * strength);
			rate.y = 0f;
			return rate;
		}

		public static Vector3 ComputeDesiredUp(Quaternion rotation, float rollStick, float pitchStick, float angleLimitDegrees)
		{
			Quaternion heading = Quaternion.LookRotation(GetHeading(rotation), Vector3.up);
			float pitch = Mathf.Clamp(pitchStick, -1f, 1f) * angleLimitDegrees;
			float roll = Mathf.Clamp(rollStick, -1f, 1f) * angleLimitDegrees;
			Quaternion tilt = Quaternion.Euler(pitch, 0f, -roll);
			return heading * tilt * Vector3.up;
		}

		// Tilt angle from level in degrees: 0 level, 90 on its side, 180 inverted.
		public static float GetTiltAngle(Quaternion rotation) => Vector3.Angle(rotation * Vector3.up, Vector3.up);

		private static Vector3 GetHeading(Quaternion rotation)
		{
			Vector3 forward = rotation * Vector3.forward;
			Vector3 heading = Vector3.ProjectOnPlane(forward, Vector3.up);

			if (heading.sqrMagnitude < 0.01f)
			{
				// Nose straight down or up: the body up axis points along the heading (down) or against it (up).
				heading = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up) * -Mathf.Sign(forward.y);
			}

			return heading.sqrMagnitude < 1e-6f ? Vector3.forward : heading.normalized;
		}
	}
}
