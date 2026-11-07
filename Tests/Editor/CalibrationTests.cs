using NUnit.Framework;
using UForce.FpvFlight.RadioInput;
using UForce.FpvFlight.Rc;

namespace UForce.FpvFlight.Tests
{
	public sealed class CalibrationTests
	{
		[Test]
		public void WhenCenteredAxisAtEnds_ThenNormalizedToFullDeflection()
		{
			RcAxisCalibration axis = new(string.Empty, -1f, 0f, 1f, true, false, 0f);
			Assert.That(axis.Normalize(0f), Is.EqualTo(0f).Within(1e-4f));
			Assert.That(axis.Normalize(1f), Is.EqualTo(1f).Within(1e-4f));
			Assert.That(axis.Normalize(-1f), Is.EqualTo(-1f).Within(1e-4f));
			Assert.That(axis.Normalize(0.5f), Is.EqualTo(0.5f / 0.94f).Within(1e-3f));
		}

		[Test]
		public void WhenAxisInverted_ThenSignFlips()
		{
			RcAxisCalibration axis = new(string.Empty, -1f, 0f, 1f, true, true, 0f);
			Assert.That(axis.Normalize(1f), Is.EqualTo(-1f).Within(1e-4f));
		}

		[Test]
		public void WhenNearCenter_ThenDeadzoneReadsZero()
		{
			RcAxisCalibration axis = new(string.Empty, -1f, 0f, 1f, true, false, 0.05f);
			Assert.That(axis.Normalize(0.03f), Is.EqualTo(0f));
			Assert.That(axis.Normalize(1f), Is.EqualTo(1f).Within(1e-4f));
		}

		[Test]
		public void WhenCenterIsOffset_ThenEachSideScalesSeparately()
		{
			RcAxisCalibration axis = new(string.Empty, 0f, 0.4f, 1f, true, false, 0f);
			Assert.That(axis.Normalize(0.4f), Is.EqualTo(0f).Within(1e-4f));
			Assert.That(axis.Normalize(1f), Is.EqualTo(1f).Within(1e-4f));
			Assert.That(axis.Normalize(0f), Is.EqualTo(-1f).Within(1e-4f));
		}

		[Test]
		public void WhenThrottleAxis_ThenBottomIsMinusOneAndTopIsOne()
		{
			RcAxisCalibration throttle = new RcCalibration().Throttle;
			Assert.That(throttle.Normalize(-1f), Is.EqualTo(-1f).Within(1e-4f));
			Assert.That(throttle.Normalize(1f), Is.EqualTo(1f).Within(1e-4f));
			Assert.IsFalse(throttle.IsCentered);
		}

		[Test]
		public void WhenSwitchRead_ThenPositionsAreRestMiddleActive()
		{
			RcSwitchCalibration armSwitch = new RcCalibration().ArmSwitch;
			Assert.That(armSwitch.GetPosition(1f), Is.EqualTo(0));
			Assert.That(armSwitch.GetPosition(0f), Is.EqualTo(1));
			Assert.That(armSwitch.GetPosition(-1f), Is.EqualTo(RcSwitchCalibration.ACTIVE_POSITION));
		}

		[Test]
		public void WhenModePositionMapped_ThenDefaultsAreAngleHorizonAcro()
		{
			RcCalibration calibration = new();
			Assert.That(calibration.ModeAtPosition(0), Is.EqualTo(FlightMode.Angle));
			Assert.That(calibration.ModeAtPosition(1), Is.EqualTo(FlightMode.Horizon));
			Assert.That(calibration.ModeAtPosition(2), Is.EqualTo(FlightMode.Acro));
		}

		[TestCase(-1f, 0)]
		[TestCase(-0.51f, 0)]
		[TestCase(-0.49f, 1)]
		[TestCase(0.49f, 1)]
		[TestCase(0.51f, 2)]
		[TestCase(1f, 2)]
		public void WhenThreePositionSwitchRead_ThenInterceptorThresholdsApply(float raw, int position)
		{
			RcSwitchCalibration cameraTilt = new RcCalibration().GetSwitch(RcSwitchChannel.CameraTilt);
			Assert.That(cameraTilt.GetPosition(raw), Is.EqualTo(position));
		}

		[Test]
		public void WhenDefaultLayout_ThenButtonsAreMomentaryAndSwitchesLatching()
		{
			RcCalibration calibration = new();
			Assert.IsTrue(calibration.GetSwitch(RcSwitchChannel.Arm).IsLatching);
			Assert.IsTrue(calibration.GetSwitch(RcSwitchChannel.PayloadArm).IsLatching);
			Assert.IsTrue(calibration.GetSwitch(RcSwitchChannel.CameraTilt).IsLatching);
			Assert.IsTrue(calibration.GetSwitch(RcSwitchChannel.FlightMode).IsLatching);
			Assert.IsFalse(calibration.GetSwitch(RcSwitchChannel.PayloadDetonate).IsLatching);
			Assert.IsFalse(calibration.GetSwitch(RcSwitchChannel.Thermal).IsLatching);
		}

