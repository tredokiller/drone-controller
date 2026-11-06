using System;
using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// A two- or three-position switch. Position 0 is the rest value, 2 the active value, 1 the middle.
	[Serializable]
	public sealed class RcSwitchCalibration
	{
		public const int ACTIVE_POSITION = 2;

		[Tooltip("Control path on the device, for example \"ry\". Empty: use the InputAction's joystick binding.")]
		[SerializeField] private string _controlPath = string.Empty;
		[SerializeField] private float _restValue = -1f;
		[SerializeField] private float _activeValue = 1f;
		[SerializeField] private bool _isLatching = true;

		public RcSwitchCalibration()
		{
		}

		public RcSwitchCalibration(string controlPath, float restValue, float activeValue, bool isLatching)
		{
			_controlPath = controlPath ?? string.Empty;
			_restValue = restValue;
			_activeValue = activeValue;
			_isLatching = isLatching;
		}

		public string ControlPath => _controlPath;
		public float RestValue => _restValue;
		public float ActiveValue => _activeValue;
		public bool IsLatching => _isLatching;

		public int GetPosition(float raw) => Mathf.RoundToInt(Mathf.InverseLerp(_restValue, _activeValue, raw) * ACTIVE_POSITION);

		public static RcSwitchCalibration FromCapture(RcControlCapture capture, float current, string controlPath, bool isAlwaysLatching)
		{
			float opposite = capture.FarthestFrom(current);
			float nearest = capture.FarthestFrom(opposite);
			bool isLatching = isAlwaysLatching || !capture.HasReturnedToStart(current);

			return new RcSwitchCalibration(
				controlPath,
				isLatching ? opposite : nearest,
				isLatching ? nearest : opposite,
				isLatching);
		}
	}
}
