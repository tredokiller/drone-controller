using UForce.FpvFlight.Crash;
using UForce.FpvFlight.RadioInput;
using UForce.FpvFlight.Rc;
using UForce.FpvFlight.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
using UForce.FpvFlight.Osd;
using UForce.FpvFlight.Osd.UI;
using UForce.FpvFlight.Video;
#endif

namespace UForce.FpvFlight.Samples
{
	// The wiring a project needs: read the radio once per frame, tick the flight core once per physics step, and route
	// the radio's other channels. Spawns a drone type (keys 1-5) and puts it back on the ground (R), as Interceptor Drones'
	// restart does. Camera tilt follows switch SC (or cycles on V / left trigger), the payload switch SF (B / LB) arms the
	// payload and blocks motor arming, detonate SD (G / RT) ends the flight, thermal SA (T / RB) cycles day / white hot / colour.
	// O opens the OSD editor over the live video with time frozen; Shift+O cycles the OSD mode (standard, own, off).
	public sealed class FpvFlightDemoDriver : MonoBehaviour
	{
		private const float DETONATE_RESPAWN_DELAY = 2f;

		[SerializeField] private InputActionAsset _actions;
		[SerializeField] private string _actionMapName = "Drone";
		[SerializeField] private RcCalibrationProfile _calibration;
		[Tooltip("Load a per-radio calibration saved by the calibration wizard.")]
		[SerializeField] private bool _isSavedCalibrationUsed = true;
		[SerializeField] private FpvDroneType[] _types;
		[SerializeField] private int _startType;
		[SerializeField] private Transform _spawnPoint;

		private RcInputActions _rcActions;
		private RcInputReader _reader;
		private GameObject _drone;
		private FpvFlightController _controller;
		private FpvCameraTilt _cameraTilt;
		private FpvCrashDetector _crashDetector;
		private int _typeIndex = -1;
		private int _handledTiltCycles;
		private int _handledDetonations;
		private int _handledThermalCycles;
		private float _respawnTime = -1f;
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
		private FpvVideoCamera _video;
		private FpvOsdFlightSource _osd;
		private FpvOsdView _osdView;
		private FpvOsdEditorScreen _osdEditor;
		private float _timeScaleBeforeEditor = 1f;

		private FpvOsdMode _osdMode;

		public bool IsOsdEditorOpen => _osdEditor && _osdEditor.IsOpen;
		public FpvOsdMode OsdMode => _osdMode;
#else
		public bool IsOsdEditorOpen => false;
#endif

		public RcInputReader Reader => _reader;
		public FpvFlightController Controller => _controller;
		public FpvDroneType CurrentType => _typeIndex >= 0 && _types != null && _typeIndex < _types.Length ? _types[_typeIndex] : null;
		public bool IsCrashed => _crashDetector && _crashDetector.IsCrashed;
		public bool IsDetonated => _respawnTime >= 0f;

		private void Awake()
		{
			InputActionMap map = _actions ? _actions.FindActionMap(_actionMapName, true) : null;
			if (map == null)
			{
				Debug.LogError($"[{nameof(FpvFlightDemoDriver)}] Assign an InputActionAsset with a '{_actionMapName}' map.", this);
				enabled = false;
				return;
			}

			_rcActions = RcInputActions.FromMap(map);
			_reader = new RcInputReader(_rcActions, _calibration, _isSavedCalibrationUsed ? new PlayerPrefsRcCalibrationStore() : null);
		}

		private void Start()
		{
			Spawn(Mathf.Clamp(_startType, 0, _types != null ? _types.Length - 1 : 0));
		}

		private void OnEnable()
		{
			_rcActions?.Enable();
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			_osdMode = PlayerPrefsFpvOsdSettingsStore.Default.Load().Mode;
			PlayerPrefsFpvOsdSettingsStore.Default.Changed += HandleOsdSettingsChanged;
#endif
		}

		private void OnDisable()
		{
			_rcActions?.Disable();
			_reader?.Reset();
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			PlayerPrefsFpvOsdSettingsStore.Default.Changed -= HandleOsdSettingsChanged;
#endif
		}

		private void Update()
		{
			_reader.Update(Time.unscaledDeltaTime);
			HandleDemoKeys();
			if (_controller == null)
			{
				return;
			}

			RouteAux(_reader.Aux);

			if (_respawnTime >= 0f && Time.time >= _respawnTime)
			{
				_respawnTime = -1f;
				Respawn();
			}
		}

		private void FixedUpdate()
		{
			if (_controller != null)
			{
				_controller.Tick(_reader.Current, Time.fixedDeltaTime);
			}
		}

		public void Spawn(int typeIndex)
		{
			if (_types == null || typeIndex < 0 || typeIndex >= _types.Length || !_types[typeIndex] || !_types[typeIndex].Prefab)
			{
				return;
			}

			if (_drone)
			{
				Destroy(_drone);
			}

			FpvDroneType type = _types[typeIndex];
			_typeIndex = typeIndex;
			Pose pose = SpawnPose();
			_drone = Instantiate(type.Prefab, pose.position, pose.rotation);
			_drone.name = type.DisplayName;
			_controller = _drone.GetComponent<FpvFlightController>();
			type.Apply(_controller);
			_cameraTilt = _drone.GetComponentInChildren<FpvCameraTilt>(true);
			_crashDetector = _drone.GetComponent<FpvCrashDetector>();
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			_video = _drone.GetComponentInChildren<FpvVideoCamera>(true);
			_osd = _drone.GetComponentInChildren<FpvOsdFlightSource>(true);
			_osdView = null;
#endif
			// Presses made before this drone existed must not act on it.
			RcAuxState aux = _reader.Aux;
			_handledTiltCycles = aux.CameraTiltCycleCount;
			_handledDetonations = aux.DetonateCount;
			_handledThermalCycles = aux.ThermalCycleCount;
			_respawnTime = -1f;
			_controller.Recover(pose.position, pose.rotation);
		}

