using System;
using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// One radio axis: raw end points and center, inversion and deadzone. Normalize is ported from Interceptor's ControllerAxisBinding.
	[Serializable]
	public sealed class RcAxisCalibration
	{
		private const float END_STOP_MARGIN = 0.03f;

		[Tooltip("Control path on the device, for example \"z\" or \"stick/y\". Empty: use the InputAction's joystick binding.")]
		[SerializeField] private string _controlPath = string.Empty;
		[SerializeField] private float _min = -1f;
		[SerializeField] private float _center;
		[SerializeField] private float _max = 1f;
		[Tooltip("Self-centering stick. Throttle is not centered.")]
		[SerializeField] private bool _isCentered = true;
		[SerializeField] private bool _isInverted;
		[Tooltip("Deadzone around center for centered axes, in normalized units.")]
		[SerializeField, Range(0f, 0.2f)] private float _deadzone = 0.02f;

		public RcAxisCalibration()
		{
		}

		public RcAxisCalibration(string controlPath, float min, float center, float max, bool isCentered, bool isInverted, float deadzone)
		{
			_controlPath = controlPath ?? string.Empty;
			_min = min;
			_center = center;
			_max = max;
			_isCentered = isCentered;
			_isInverted = isInverted;
			_deadzone = Mathf.Clamp(deadzone, 0f, 0.2f);
		}

		public string ControlPath => _controlPath;
		public float Min => _min;
		public float Center => _center;
		public float Max => _max;
		public bool IsCentered => _isCentered;
		public bool IsInverted => _isInverted;
		public float Deadzone => _deadzone;

		// Returns -1..1. For a non-centered axis (throttle) -1 is the bottom end and +1 the top end.
		public float Normalize(float raw)
		{
			float margin = (_max - _min) * END_STOP_MARGIN;
			float min = _min + margin;
			float max = _max - margin;

			float value = _isCentered
				? raw >= _center
					? Mathf.InverseLerp(_center, max, raw)
					: -Mathf.InverseLerp(_center, min, raw)
				: Mathf.InverseLerp(min, max, raw) * 2f - 1f;

			if (_isInverted)
			{
				value = -value;
			}

			return _isCentered ? ApplyDeadzone(value, _deadzone) : value;
		}

		public void SetRange(float min, float max)
		{
			_min = min;
			_max = max;
		}

		public void SetInverted(bool isInverted) => _isInverted = isInverted;

		private static float ApplyDeadzone(float value, float deadzone)
		{
			if (deadzone <= 0f)
			{
				return value;
			}

			float abs = Mathf.Abs(value);
			return abs < deadzone ? 0f : Mathf.Sign(value) * Mathf.InverseLerp(deadzone, 1f, abs);
		}
	}
}
