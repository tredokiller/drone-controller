using UnityEngine;

namespace UForce.FpvFlight.Visuals
{
	// FPV camera tilt, ported from Interceptor Drones (DroneMovementController camera angle). Each position is an uptilt
	// above the body's forward axis: the interceptor's 3-position switch SC gives 90° (looking along the thrust axis,
	// out of the nose of a vertical interceptor), 45° and 0° (along the body). One angle is a fixed uptilt, and the switch
	// is ignored. Moves with an ease-in-out over the switch duration; an optional servo sound loops while it moves.
	// The camera's parent must be aligned with the drone body. Works together with FpvCameraShake.
	[DisallowMultipleComponent]
	public sealed class FpvCameraTilt : MonoBehaviour
	{
		[Tooltip("Uptilt per switch position, degrees above the body's forward axis (Interceptor: 90, 45, 0).")]
		[SerializeField] private float[] _uptiltAngles = { 90f, 45f, 0f };
		[SerializeField, Range(0, 2)] private int _startPosition;
		[Tooltip("Seconds for one switch (Interceptor P1-SUN: 0.15).")]
		[SerializeField, Min(0f)] private float _switchDuration = 0.15f;
		[Tooltip("Optional servo sound, looped while the camera moves.")]
		[SerializeField] private AudioSource _servoAudio;

		private int _position;
		private float _fromAngle;
		private float _toAngle;
		private float _progress = 1f;

		public int Position => _position;
		public int PositionCount => _uptiltAngles != null ? _uptiltAngles.Length : 0;
		public float CurrentUptilt { get; private set; }
		public bool IsMoving => _progress < 1f;

		private void Awake()
		{
			if (_servoAudio)
			{
				_servoAudio.playOnAwake = false;
				_servoAudio.loop = true;
			}

			SetPosition(_startPosition, true);
		}

		// From a latching switch: 0 rest, 1 middle, 2 active. Clamped to the available positions.
		public void SetPosition(int position, bool isInstant = false)
		{
			if (PositionCount == 0)
			{
				return;
			}

			int clamped = Mathf.Clamp(position, 0, PositionCount - 1);
			if (clamped == _position && !isInstant && _progress >= 1f && Mathf.Approximately(CurrentUptilt, _uptiltAngles[clamped]))
			{
				return;
			}

			_position = clamped;
			_fromAngle = CurrentUptilt;
			_toAngle = _uptiltAngles[clamped];
			_progress = isInstant || _switchDuration <= 0f ? 1f : 0f;
			if (isInstant)
			{
				Apply(_toAngle);
			}
		}

		// From a button: next position, wrapping around (Interceptor's V / left trigger).
		public void Cycle()
		{
			if (PositionCount > 1)
			{
				SetPosition((_position + 1) % PositionCount);
			}
		}

		public void SetAngles(float[] uptiltAngles, int startPosition)
		{
			_uptiltAngles = uptiltAngles != null && uptiltAngles.Length > 0 ? uptiltAngles : new[] { 0f };
			SetPosition(startPosition, true);
		}

		private void LateUpdate()
		{
			if (_progress < 1f)
			{
				_progress = Mathf.MoveTowards(_progress, 1f, Time.deltaTime / Mathf.Max(0.0001f, _switchDuration));
				// InOutSine, as Interceptor's tween.
				float eased = -(Mathf.Cos(Mathf.PI * _progress) - 1f) * 0.5f;
				Apply(Mathf.LerpAngle(_fromAngle, _toAngle, eased));
			}

			UpdateServoSound();
		}

		private void Apply(float uptilt)
		{
			CurrentUptilt = uptilt;
			transform.localRotation = Quaternion.Euler(-uptilt, 0f, 0f);
		}

		private void UpdateServoSound()
		{
			if (!_servoAudio)
			{
				return;
			}

			bool isMoving = IsMoving;
			if (isMoving && !_servoAudio.isPlaying)
			{
				_servoAudio.Play();
			}
			else if (!isMoving && _servoAudio.isPlaying)
			{
				_servoAudio.Stop();
			}
		}
	}
}
