using System;
using UForce.FpvFlight.Rc;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UForce.FpvFlight.RadioInput
{
	// Turns the radio InputActions into an RcCommand and an RcAuxState once per frame. A connected radio joystick is used
	// exclusively; otherwise gamepad and keyboard drive debug virtual sticks. Toggles become held levels and button presses
	// become counters, so physics code running in FixedUpdate never misses or doubles a press.
	public sealed class RcInputReader
	{
		private readonly RcInputActions r_actions;
		private readonly RcCalibrationProfile r_profile;
		private readonly IRcCalibrationStore r_store;
		private readonly RcDebugInputSettings r_fallbackDebugSettings = new();
		private readonly AxisControl[] r_switchControls = new AxisControl[RcSwitchChannels.COUNT];
		private readonly int[] r_switchPositions = new int[RcSwitchChannels.COUNT];

		private InputDevice _radio;
		private RcCalibration _calibration;
		private AxisControl _throttleControl;
		private AxisControl _yawControl;
		private AxisControl _pitchControl;
		private AxisControl _rollControl;

		private float _virtualRoll;
		private float _virtualPitch;
		private float _virtualYaw;
		private float _virtualThrottle;
		private bool _isArmLatched;
		private bool _isArmRequested;
		private bool _isPayloadArmLatched;
		private FlightMode _debugMode;
		private int _detonateCount;
		private int _cameraTiltCycleCount;
		private int _thermalCycleCount;
		private bool _isSuspended;

		public RcInputReader(RcInputActions actions, RcCalibrationProfile profile, IRcCalibrationStore store = null)
		{
			r_actions = actions ?? throw new ArgumentNullException(nameof(actions));
			r_profile = profile;
			r_store = store;
			Reset();
		}

		// A radio switch moved to a new position, or a radio or debug button was pressed (position 2).
		public event Action<RcSwitchChannel, int> SwitchChanged;

		public RcCommand Current { get; private set; }
		public RcAuxState Aux { get; private set; }
		public RcInputSource Source { get; private set; }
		public InputDevice RadioDevice => _radio;
		public bool IsRadioConnected => _radio != null;
		public RcCalibration Calibration => _calibration;
		public bool IsSuspended => _isSuspended;

		private RcDebugInputSettings DebugSettings => r_profile ? r_profile.DebugInput : r_fallbackDebugSettings;

		public void Update(float deltaTime)
		{
			if (_isSuspended)
			{
				return;
			}

			UpdateRadioDevice();

			if (_radio != null)
			{
				Source = RcInputSource.Radio;
				Current = ReadRadio();
				Aux = ReadRadioAux();
				return;
			}

			Current = ReadDebugDevices(deltaTime);
			Aux = ReadDebugAux();
		}

		public void Reset()
		{
			_virtualRoll = 0f;
			_virtualPitch = 0f;
			_virtualYaw = 0f;
			_virtualThrottle = 0f;
			_isArmLatched = false;
			_isArmRequested = false;
			_isPayloadArmLatched = false;
			_debugMode = DebugSettings.DefaultMode;
			Source = RcInputSource.None;
			Current = new RcCommand(0f, 0f, 0f, 0f, false, _debugMode);
			// Press counters keep growing across resets, so a consumer never mistakes a reset for a press.
			Aux = new RcAuxState(false, _detonateCount, RcAuxState.NO_POSITION, _cameraTiltCycleCount, _thermalCycleCount);
			ForgetSwitchPositions();
		}

		// While suspended (for example during calibration) the reader outputs a disarmed, centred command.
		public void SetSuspended(bool isSuspended)
		{
			if (_isSuspended == isSuspended)
			{
				return;
			}

			_isSuspended = isSuspended;
			Reset();
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
				_isArmRequested = false;
				_isPayloadArmLatched = false;
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

			for (int index = 0; index < RcSwitchChannels.COUNT; index++)
			{
				var channel = (RcSwitchChannel)index;
				r_switchControls[index] = ResolveControl(_calibration.GetSwitch(channel).ControlPath, r_actions.GetSwitchAction(channel));
			}

			ForgetSwitchPositions();

			if (_throttleControl == null || _yawControl == null || _pitchControl == null || _rollControl == null)
			{
				Debug.LogWarning($"[{nameof(RcInputReader)}] Radio '{_radio.displayName}' is missing a stick control. Check the joystick bindings or the calibration control paths.");
			}

			if (r_switchControls[(int)RcSwitchChannel.Arm] == null)
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
			Array.Clear(r_switchControls, 0, r_switchControls.Length);
			ForgetSwitchPositions();
		}

		private void ForgetSwitchPositions()
		{
			for (int index = 0; index < r_switchPositions.Length; index++)
			{
				r_switchPositions[index] = RcAuxState.NO_POSITION;
			}
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

			bool isArmSwitchOn = ReadSwitchPosition(RcSwitchChannel.Arm) == calibration.ArmActivePosition;
			int modePosition = ReadSwitchPosition(RcSwitchChannel.FlightMode);
			FlightMode mode = calibration.ModeAtPosition(Mathf.Max(0, modePosition));

			return new RcCommand(roll, pitch, yaw, throttle, isArmSwitchOn, mode);
		}

		// Interceptor Drones' function rules: a latching payload switch is armed in its active position, a momentary one
		// toggles; detonate fires on entering the active position; camera tilt follows a latching switch or cycles on a
		// button; thermal advances on every change of a latching switch or on every press of a button.
		private RcAuxState ReadRadioAux()
		{
			RcCalibration calibration = _calibration;

			int payloadPosition = ReadSwitchPosition(RcSwitchChannel.PayloadArm, out bool isPayloadChanged);
			if (calibration.GetSwitch(RcSwitchChannel.PayloadArm).IsLatching)
			{
				_isPayloadArmLatched = payloadPosition == calibration.PayloadArmActivePosition;
			}
			else if (isPayloadChanged && payloadPosition == RcSwitchCalibration.ACTIVE_POSITION)
			{
				_isPayloadArmLatched = !_isPayloadArmLatched;
			}

			int detonatePosition = ReadSwitchPosition(RcSwitchChannel.PayloadDetonate, out bool isDetonateChanged);
			if (isDetonateChanged && detonatePosition == RcSwitchCalibration.ACTIVE_POSITION)
			{
				_detonateCount++;
			}

			int tiltPosition = ReadSwitchPosition(RcSwitchChannel.CameraTilt, out bool isTiltChanged);
			bool isTiltLatching = calibration.GetSwitch(RcSwitchChannel.CameraTilt).IsLatching && r_switchControls[(int)RcSwitchChannel.CameraTilt] != null;
			if (!isTiltLatching && isTiltChanged && tiltPosition == RcSwitchCalibration.ACTIVE_POSITION)
			{
				_cameraTiltCycleCount++;
			}

			int thermalPosition = ReadSwitchPosition(RcSwitchChannel.Thermal, out bool isThermalChanged);
			bool isThermalLatching = calibration.GetSwitch(RcSwitchChannel.Thermal).IsLatching;
			if (isThermalChanged && (isThermalLatching || thermalPosition == RcSwitchCalibration.ACTIVE_POSITION))
			{
				_thermalCycleCount++;
			}

			return new RcAuxState(
				_isPayloadArmLatched,
				_detonateCount,
				isTiltLatching ? tiltPosition : RcAuxState.NO_POSITION,
				_cameraTiltCycleCount,
				_thermalCycleCount);
		}

		private int ReadSwitchPosition(RcSwitchChannel channel) => ReadSwitchPosition(channel, out _);

		// The switch's position 0..2, or NO_POSITION without a control. The first reading after a (re)connect is never a change.
		private int ReadSwitchPosition(RcSwitchChannel channel, out bool isChanged)
		{
			int index = (int)channel;
			AxisControl control = r_switchControls[index];
			if (control == null)
			{
				isChanged = false;
				return RcAuxState.NO_POSITION;
			}

			int position = _calibration.GetSwitch(channel).GetPosition(control.ReadUnprocessedValue());
			int previous = r_switchPositions[index];
			r_switchPositions[index] = position;
			isChanged = previous != RcAuxState.NO_POSITION && previous != position;

			if (isChanged)
			{
				SwitchChanged?.Invoke(channel, position);
			}

			return position;
		}

		// As Interceptor Drones: while a flight key is held the keyboard's virtual sticks fly, otherwise the gamepad sticks.
		// Throttle follows the stick position (a released gamepad stick is mid throttle). The arm button requests arming;
		// the request becomes the arm switch once throttle is at the bottom, and pressing again disarms.
		private RcCommand ReadDebugDevices(float deltaTime)
		{
			RcDebugInputSettings settings = DebugSettings;

			if (WasPressed(RcSwitchChannel.FlightMode))
			{
				_debugMode = NextMode(_debugMode);
			}

			float keyboardRoll = ReadKeyboard(r_actions.Roll);
			float keyboardPitch = ReadKeyboard(r_actions.Pitch);
			float keyboardYaw = ReadKeyboard(r_actions.Yaw);
			float keyboardThrottle = ReadKeyboard(r_actions.Throttle);
			_virtualRoll = MoveVirtualStick(_virtualRoll, keyboardRoll, settings, deltaTime);
			_virtualPitch = MoveVirtualStick(_virtualPitch, keyboardPitch, settings, deltaTime);
			_virtualYaw = MoveVirtualStick(_virtualYaw, keyboardYaw, settings, deltaTime);
			_virtualThrottle = MoveVirtualStick(_virtualThrottle, keyboardThrottle, settings, deltaTime);

			bool isKeyboardFlying = keyboardRoll != 0f || keyboardPitch != 0f || keyboardYaw != 0f || keyboardThrottle != 0f;
			bool hasGamepad = HasGamepad(r_actions.Throttle) || HasGamepad(r_actions.Roll);

			float roll;
			float pitch;
			float yaw;
			float throttleStick;
			if (isKeyboardFlying || !hasGamepad)
			{
				Source = isKeyboardFlying || Source == RcInputSource.None ? RcInputSource.Keyboard : Source;
				roll = _virtualRoll;
				pitch = _virtualPitch;
				yaw = _virtualYaw;
				throttleStick = _virtualThrottle;
			}
			else
			{
				Source = RcInputSource.Gamepad;
				roll = ReadGamepad(r_actions.Roll, settings);
				pitch = ReadGamepad(r_actions.Pitch, settings);
				yaw = ReadGamepad(r_actions.Yaw, settings);
				throttleStick = ReadGamepad(r_actions.Throttle, settings);
			}

			float throttle = Mathf.Clamp01((throttleStick + 1f) * 0.5f);

			if (WasPressed(RcSwitchChannel.Arm))
			{
				_isArmRequested = !_isArmRequested;
			}

			if (!_isArmRequested)
			{
				_isArmLatched = false;
			}
			else if (!_isArmLatched && throttle <= settings.ArmThrottleMax)
			{
				_isArmLatched = true;
			}

			return new RcCommand(roll, pitch, yaw, throttle, _isArmLatched, _debugMode);
		}

		// Gamepad and keyboard: payload arm toggles, detonate / camera tilt / thermal count presses (camera tilt cycles).
		private RcAuxState ReadDebugAux()
		{
			if (WasPressed(RcSwitchChannel.PayloadArm))
			{
				_isPayloadArmLatched = !_isPayloadArmLatched;
			}

			if (WasPressed(RcSwitchChannel.PayloadDetonate))
			{
				_detonateCount++;
			}

			if (WasPressed(RcSwitchChannel.CameraTilt))
			{
				_cameraTiltCycleCount++;
			}

			if (WasPressed(RcSwitchChannel.Thermal))
			{
				_thermalCycleCount++;
			}

			return new RcAuxState(_isPayloadArmLatched, _detonateCount, RcAuxState.NO_POSITION, _cameraTiltCycleCount, _thermalCycleCount);
		}

		private bool WasPressed(RcSwitchChannel channel)
		{
			InputAction action = r_actions.GetSwitchAction(channel);
			if (action == null || !action.WasPressedThisFrame())
			{
				return false;
			}

			SwitchChanged?.Invoke(channel, RcSwitchCalibration.ACTIVE_POSITION);
			return true;
		}

		// Angle → Horizon → Acro → Angle, the order of the radio's 3-position mode switch.
		public static FlightMode NextMode(FlightMode mode)
		{
			switch (mode)
			{
				case FlightMode.Angle:
					return FlightMode.Horizon;
				case FlightMode.Horizon:
					return FlightMode.Acro;
				default:
					return FlightMode.Angle;
			}
		}

		private static float MoveVirtualStick(float current, float target, RcDebugInputSettings settings, float deltaTime)
		{
			float speed = Mathf.Approximately(target, 0f) ? settings.KeyboardStickReturnSpeed : settings.KeyboardStickAcceleration;
			return Mathf.MoveTowards(current, Mathf.Clamp(target, -1f, 1f), speed * deltaTime);
		}

		// The action's value while the keyboard drives it, 0 otherwise.
		private static float ReadKeyboard(InputAction action)
		{
			if (action == null || action.activeControl == null || action.activeControl.device is not Keyboard)
			{
				return 0f;
			}

			return Mathf.Clamp(action.ReadValue<float>(), -1f, 1f);
		}

		// The most deflected gamepad stick bound to the action, shaped with deadzone and curve.
		private static float ReadGamepad(InputAction action, RcDebugInputSettings settings)
		{
			if (action == null)
			{
				return 0f;
			}

			float value = 0f;
			var controls = action.controls;
			for (int index = 0; index < controls.Count; index++)
			{
				if (controls[index] is not AxisControl axis || axis.device is not Gamepad || !axis.device.added)
				{
					continue;
				}

				float shaped = settings.ShapeGamepadStick(axis.ReadUnprocessedValue());
				if (Mathf.Abs(shaped) > Mathf.Abs(value))
				{
					value = shaped;
				}
			}

			return value;
		}

		private static bool HasGamepad(InputAction action)
		{
			if (action == null)
			{
				return false;
			}

			var controls = action.controls;
			for (int index = 0; index < controls.Count; index++)
			{
				if (controls[index].device is Gamepad && controls[index].device.added)
				{
					return true;
				}
			}

			return false;
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
