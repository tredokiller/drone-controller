using UForce.FpvFlight.RadioInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UForce.FpvFlight.Samples
{
	// The minimal wiring a project needs: read the radio once per frame, tick the flight core once per physics step.
	[RequireComponent(typeof(FpvFlightController))]
	public sealed class FpvFlightDemoDriver : MonoBehaviour
	{
		[SerializeField] private InputActionAsset _actions;
		[SerializeField] private string _actionMapName = "Drone";
		[SerializeField] private RcCalibrationProfile _calibration;
		[Tooltip("Load a per-radio calibration saved by Window > UForce > FPV Flight Monitor.")]
		[SerializeField] private bool _isSavedCalibrationUsed = true;

		private FpvFlightController _controller;
		private RcInputActions _rcActions;
		private RcInputReader _reader;

		public RcInputReader Reader => _reader;

		private void Awake()
		{
			_controller = GetComponent<FpvFlightController>();
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

		private void OnEnable()
		{
			_rcActions?.Enable();
		}

		private void OnDisable()
		{
			_rcActions?.Disable();
			_reader?.Reset();
		}

		private void Update()
		{
			_reader.Update(Time.unscaledDeltaTime);
		}

		private void FixedUpdate()
		{
			_controller.Tick(_reader.Current, Time.fixedDeltaTime);
		}
	}
}
