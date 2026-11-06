using System.Collections.Generic;
using UForce.FpvFlight.RadioInput;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UForce.FpvFlight.Editor
{
	// Radio channels, flight telemetry and the radio calibration wizard in one window.
	public sealed class FpvFlightMonitorWindow : EditorWindow
	{
		private static readonly string[] s_tabs = { "Radio", "Flight", "Calibration" };

		private readonly List<FpvFlightController> r_controllers = new();

		private int _tab;
		private Vector2 _scroll;
		private int _selectedController;
		private RcCalibrationSession _session;
		private RcCalibrationProfile _targetProfile;

		[MenuItem("Window/UForce/FPV Flight Monitor")]
		public static void Open() => GetWindow<FpvFlightMonitorWindow>("FPV Flight");

		private void OnEnable()
		{
			EditorApplication.update += HandleEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= HandleEditorUpdate;
		}

		private void HandleEditorUpdate()
		{
			_session?.Tick();
			Repaint();
		}

		private void OnGUI()
		{
			_tab = GUILayout.Toolbar(_tab, s_tabs);
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			switch (_tab)
			{
				case 0:
					DrawRadios();
					break;
				case 1:
					DrawFlight();
					break;
				default:
					DrawCalibration();
					break;
			}

			EditorGUILayout.EndScrollView();
		}

		private static void DrawRadios()
		{
			bool hasRadio = false;
			foreach (InputDevice device in InputSystem.devices)
			{
				if (!RcDevices.IsRadio(device))
				{
					continue;
				}

				hasRadio = true;
				EditorGUILayout.LabelField(device.displayName, EditorStyles.boldLabel);
				EditorGUILayout.SelectableLabel(RcDevices.GetDeviceKey(device), EditorStyles.miniLabel, GUILayout.Height(16f));

				foreach (InputControl control in device.allControls)
				{
					if (control is not AxisControl axis || control.synthetic || control.parent is StickControl stick && control != stick.x && control != stick.y)
					{
						continue;
					}

					float raw = axis.ReadUnprocessedValue();
					string path = control.path.Substring(device.path.Length + 1);
					Rect rect = EditorGUILayout.GetControlRect();
					EditorGUI.ProgressBar(rect, Mathf.InverseLerp(-1f, 1f, raw), $"{path}  {raw:+0.000;-0.000}");
				}

				EditorGUILayout.Space();
			}

			if (!hasRadio)
			{
				EditorGUILayout.HelpBox("No radio joystick found. Connect the TX12 over USB and pick \"USB Joystick (HID)\" on the radio.", MessageType.Info);
			}
		}

		private void DrawFlight()
		{
			if (!EditorApplication.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play mode to see flight telemetry.", MessageType.Info);
				return;
			}

			r_controllers.Clear();
			r_controllers.AddRange(FindObjectsByType<FpvFlightController>(FindObjectsSortMode.InstanceID));
			if (r_controllers.Count == 0)
			{
				EditorGUILayout.HelpBox("No FpvFlightController in the scene.", MessageType.Info);
				return;
			}

			string[] names = new string[r_controllers.Count];
			for (int index = 0; index < names.Length; index++)
			{
				names[index] = r_controllers[index].name;
			}

			_selectedController = Mathf.Clamp(EditorGUILayout.Popup("Drone", _selectedController, names), 0, r_controllers.Count - 1);
			FpvFlightController controller = r_controllers[_selectedController];
			if (!controller.IsInitialized)
			{
				EditorGUILayout.HelpBox("The controller is not initialized. Check its assets.", MessageType.Warning);
				return;
			}

			FlightTelemetryGui.Draw(controller.Telemetry);
		}

		private void DrawCalibration()
		{
			_targetProfile = (RcCalibrationProfile)EditorGUILayout.ObjectField("Write to profile", _targetProfile, typeof(RcCalibrationProfile), false);

			if (_session == null)
			{
				EditorGUILayout.HelpBox("Calibrates the connected radio: sticks, end points, arm switch and flight-mode switch. The result can be saved for this radio (PlayerPrefs) or written into an RcCalibrationProfile asset.", MessageType.None);
				if (GUILayout.Button("Start calibration"))
				{
					_session = new RcCalibrationSession(_targetProfile ? _targetProfile.Calibration : null);
				}

				return;
			}

			if (_session.IsDeviceLost)
			{
				EditorGUILayout.HelpBox("The radio was disconnected.", MessageType.Error);
			}

			RcCalibrationStep step = _session.Step;
			EditorGUILayout.LabelField($"Step {_session.StepIndex + 1} of {RcCalibrationSession.Steps.Count}", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(GetInstruction(step), MessageType.Info);

			if (!_session.IsComplete)
			{
				Rect rect = EditorGUILayout.GetControlRect();
				EditorGUI.ProgressBar(rect, _session.Progress, _session.DetectedName ?? "waiting for movement");

				using (new EditorGUILayout.HorizontalScope())
				{
					using (new EditorGUI.DisabledScope(!_session.CanContinue))
					{
						if (GUILayout.Button("Continue"))
						{
							_session.Continue();
						}
					}

					using (new EditorGUI.DisabledScope(!step.IsOptional))
					{
						if (GUILayout.Button("Skip"))
						{
							_session.Skip();
						}
					}
				}
			}
			else
			{
				DrawSummary(_session.Result);
			}

			if (GUILayout.Button("Cancel"))
			{
				_session = null;
			}
		}

		private void DrawSummary(RcCalibration result)
		{
			EditorGUILayout.LabelField(result.DeviceName, EditorStyles.boldLabel);
			for (int index = 0; index < 4; index++)
			{
				var channel = (RcAxisChannel)index;
				RcAxisCalibration axis = result.GetAxis(channel);
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(channel.ToString(), $"{axis.ControlPath}  [{axis.Min:0.00} {axis.Center:0.00} {axis.Max:0.00}]{(axis.IsInverted ? " inverted" : string.Empty)}");
					if (GUILayout.Button("Invert", GUILayout.Width(60f)))
					{
						_session.ToggleInvert(channel);
					}
				}
			}

			EditorGUILayout.LabelField("Arm", string.IsNullOrEmpty(result.ArmSwitch.ControlPath) ? "not set" : result.ArmSwitch.ControlPath);
			EditorGUILayout.LabelField("Flight mode", string.IsNullOrEmpty(result.FlightModeSwitch.ControlPath) ? "not set" : result.FlightModeSwitch.ControlPath);

			if (GUILayout.Button("Save for this radio (PlayerPrefs)"))
			{
				new PlayerPrefsRcCalibrationStore().Save(result);
				Debug.Log($"[{nameof(FpvFlightMonitorWindow)}] Saved calibration for {result.DeviceName}.");
			}

			using (new EditorGUI.DisabledScope(!_targetProfile))
			{
				if (GUILayout.Button("Write into profile asset"))
				{
					Undo.RecordObject(_targetProfile, "Write radio calibration");
					_targetProfile.SetCalibration(result);
					EditorUtility.SetDirty(_targetProfile);
					AssetDatabase.SaveAssetIfDirty(_targetProfile);
				}
			}
		}

		private static string GetInstruction(RcCalibrationStep step)
		{
			switch (step.Kind)
			{
				case RcCalibrationStepKind.Axis:
					switch (step.Axis)
					{
						case RcAxisChannel.Throttle:
							return "Throttle: move the throttle stick fully up, then fully down, and leave it at the bottom.";
						case RcAxisChannel.Yaw:
							return "Yaw: move the yaw stick to the RIGHT first, then fully left, then let it center.";
						case RcAxisChannel.Pitch:
							return "Pitch: push the pitch stick FORWARD (up) first, then fully back, then let it center.";
						default:
							return "Roll: move the roll stick to the RIGHT first, then fully left, then let it center.";
					}
				case RcCalibrationStepKind.Range:
					return "Circle both sticks along their full travel a few times.";
				case RcCalibrationStepKind.Switch:
					return step.Switch == RcSwitchChannel.Arm
						? "Arm switch: flip it to the ARMED position and leave it there."
						: "Flight mode switch (optional): flip it to the ACRO position and leave it there.";
				default:
					return "Done. Check the result, then save it for this radio or write it into a profile asset.";
			}
		}
	}
}
