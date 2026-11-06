using System;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// Keyboard and gamepad stay for debugging. Their sticks are shaped to feel like a radio.
	[Serializable]
	public sealed class RcDebugInputSettings
	{
		[Tooltip("Keyboard virtual stick speed toward full deflection, 1/s.")]
		[SerializeField, Min(0.01f)] private float _keyboardStickAcceleration = 3f;
		[Tooltip("Keyboard virtual stick speed back to center, 1/s.")]
		[SerializeField, Min(0.01f)] private float _keyboardStickReturnSpeed = 4.5f;
		[Tooltip("Keyboard and gamepad move the throttle at this speed and it stays where it is left, like a non-centering stick, 1/s.")]
		[SerializeField, Min(0.01f)] private float _throttleSpeed = 0.5f;
		[SerializeField, Range(0f, 0.5f)] private float _gamepadDeadzone = 0.08f;
		[Tooltip("Gamepad stick curve: value^(1 + exponent).")]
		[SerializeField, Range(0f, 3f)] private float _gamepadExponent = 0.5f;
		[SerializeField] private FlightMode _defaultMode = FlightMode.Angle;

		public float KeyboardStickAcceleration => _keyboardStickAcceleration;
		public float KeyboardStickReturnSpeed => _keyboardStickReturnSpeed;
		public float ThrottleSpeed => _throttleSpeed;
		public float GamepadDeadzone => _gamepadDeadzone;
		public float GamepadExponent => _gamepadExponent;
		public FlightMode DefaultMode => _defaultMode;

		public float ShapeGamepadStick(float value)
		{
			float magnitude = Mathf.InverseLerp(_gamepadDeadzone, 1f, Mathf.Abs(value));
			return Mathf.Sign(value) * Mathf.Pow(magnitude, 1f + _gamepadExponent);
		}
	}
}
