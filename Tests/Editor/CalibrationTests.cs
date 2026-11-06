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
		public void WhenModePositionMapped_ThenDefaultsAreAngleThenAcro()
		{
			RcCalibration calibration = new();
			Assert.That(calibration.ModeAtPosition(0), Is.EqualTo(FlightMode.Angle));
			Assert.That(calibration.ModeAtPosition(2), Is.EqualTo(FlightMode.Acro));
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
