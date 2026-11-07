using System;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// Keyboard and gamepad for debugging, configured as in Interceptor Drones (DroneMovementController, P1-SUN prefab):
	// throttle follows the stick position, the arm button requests arming and the drone arms once throttle is at the bottom.
	[Serializable]
	public sealed class RcDebugInputSettings
	{
		[Tooltip("Keyboard virtual stick speed toward full deflection, 1/s.")]
		[SerializeField, Min(0.01f)] private float _keyboardStickAcceleration = 3f;
		[Tooltip("Keyboard virtual stick speed back to center, 1/s.")]
		[SerializeField, Min(0.01f)] private float _keyboardStickReturnSpeed = 4.5f;
		[SerializeField, Range(0f, 0.5f)] private float _gamepadDeadzone = 0.1f;
		[Tooltip("Gamepad stick curve: value^(1 + exponent).")]
		[SerializeField, Range(0f, 3f)] private float _gamepadExponent = 0.8f;
		[Tooltip("An arm request becomes the arm switch only at or below this throttle.")]
		[SerializeField, Range(0f, 0.3f)] private float _armThrottleMax = 0.05f;
		[SerializeField] private FlightMode _defaultMode = FlightMode.Acro;

		public float KeyboardStickAcceleration => _keyboardStickAcceleration;
		public float KeyboardStickReturnSpeed => _keyboardStickReturnSpeed;
		public float GamepadDeadzone => _gamepadDeadzone;
		public float GamepadExponent => _gamepadExponent;
		public float ArmThrottleMax => _armThrottleMax;
		public FlightMode DefaultMode => _defaultMode;

		public float ShapeGamepadStick(float value)
		{
			float magnitude = Mathf.InverseLerp(_gamepadDeadzone, 1f, Mathf.Abs(value));
			return Mathf.Sign(value) * Mathf.Pow(magnitude, 1f + _gamepadExponent);
		}
	}
}
