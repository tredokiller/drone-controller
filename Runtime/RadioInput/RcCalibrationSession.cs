using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UForce.FpvFlight.RadioInput
{
	// UI-free radio calibration wizard, ported from Interceptor's ControllerCalibrationSession.
	// Steps: move each stick end to end (the first direction of motion sets inversion), circle both sticks,
	// flip the arm switch, optionally flip the flight mode switch. Call Tick every frame and Continue or Skip per step.
	public sealed class RcCalibrationSession
	{
		public static readonly IReadOnlyList<RcCalibrationStep> Steps = new[]
		{
			RcCalibrationStep.ForAxis(RcAxisChannel.Throttle),
			RcCalibrationStep.ForAxis(RcAxisChannel.Yaw),
			RcCalibrationStep.ForAxis(RcAxisChannel.Pitch),
			RcCalibrationStep.ForAxis(RcAxisChannel.Roll),
			RcCalibrationStep.Of(RcCalibrationStepKind.Range),
			RcCalibrationStep.ForSwitch(RcSwitchChannel.Arm, false),
			RcCalibrationStep.ForSwitch(RcSwitchChannel.FlightMode, true),
			RcCalibrationStep.Of(RcCalibrationStepKind.Summary)
		};

		private const int AXIS_COUNT = 4;
		private const float CENTERED_DEADZONE = 0.02f;

		private readonly Dictionary<AxisControl, RcControlCapture> r_captures = new();
		private readonly HashSet<AxisControl> r_boundControls = new();
		private readonly AxisControl[] r_axisControls = new AxisControl[AXIS_COUNT];
		private readonly RcControlCapture[] r_axisCaptures = new RcControlCapture[AXIS_COUNT];
		private readonly RcControlCapture[] r_rangeCaptures = new RcControlCapture[AXIS_COUNT];

		private AxisControl _detected;

		// baseCalibration supplies the throttle deadzone, the arm position and the mode mapping; axes and switches are re-detected.
		public RcCalibrationSession(RcCalibration baseCalibration = null)
		{
			Result = baseCalibration != null ? baseCalibration.Clone() : new RcCalibration();
		}

		public int StepIndex { get; private set; }
		public RcCalibrationStep Step => Steps[StepIndex];
		public bool IsComplete => Step.Kind == RcCalibrationStepKind.Summary;
		public InputDevice Device { get; private set; }
		public RcCalibration Result { get; }
		public bool IsDeviceLost => Device != null && !Device.added;
		public string DetectedName => _detected?.displayName;

		public bool CanContinue
		{
			get
			{
				switch (Step.Kind)
				{
					case RcCalibrationStepKind.Axis:
						return _detected != null && (IsCentered(Step.Axis) || IsDetectedAtEndStop());
					case RcCalibrationStepKind.Switch:
						return _detected != null;
					case RcCalibrationStepKind.Range:
						return IsRangeCovered();
					default:
						return true;
				}
			}
		}

		public float Progress
		{
			get
			{
				if (Step.Kind == RcCalibrationStepKind.Range)
				{
					return GetRangeCoverage();
				}

				if (_detected == null)
				{
					return 0f;
				}

				RcControlCapture capture = r_captures[_detected];
				return Mathf.InverseLerp(capture.Min, capture.Max, _detected.ReadUnprocessedValue());
			}
		}

		public void Tick()
		{
			if (IsDeviceLost)
			{
				return;
			}

			switch (Step.Kind)
			{
				case RcCalibrationStepKind.Axis:
				case RcCalibrationStepKind.Switch:
					Detect();
					break;
				case RcCalibrationStepKind.Range:
					for (int index = 0; index < AXIS_COUNT; index++)
					{
						float value = r_axisControls[index].ReadUnprocessedValue();
						r_axisCaptures[index].Add(value);
						r_rangeCaptures[index].Add(value);
					}

					break;
			}
		}

		public void Continue()
		{
			if (!CanContinue)
			{
				return;
			}

			switch (Step.Kind)
			{
				case RcCalibrationStepKind.Axis:
					BindAxis();
					break;
				case RcCalibrationStepKind.Range:
					ApplyRanges();
					break;
				case RcCalibrationStepKind.Switch:
					BindSwitch();
					break;
			}

			Advance();
		}

		public void Skip()
		{
			if (Step.IsOptional)
			{
				Advance();
			}
		}

		public void ToggleInvert(RcAxisChannel channel)
		{
			RcAxisCalibration axis = Result.GetAxis(channel);
			axis.SetInverted(!axis.IsInverted);
		}

		private static bool IsCentered(RcAxisChannel channel) => channel != RcAxisChannel.Throttle;

		private static string GetRelativePath(InputControl control) => control.path.Substring(control.device.path.Length + 1);

		private bool IsDetectedAtEndStop() => r_captures[_detected].IsAtEndStop(_detected.ReadUnprocessedValue());

		private void Detect()
		{
			_detected = null;
			float largestSpan = 0f;

			foreach (InputDevice device in InputSystem.devices)
			{
				bool isSource = Device != null ? device == Device : RcDevices.IsRadio(device);
				if (!isSource)
				{
					continue;
				}

				foreach (InputControl control in device.allControls)
				{
					if (!IsCandidate(control))
					{
						continue;
					}

					var axis = (AxisControl)control;
					float value = axis.ReadUnprocessedValue();

					if (r_captures.TryGetValue(axis, out RcControlCapture capture))
					{
						capture.Add(value);
					}
					else
					{
						r_captures[axis] = capture = new RcControlCapture(value);
					}

					if (capture.IsDetected && capture.Span > largestSpan)
					{
						largestSpan = capture.Span;
						_detected = axis;
					}
				}
			}
		}

		private bool IsCandidate(InputControl control) =>
			control is AxisControl axis
			&& !control.synthetic
			&& !control.noisy
			&& !r_boundControls.Contains(axis)
			&& (Step.Kind == RcCalibrationStepKind.Switch || control is not ButtonControl);

		private void BindAxis()
		{
			RcAxisChannel channel = Step.Axis;
			int index = (int)channel;
			float reference = _detected.ReadUnprocessedValue();
			RcControlCapture capture = r_captures[_detected];
			bool isCentered = IsCentered(channel);

			Device = _detected.device;
			Result.SetDevice(RcDevices.GetDeviceKey(Device), Device.displayName);

			r_boundControls.Add(_detected);
			r_axisControls[index] = _detected;
			r_axisCaptures[index] = capture;

			bool isInverted = isCentered ? capture.IsInvertedAroundCenter(reference) : capture.IsInverted(reference);
			Result.SetAxis(channel, new RcAxisCalibration(
				GetRelativePath(_detected),
				capture.Min,
				reference,
				capture.Max,
				isCentered,
				isInverted,
				isCentered ? CENTERED_DEADZONE : 0f));
		}

		private void ApplyRanges()
		{
			for (int index = 0; index < AXIS_COUNT; index++)
			{
				Result.GetAxis((RcAxisChannel)index).SetRange(r_axisCaptures[index].Min, r_axisCaptures[index].Max);
			}
		}

		private bool IsRangeCovered()
		{
			for (int index = 0; index < AXIS_COUNT; index++)
			{
				if (r_rangeCaptures[index] == null || !r_rangeCaptures[index].IsDetected)
				{
					return false;
				}

				RcAxisCalibration axis = Result.GetAxis((RcAxisChannel)index);
				if (axis.IsCentered && !r_axisCaptures[index].CoversBothSides(axis.Center))
				{
					return false;
				}
			}

			return true;
		}

		private void BindSwitch()
		{
			bool isAlwaysLatching = _detected is not ButtonControl;
			RcSwitchCalibration calibration = RcSwitchCalibration.FromCapture(
				r_captures[_detected],
				_detected.ReadUnprocessedValue(),
				GetRelativePath(_detected),
				isAlwaysLatching);

			r_boundControls.Add(_detected);
			Result.SetSwitch(Step.Switch, calibration);
		}

		private void Advance()
		{
			StepIndex = Mathf.Min(StepIndex + 1, Steps.Count - 1);
			r_captures.Clear();
			_detected = null;

			if (Step.Kind == RcCalibrationStepKind.Range)
			{
				for (int index = 0; index < AXIS_COUNT; index++)
				{
					r_rangeCaptures[index] = new RcControlCapture(r_axisControls[index].ReadUnprocessedValue());
				}
			}
		}

		private float GetRangeCoverage()
		{
			float coverage = 1f;

			for (int index = 0; index < AXIS_COUNT; index++)
			{
				if (r_rangeCaptures[index] == null || r_axisCaptures[index] == null)
				{
					return 0f;
				}

				RcAxisCalibration axis = Result.GetAxis((RcAxisChannel)index);
				float axisCoverage = axis.IsCentered
					? r_axisCaptures[index].GetSideBalance(axis.Center)
					: r_rangeCaptures[index].Span / Mathf.Max(1e-5f, r_axisCaptures[index].Span);

				coverage = Mathf.Min(coverage, axisCoverage);
			}

			return coverage;
		}
	}
}