		[Test]
		public void WhenSwitchCaptureReturnedToStart_ThenOnlyStateSwitchesStayLatching()
		{
			// Moved from rest (-1) to the far end and back: a button-like gesture.
			RcControlCapture capture = new(-1f);
			capture.Add(1f);
			capture.Add(-1f);

			RcSwitchCalibration forced = RcSwitchCalibration.FromCapture(capture, -1f, "slider", RcSwitchChannels.IsStateSwitch(RcSwitchChannel.CameraTilt));
			RcSwitchCalibration free = RcSwitchCalibration.FromCapture(capture, -1f, "trigger", RcSwitchChannels.IsStateSwitch(RcSwitchChannel.Thermal));

			Assert.IsTrue(forced.IsLatching);
			Assert.IsFalse(free.IsLatching);
			Assert.That(free.GetPosition(1f), Is.EqualTo(RcSwitchCalibration.ACTIVE_POSITION), "Pressed reads as active.");
			Assert.That(free.GetPosition(-1f), Is.EqualTo(0), "Released reads as rest.");
		}

		[Test]
		public void WhenSessionStarts_ThenOptionalStepsCanBeSkippedToSummary()
		{
			RcCalibrationSession session = new();
			int armStep = -1;
			for (int index = 0; index < RcCalibrationSession.Steps.Count; index++)
			{
				RcCalibrationStep step = RcCalibrationSession.Steps[index];
				if (step.Kind == RcCalibrationStepKind.Switch && step.Switch == RcSwitchChannel.Arm)
				{
					armStep = index;
				}
				else if (step.Kind == RcCalibrationStepKind.Switch)
				{
					Assert.IsTrue(step.IsOptional, $"{step.Switch} must be optional.");
				}
			}

			Assert.That(armStep, Is.GreaterThan(0));
			Assert.IsFalse(RcCalibrationSession.Steps[armStep].IsOptional, "Arm is required.");
			Assert.That(RcCalibrationSession.Steps[RcCalibrationSession.Steps.Count - 1].Kind, Is.EqualTo(RcCalibrationStepKind.Summary));

			session.Skip();
			Assert.That(session.StepIndex, Is.EqualTo(0), "A required step cannot be skipped.");
		}

		[Test]
		public void WhenExistingCalibrationOpened_ThenSessionStartsAtSummary()
		{
			RcCalibration calibration = new();
			calibration.SetDevice("HID|Radiomaster|TX12", "TX12");

			RcCalibrationSession session = RcCalibrationSession.OpenExisting(calibration, null);

			Assert.IsTrue(session.IsComplete);
			Assert.That(session.Result.DeviceKey, Is.EqualTo("HID|Radiomaster|TX12"));
			Assert.That(session.Preview.GetSwitchPosition(RcSwitchChannel.Arm), Is.EqualTo(RcAuxState.NO_POSITION));
		}

		[Test]
		public void WhenModeCycled_ThenOrderIsAngleHorizonAcro()
		{
			Assert.That(RcInputReader.NextMode(FlightMode.Angle), Is.EqualTo(FlightMode.Horizon));
			Assert.That(RcInputReader.NextMode(FlightMode.Horizon), Is.EqualTo(FlightMode.Acro));
			Assert.That(RcInputReader.NextMode(FlightMode.Acro), Is.EqualTo(FlightMode.Angle));
		}

		[TestCase(0f, 0f, 1f)]
		[TestCase(0.375f, 0f, 0.5f)]
		[TestCase(0f, -0.75f, 0f)]
		[TestCase(1f, 0f, 0f)]
		public void WhenHorizonStickDeflected_ThenLevellingFades(float roll, float pitch, float expected)
		{
			Assert.That(FpvFlightSimulation.HorizonLevelFactor(roll, pitch, 0.75f), Is.EqualTo(expected).Within(1e-4f));
		}

		[Test]
		public void WhenCloned_ThenValuesSurviveJson()
		{
			RcCalibration calibration = new();
			calibration.SetDevice("HID|Radiomaster|TX12", "TX12");
			calibration.SetAxis(RcAxisChannel.Roll, new RcAxisCalibration("stick/x", -0.9f, 0.05f, 0.95f, true, true, 0.03f));

			RcCalibration copy = calibration.Clone();

			Assert.That(copy.DeviceKey, Is.EqualTo("HID|Radiomaster|TX12"));
			Assert.That(copy.Roll.ControlPath, Is.EqualTo("stick/x"));
			Assert.That(copy.Roll.Min, Is.EqualTo(-0.9f));
			Assert.IsTrue(copy.Roll.IsInverted);
		}

		[Test]
		public void WhenStored_ThenLoadedBackByDeviceKey()
		{
			PlayerPrefsRcCalibrationStore store = new("UForce.FpvFlight.Tests.");
			RcCalibration calibration = new();
			calibration.SetDevice("test|device|key", "Test");
			calibration.SetThrottleLowDeadzone(0.07f);

			try
			{
				store.Save(calibration);
				Assert.IsTrue(store.TryLoad("test|device|key", out RcCalibration loaded));
				Assert.That(loaded.ThrottleLowDeadzone, Is.EqualTo(0.07f).Within(1e-5f));
			}
			finally
			{
				store.Delete("test|device|key");
			}

			Assert.IsFalse(store.TryLoad("test|device|key", out _));
		}

		[Test]
		public void WhenCaptureMovesEndToEnd_ThenDetectedWithDirection()
		{
			RcControlCapture capture = new(0f);
			capture.Add(0.3f);
			capture.Add(0.9f);
			capture.Add(-0.8f);

			Assert.IsTrue(capture.IsDetected);
			Assert.IsTrue(capture.IsStartedTowardsMax);
			Assert.IsFalse(capture.IsInvertedAroundCenter(0f));
		}
	}
}
