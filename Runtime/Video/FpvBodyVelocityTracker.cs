using UnityEngine;

namespace UForce.FpvFlight.Video
{
	// Velocity from the transform's motion, so it also works for a kinematic body that another peer moves.
	public sealed class FpvBodyVelocityTracker
	{
		private readonly Transform r_transform;

		private Vector3 _previousPosition;
		private bool _hasPrevious;

		public FpvBodyVelocityTracker(Transform transform)
		{
			r_transform = transform;
		}

		public Vector3 Velocity { get; private set; }
		public float SpeedKmh => Velocity.magnitude * 3.6f;

		public void Tick(float deltaTime)
		{
			Vector3 position = r_transform.position;
			if (_hasPrevious && deltaTime > 0f)
			{
				Velocity = (position - _previousPosition) / deltaTime;
			}

			_previousPosition = position;
			_hasPrevious = true;
		}

		public void Reset()
		{
			_hasPrevious = false;
			Velocity = Vector3.zero;
		}
	}
}
