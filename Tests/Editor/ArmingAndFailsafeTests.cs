using NUnit.Framework;
using UForce.FpvFlight.Environment;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Tests
{
	public sealed class ArmingAndFailsafeTests
	{
		private const float DELTA_TIME = 0.02f;
		private const float GRAVITY = 9.81f;

		private TestAssets _assets;
		private FpvFlightSimulation _simulation;
		private FakeLinkModel _link;

		[SetUp]
		public void SetUp()
		{
			_assets = new TestAssets();
			_simulation = _assets.CreateSimulation();
			_link = new FakeLinkModel();
			_simulation.SetLinkModel(_link);
		}

		[TearDown]
		public void TearDown()
		{
			_assets.Dispose();
		}

		[Test]
		public void WhenArmSwitchFlippedWithThrottleHigh_ThenNotArmedUntilSwitchCycled()
		{
			Run(Command(0f, false), 0.1f);
			Run(Command(0.5f, true), 0.1f);
			Assert.IsFalse(_simulation.IsArmed);
			Assert.IsTrue((_simulation.Telemetry.ArmingBlockReason & ArmingBlockReason.ThrottleNotLow) != 0);

			Run(Command(0f, true), 0.1f);
			Assert.IsFalse(_simulation.IsArmed, "Lowering throttle with the switch still on must not arm.");

			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed);
		}

		[Test]
		public void WhenSwitchAlreadyOnAtStart_ThenItMustBeCycled()
		{
			Run(Command(0f, true), 0.2f);
			Assert.IsFalse(_simulation.IsArmed);

			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed);
		}

		[Test]
		public void WhenArmSwitchTurnedOff_ThenDisarmed()
		{
			Arm();
			Run(Command(0.6f, true), 0.2f);
			Run(Command(0.6f, false), 0.04f);
			Assert.IsFalse(_simulation.IsArmed);
		}

		[Test]
		public void WhenLinkLost_ThenStageOneCentersSticksAndThenDrops()
		{
			Arm();
			Run(new RcCommand(1f, 0f, 0f, 0.6f, true, FlightMode.Acro), 0.2f);
			Assert.That(_simulation.Telemetry.TargetRollRate, Is.GreaterThan(600f));

			_link.State = RcLinkState.Lost;
			Run(Command(0.6f, true), 0.06f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.None), "Short glitches hold the last frame.");

			Run(Command(0.6f, true), 0.08f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.StageOne));
			Assert.IsTrue(_simulation.IsArmed);
			Assert.That(_simulation.Telemetry.TargetRollRate, Is.EqualTo(0f).Within(1e-3f));
			Assert.That(_simulation.Telemetry.Throttle, Is.EqualTo(0f).Within(1e-3f));

			Run(Command(0.6f, true), 1.5f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.StageTwo));
			Assert.IsFalse(_simulation.IsArmed);
		}

		[Test]
		public void WhenLinkReturnsDuringStageOne_ThenControlResumes()
		{
			Arm();
			Run(Command(0.6f, true), 0.1f);
			_link.State = RcLinkState.Lost;
			Run(Command(0.6f, true), 0.5f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.StageOne));

			_link.State = RcLinkState.Ideal;
			Run(new RcCommand(1f, 0f, 0f, 0.6f, true, FlightMode.Acro), 0.04f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.None));
			Assert.IsTrue(_simulation.IsArmed);
			Assert.That(_simulation.Telemetry.TargetRollRate, Is.GreaterThan(600f));
		}

		[Test]
		public void WhenDroppedByFailsafe_ThenRearmNeedsSwitchCycle()
		{
			Arm();
			_link.State = RcLinkState.Lost;
			Run(Command(0f, true), 2f);
			Assert.IsFalse(_simulation.IsArmed);

			_link.State = RcLinkState.Ideal;
			Run(Command(0f, true), 0.2f);
			Assert.IsFalse(_simulation.IsArmed);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.None));

			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed);
		}

		[Test]
		public void WhenLinkLostWhileDisarmed_ThenNoDropLatchesAndArmingIsBlocked()
		{
			Run(Command(0f, false), 0.1f);
			_link.State = RcLinkState.Lost;
			Run(Command(0f, false), 3f);
			Assert.That(_simulation.FailsafeStage, Is.EqualTo(FailsafeStage.None));
			Assert.IsTrue((_simulation.Telemetry.ArmingBlockReason & ArmingBlockReason.RxLoss) != 0);

			_link.State = RcLinkState.Ideal;
			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed);
		}

		[Test]
		public void WhenSwitchTurnedOnDuringLinkLoss_ThenRecoveryDoesNotArm()
		{
			Run(Command(0f, false), 0.1f);
			_link.State = RcLinkState.Lost;
			Run(Command(0f, true), 0.5f);

			_link.State = RcLinkState.Ideal;
			Run(Command(0f, true), 0.2f);
			Assert.IsFalse(_simulation.IsArmed);
			Assert.IsTrue((_simulation.Telemetry.ArmingBlockReason & ArmingBlockReason.SwitchMustCycle) != 0);
		}

		[Test]
		public void WhenDisarmed_ThenNoThrustAndNoControl()
		{
			Run(new RcCommand(1f, 1f, 1f, 1f, false, FlightMode.Acro), 0.5f);
			FlightStepResult result = Step(new RcCommand(1f, 1f, 1f, 1f, false, FlightMode.Acro));
			Assert.That(result.ThrustForce.magnitude, Is.EqualTo(0f).Within(1e-4f));
			Assert.That(result.AngularAcceleration.magnitude, Is.EqualTo(0f).Within(1e-4f));
		}

		[TestCase(ArmingInhibit.PayloadArmed, ArmingBlockReason.PayloadArmed)]
		[TestCase(ArmingInhibit.Crashed, ArmingBlockReason.Crashed)]
		[TestCase(ArmingInhibit.External, ArmingBlockReason.External)]
		public void WhenArmingInhibited_ThenSwitchDoesNotArmUntilClearedAndCycled(ArmingInhibit inhibit, ArmingBlockReason reason)
		{
			_simulation.SetArmingInhibit(inhibit, true);
			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsFalse(_simulation.IsArmed);
			Assert.IsTrue((_simulation.Telemetry.ArmingBlockReason & reason) != 0);

			_simulation.SetArmingInhibit(inhibit, false);
			Run(Command(0f, true), 0.1f);
			Assert.IsFalse(_simulation.IsArmed, "Clearing the inhibit with the switch still on must not arm.");

			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed);
		}

		[Test]
		public void WhenInhibitSetWhileArmed_ThenDroneStaysArmed()
		{
			Arm();
			_simulation.SetArmingInhibit(ArmingInhibit.PayloadArmed, true);
			Run(Command(0.3f, true), 0.2f);
			Assert.IsTrue(_simulation.IsArmed, "An inhibit only blocks arming; it does not disarm in flight.");
		}

		[Test]
		public void WhenBatteryAtOnePercent_ThenArmingIsBlocked()
		{
			_simulation.SetBattery(_assets.Battery, 0.01f);
			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsFalse(_simulation.IsArmed);
			Assert.IsTrue((_simulation.Telemetry.ArmingBlockReason & ArmingBlockReason.BatteryEmpty) != 0);
		}

		[Test]
		public void WhenArmedAtZeroThrottle_ThenMotorsIdle()
		{
			Arm();
			Run(Command(0f, true), 0.5f);
			Assert.That(_simulation.MotorOutput, Is.EqualTo(_assets.Airframe.MotorIdle).Within(1e-3f));
		}

		private void Arm()
		{
			Run(Command(0f, false), 0.1f);
			Run(Command(0f, true), 0.1f);
			Assert.IsTrue(_simulation.IsArmed, "Precondition: armed.");
		}

		private static RcCommand Command(float throttle, bool armSwitch) => new(0f, 0f, 0f, throttle, armSwitch, FlightMode.Acro);

		private void Run(RcCommand command, float seconds)
		{
			int steps = Mathf.RoundToInt(seconds / DELTA_TIME);
			for (int step = 0; step < steps; step++)
			{
				Step(command);
			}
		}

		private FlightStepResult Step(RcCommand command)
		{
			FlightBodyState body = new(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero);
			return _simulation.Step(command, body, WindSample.Calm, GRAVITY, DELTA_TIME);
		}
	}
}