		public void Respawn()
		{
			if (_controller == null)
			{
				return;
			}

			if (_drone && !_drone.activeSelf)
			{
				_drone.SetActive(true);
			}

			_respawnTime = -1f;
			Pose pose = SpawnPose();
			_controller.Recover(pose.position, pose.rotation);
		}

		private Pose SpawnPose() => _spawnPoint ? new Pose(_spawnPoint.position, _spawnPoint.rotation) : new Pose(new Vector3(0f, 0.5f, 0f), Quaternion.identity);

		private void HandleDemoKeys()
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return;
			}

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			if (IsOsdEditorOpen)
			{
				if (keyboard.escapeKey.wasPressedThisFrame)
				{
					_osdEditor.Close();
				}

				return;
			}

			if (keyboard.oKey.wasPressedThisFrame)
			{
				if (keyboard.shiftKey.isPressed)
				{
					CycleOsdMode();
				}
				else
				{
					OpenOsdEditor();
				}
			}
#endif

			for (int index = 0; index < 5; index++)
			{
				if (keyboard[Key.Digit1 + index].wasPressedThisFrame)
				{
					Spawn(index);
				}
			}

			if (keyboard.rKey.wasPressedThisFrame)
			{
				Respawn();
			}
		}

		private void RouteAux(in RcAuxState aux)
		{
			if (_cameraTilt)
			{
				if (aux.CameraTiltPosition != RcAuxState.NO_POSITION)
				{
					_cameraTilt.SetPosition(aux.CameraTiltPosition);
				}
				else if (aux.CameraTiltCycleCount != _handledTiltCycles)
				{
					_cameraTilt.Cycle();
				}
			}

			_handledTiltCycles = aux.CameraTiltCycleCount;
			_controller.SetArmingInhibit(ArmingInhibit.PayloadArmed, aux.IsPayloadArmed);

			if (aux.DetonateCount != _handledDetonations && aux.IsPayloadArmed && _respawnTime < 0f)
			{
				// No warhead in the demo: the drone is lost and comes back after a moment, as Interceptor's bomb restart.
				_controller.ForceDisarm();
				_drone.SetActive(false);
				_respawnTime = Time.time + DETONATE_RESPAWN_DELAY;
			}

			_handledDetonations = aux.DetonateCount;

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			if (_osd)
			{
				_osd.SetPayload(true, aux.IsPayloadArmed);
			}

			if (_video && aux.ThermalCycleCount != _handledThermalCycles)
			{
				_video.SetMode(NextThermalMode(_video.Mode));
			}
#endif
			_handledThermalCycles = aux.ThermalCycleCount;
		}

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
		// The editor shows the drone's own video and OSD; the flight stands still while the pilot arranges the OSD.
		private void OpenOsdEditor()
		{
			if (!_osdView && _drone)
			{
				_osdView = _drone.GetComponentInChildren<FpvOsdView>(true);
			}

			if (!_osdView || !_video)
			{
				return;
			}

			if (!_osdEditor)
			{
				_osdEditor = FpvOsdEditorScreen.Create();
				_osdEditor.Closed += HandleOsdEditorClosed;
			}

			_timeScaleBeforeEditor = Time.timeScale;
			Time.timeScale = 0f;
			_osdEditor.Open(_osdView, _video.Camera);
		}

		private void HandleOsdEditorClosed(bool isSaved)
		{
			Time.timeScale = _timeScaleBeforeEditor;
		}

		private void HandleOsdSettingsChanged(FpvOsdSettings settings)
		{
			_osdMode = settings.Mode;
		}

		// Standard → own → off; the own layout is skipped until one is saved, as in FPV Defence Simulator.
		private static void CycleOsdMode()
		{
			PlayerPrefsFpvOsdSettingsStore store = PlayerPrefsFpvOsdSettingsStore.Default;
			FpvOsdSettings settings = store.Load();
			settings.Mode = settings.Mode switch
			{
				FpvOsdMode.Main => settings.CustomLayout.IsEmpty ? FpvOsdMode.Off : FpvOsdMode.Custom,
				FpvOsdMode.Custom => FpvOsdMode.Off,
				_ => FpvOsdMode.Main
			};

			store.Save(settings);
		}

		// Interceptor's thermal button: off → white hot → colour → off.
		private static FpvVideoCameraMode NextThermalMode(FpvVideoCameraMode mode)
		{
			switch (mode)
			{
				case FpvVideoCameraMode.ThermalWhiteHot:
					return FpvVideoCameraMode.ThermalColor;
				case FpvVideoCameraMode.ThermalColor:
					return FpvVideoCameraMode.Day;
				default:
					return FpvVideoCameraMode.ThermalWhiteHot;
			}
		}
#endif
	}
}
