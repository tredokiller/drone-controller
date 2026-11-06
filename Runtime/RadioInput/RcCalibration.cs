using System;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// Full calibration of one radio. Defaults match a Radiomaster TX12 (EdgeTX/OpenTX, AETR, USB joystick mode):
	// throttle z, yaw rx, pitch stick/y (inverted), roll stick/x, arm ry, flight mode slider1.
	[Serializable]
	public sealed class RcCalibration
	{
		[SerializeField] private string _deviceKey = string.Empty;
		[SerializeField] private string _deviceName = string.Empty;

		[Header("Axes")]
		[SerializeField] private RcAxisCalibration _throttle = new(string.Empty, -1f, -1f, 1f, false, false, 0f);
		[SerializeField] private RcAxisCalibration _yaw = new(string.Empty, -1f, 0f, 1f, true, false, 0.02f);
		[SerializeField] private RcAxisCalibration _pitch = new(string.Empty, -1f, 0f, 1f, true, true, 0.02f);
		[SerializeField] private RcAxisCalibration _roll = new(string.Empty, -1f, 0f, 1f, true, false, 0.02f);
		[Tooltip("Bottom throttle deadzone: the lowest part of the stick travel reads as 0%.")]
		[SerializeField, Range(0f, 0.2f)] private float _throttleLowDeadzone = 0.03f;

		[Header("Switches")]
		[SerializeField] private RcSwitchCalibration _arm = new(string.Empty, 1f, -1f, true);
		[Tooltip("Arm switch position that means armed: 0 rest, 1 middle, 2 active.")]
		[SerializeField, Range(0, 2)] private int _armActivePosition = RcSwitchCalibration.ACTIVE_POSITION;
		[SerializeField] private RcSwitchCalibration _flightMode = new(string.Empty, -1f, 1f, true);
		[SerializeField] private FlightMode _modeAtRest = FlightMode.Angle;
		[SerializeField] private FlightMode _modeAtMiddle = FlightMode.Acro;
		[SerializeField] private FlightMode _modeAtActive = FlightMode.Acro;

		public string DeviceKey => _deviceKey;
		public string DeviceName => _deviceName;
		public RcAxisCalibration Throttle => _throttle;
		public RcAxisCalibration Yaw => _yaw;
		public RcAxisCalibration Pitch => _pitch;
		public RcAxisCalibration Roll => _roll;
		public float ThrottleLowDeadzone => _throttleLowDeadzone;
		public RcSwitchCalibration ArmSwitch => _arm;
		public int ArmActivePosition => _armActivePosition;
		public RcSwitchCalibration FlightModeSwitch => _flightMode;
		public FlightMode ModeAtRest => _modeAtRest;
		public FlightMode ModeAtMiddle => _modeAtMiddle;
		public FlightMode ModeAtActive => _modeAtActive;

		public RcAxisCalibration GetAxis(RcAxisChannel channel)
		{
			switch (channel)
			{
				case RcAxisChannel.Throttle:
					return _throttle;
				case RcAxisChannel.Yaw:
					return _yaw;
				case RcAxisChannel.Pitch:
					return _pitch;
				default:
					return _roll;
			}
		}

		public void SetAxis(RcAxisChannel channel, RcAxisCalibration calibration)
		{
			switch (channel)
			{
				case RcAxisChannel.Throttle:
					_throttle = calibration;
					break;
				case RcAxisChannel.Yaw:
					_yaw = calibration;
					break;
				case RcAxisChannel.Pitch:
					_pitch = calibration;
					break;
				default:
					_roll = calibration;
					break;
			}
		}

		public RcSwitchCalibration GetSwitch(RcSwitchChannel channel) => channel == RcSwitchChannel.Arm ? _arm : _flightMode;

		public void SetSwitch(RcSwitchChannel channel, RcSwitchCalibration calibration)
		{
			if (channel == RcSwitchChannel.Arm)
			{
				_arm = calibration;
			}
			else
			{
				_flightMode = calibration;
			}
		}

		public FlightMode ModeAtPosition(int position)
		{
			switch (position)
			{
				case 0:
					return _modeAtRest;
				case 1:
					return _modeAtMiddle;
				default:
					return _modeAtActive;
			}
		}

		public void SetDevice(string deviceKey, string deviceName)
		{
			_deviceKey = deviceKey ?? string.Empty;
			_deviceName = deviceName ?? string.Empty;
		}

		public void SetThrottleLowDeadzone(float deadzone) => _throttleLowDeadzone = Mathf.Clamp(deadzone, 0f, 0.2f);

		public void SetModes(FlightMode atRest, FlightMode atMiddle, FlightMode atActive)
		{
			_modeAtRest = atRest;
			_modeAtMiddle = atMiddle;
			_modeAtActive = atActive;
		}

		public RcCalibration Clone() => JsonUtility.FromJson<RcCalibration>(JsonUtility.ToJson(this));
	}
}
