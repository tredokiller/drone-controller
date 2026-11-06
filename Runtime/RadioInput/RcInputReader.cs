using System;
using UForce.FpvFlight.Rc;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UForce.FpvFlight.RadioInput
{
	// Turns the six InputActions into an RcCommand once per frame. A connected radio joystick is used exclusively;
	// otherwise gamepad and keyboard drive debug virtual sticks. Toggles become held levels, so physics code
	// running in FixedUpdate only reads Current and never misses or doubles a button press.
	public sealed class RcInputReader
	{
		private readonly RcInputActions r_actions;
		private readonly RcCalibrationProfile r_profile;
		private readonly IRcCalibrationStore r_store;
		private readonly RcDebugInputSettings r_fallbackDebugSettings = new();

		private InputDevice _radio;
		private RcCalibration _calibration;
		private AxisControl _throttleControl;
		private AxisControl _yawControl;
		private AxisControl _pitchControl;
		private AxisControl _rollControl;
		private AxisControl _armControl;
		private AxisControl _modeControl;

		private float _virtualRoll;
		private float _virtualPitch;
		private float _virtualYaw;
		private float _virtualThrottle;
		private bool _isArmLatched;
		private FlightMode _debugMode;

		public RcInputReader(RcInputActions actions, RcCalibrationProfile profile, IRcCalibrationStore store = null)
		{
			r_actions = actions ?? throw new ArgumentNullException(nameof(actions));
			r_profile = profile;
			r_store = store;
			Reset();
		}

		public RcCommand Current { get; private set; }
		public RcInputSource Source { get; private set; }
		public InputDevice RadioDevice => _radio;
		public bool IsRadioConnected => _radio != null;
		public RcCalibration Calibration => _calibration;

		private RcDebugInputSettings DebugSettings => r_profile ? r_profile.DebugInput : r_fallbackDebugSettings;

		public void Update(float deltaTime)
		{
			UpdateRadioDevice();

			if (_radio != null)
			{
				Source = RcInputSource.Radio;
				Current = ReadRadio();
				return;
			}

			Current = ReadDebugDevices(deltaTime);
		}

		public void Reset()
		{
			_virtualRoll = 0f;
			_virtualPitch = 0f;
			_virtualYaw = 0f;
			_virtualThrottle = 0f;
			_isArmLatched = false;
			_debugMode = DebugSettings.DefaultMode;
			Source = RcInputSource.None;
			Current = new RcCommand(0f, 0f, 0f, 0f, false, _debugMode);
		}

		// Replaces the calibration of the connected radio, for example right after the calibration wizard.
		public void ApplyCalibration(RcCalibration calibration)
		{
			if (calibration == null)
			{
				return;
			}

			_calibration = calibration.Clone();
			if (_radio != null)
			{
				ResolveRadioControls();
			}
		}

		// Forgets the current radio; it is picked up again (with a fresh calibration lookup) on the next Update.
		public void ReleaseRadio()
		{
			_radio = null;
			ClearRadioControls();
		}

		private void UpdateRadioDevice()
		{
			if (_radio != null && !_radio.added)
			{
				ReleaseRadio();
				_isArmLatched = false;
			}

			if (_radio != null)
			{
				return;
			}

			InputDevice device = FindRadioDevice();
			if (device == null)
			{
				return;
			}

			_radio = device;
			_calibration = r_profile ? r_profile.Resolve(device, r_store) : LoadOrCreateCalibration(device);
			ResolveRadioControls();
			_isArmLatched = false;
		}

		private RcCalibration LoadOrCreateCalibration(InputDevice device)
		{
			if (r_store != null && r_store.TryLoad(RcDevices.GetDeviceKey(device), out RcCalibration saved))
			{
				return saved;
			}

			return new RcCalibration();
		}

		private InputDevice FindRadioDevice()
		{
			InputDevice device = FindRadioDeviceIn(r_actions.Throttle)
				?? FindRadioDeviceIn(r_actions.Roll)
				?? FindRadioDeviceIn(r_actions.Pitch)
				?? FindRadioDeviceIn(r_actions.Yaw);

			if (device != null)
			{
				return device;
			}

			// No joystick bindings in the actions: a calibration with explicit control paths still selects the first joystick.
			bool hasExplicitPaths = r_profile && !string.IsNullOrEmpty(r_profile.Calibration.Throttle.ControlPath);
			if (hasExplicitPaths && Joystick.current != null && Joystick.current.added)
			{
				return Joystick.current;
			}

			return null;
		}

		private static InputDevice FindRadioDeviceIn(InputAction action)
		{
			if (action == null)
			{
				return null;
			}

			var controls = action.controls;
			for (int index = 0; index < controls.Count; index++)
			{
				InputDevice device = controls[index].device;
				if (device.added && RcDevices.IsRadio(device))
				{
					return device;
				}
			}

			return null;
		}

		private void ResolveRadioControls()
		{
			_throttleControl = ResolveControl(_calibration.Throttle.ControlPath, r_actions.Throttle);
			_yawControl = ResolveControl(_calibration.Yaw.ControlPath, r_actions.Yaw);
			_pitchControl = ResolveControl(_calibration.Pitch.ControlPath, r_actions.Pitch);
			_rollControl = ResolveControl(_calibration.Roll.ControlPath, r_actions.Roll);
			_armControl = ResolveControl(_calibration.ArmSwitch.ControlPath, r_actions.Arm);
			_modeControl = ResolveControl(_calibration.FlightModeSwitch.ControlPath, r_actions.FlightMode);

			if (_throttleControl == null || _yawControl == null || _pitchControl == null || _rollControl == null)
			{
				Debug.LogWarning($"[{nameof(RcInputReader)}] Radio '{_radio.displayName}' is missing a stick control. Check the joystick bindings or the calibration control paths.");
			}

			if (_armControl == null)
			{
				Debug.LogWarning($"[{nameof(RcInputReader)}] Radio '{_radio.displayName}' has no arm switch control, so it cannot arm.");
			}
		}

		private AxisControl ResolveControl(string controlPath, InputAction action)
		{
			if (!string.IsNullOrEmpty(controlPath))
			{
				return _radio.TryGetChildControl(controlPath) as AxisControl;
			}

			if (action == null)
			{
				return null;
			}

			var controls = action.controls;
			for (int index = 0; index < controls.Count; index++)
			{
				if (controls[index].device == _radio && controls[index] is AxisControl axis)
				{
					return axis;
				}
			}

			return null;
		}

		private void ClearRadioControls()
		{
			_throttleControl = null;
			_yawControl = null;
			_pitchControl = null;
			_rollControl = null;
			_armControl = null;
			_modeControl = null;
		}

		private RcCommand ReadRadio()
		{
			RcCalibration calibration = _calibration;

			float throttle = 0f;
			if (_throttleControl != null)
			{
				float normalized = (calibration.Throttle.Normalize(_throttleControl.ReadUnprocessedValue()) + 1f) * 0.5f;
				throttle = ApplyLowDeadzone(normalized, calibration.ThrottleLowDeadzone);
			}

			float yaw = _yawControl != null ? calibration.Yaw.Normalize(_yawControl.ReadUnprocessedValue()) : 0f;
			float pitch = _pitchControl != null ? calibration.Pitch.Normalize(_pitchControl.ReadUnprocessedValue()) : 0f;
			float roll = _rollControl != null ? calibration.Roll.Normalize(_rollControl.ReadUnprocessedValue()) : 0f;

			bool isArmSwitchOn = _armControl != null
				&& calibration.ArmSwitch.GetPosition(_armControl.ReadUnprocessedValue()) == calibration.ArmActivePosition;

			int modePosition = _modeControl != null ? calibration.FlightModeSwitch.GetPosition(_modeControl.ReadUnprocessedValue()) : 0;
			FlightMode mode = calibration.ModeAtPosition(modePosition);

			return new RcCommand(roll, pitch, yaw, throttle, isArmSwitchOn, mode);
		}

		private RcCommand ReadDebugDevices(float deltaTime)
		{
			RcDebugInputSettings settings = DebugSettings;

			if (r_actions.Arm != null && r_actions.Arm.WasPressedThisFrame())
			{
				_isArmLatched = !_isArmLatched;
			}

			if (r_actions.FlightMode != null && r_actions.FlightMode.WasPressedThisFrame())
			{
				_debugMode = _debugMode == FlightMode.Acro ? FlightMode.Angle : FlightMode.Acro;
			}

			float roll = ReadDebugStick(r_actions.Roll, ref _virtualRoll, settings, deltaTime);
			float pitch = ReadDebugStick(r_actions.Pitch, ref _virtualPitch, settings, deltaTime);
			float yaw = ReadDebugStick(r_actions.Yaw, ref _virtualYaw, settings, deltaTime);
			float throttle = ReadDebugThrottle(r_actions.Throttle, settings, deltaTime);

			return new RcCommand(roll, pitch, yaw, throttle, _isArmLatched, _debugMode);
		}

		private float ReadDebugStick(InputAction action, ref float virtualStick, RcDebugInputSettings settings, float deltaTime)
		{
			if (action == null)
			{
				return 0f;
			}

			float value = action.ReadValue<float>();
			InputDevice device = action.activeControl?.device;

			if (device is Gamepad)
			{
				Source = RcInputSource.Gamepad;
				virtualStick = 0f;
				return settings.ShapeGamepadStick(value);
			}

			bool isKeyboard = device is Keyboard;
			if (isKeyboard)
			{
				Source = RcInputSource.Keyboard;
			}

			// Keys ramp a virtual stick toward full deflection and let it spring back, like a radio stick.
			float target = isKeyboard ? Mathf.Clamp(value, -1f, 1f) : 0f;
			float speed = Mathf.Approximately(target, 0f) ? settings.KeyboardStickReturnSpeed : settings.KeyboardStickAcceleration;
			virtualStick = Mathf.MoveTowards(virtualStick, target, speed * deltaTime);
			return virtualStick;
		}

		private float ReadDebugThrottle(InputAction action, RcDebugInputSettings settings, float deltaTime)
		{
			if (action == null)
			{
				return _virtualThrottle;
			}

			float value = action.ReadValue<float>();
			InputDevice device = action.activeControl?.device;
			if (device is Gamepad)
			{
				Source = RcInputSource.Gamepad;
				value = settings.ShapeGamepadStick(value);
			}
			else if (device is Keyboard)
			{
				Source = RcInputSource.Keyboard;
			}
			else
			{
				value = 0f;
			}

			// The throttle moves while the key or stick is held and stays where it is left.
			_virtualThrottle = Mathf.Clamp01(_virtualThrottle + Mathf.Clamp(value, -1f, 1f) * settings.ThrottleSpeed * deltaTime);
			return _virtualThrottle;
		}

		private static float ApplyLowDeadzone(float throttle, float deadzone)
		{
			if (deadzone <= 0f)
			{
				return Mathf.Clamp01(throttle);
			}

			return Mathf.InverseLerp(deadzone, 1f, throttle);
		}
	}
}
