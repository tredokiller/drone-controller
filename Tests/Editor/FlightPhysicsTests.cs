using NUnit.Framework;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Rc;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UForce.FpvFlight.Tests
{
	// Flies a real Rigidbody in an isolated preview physics scene, so the user's open scene is never simulated.
	public sealed class FlightPhysicsTests
	{
		private const float DELTA_TIME = 0.02f;

		private TestAssets _assets;
		private Scene _scene;
		private PhysicsScene _physics;
		private GameObject _drone;
		private FpvFlightController _controller;
		private Rigidbody _body;
		private SimulationMode _previousMode;

		[SetUp]
		public void SetUp()
		{
			_assets = new TestAssets();
			_scene = EditorSceneManager.NewPreviewScene();
			_physics = _scene.GetPhysicsScene();
			_previousMode = Physics.simulationMode;
			Physics.simulationMode = SimulationMode.Script;
		}

		[TearDown]
		public void TearDown()
		{
			Physics.simulationMode = _previousMode;
			EditorSceneManager.ClosePreviewScene(_scene);
			_assets.Dispose();
		}

		[TestCase(0.02f)]
		[TestCase(0.01f)]
		public void WhenFullRollStickInAcro_ThenCommandedRateIsReachedWithoutOvershoot(float deltaTime)
		{
			CreateDrone();
			ArmAtThrottle(0.3f, deltaTime);

			float target = _assets.Rates.MaxRollRate;
			float peak = 0f;
			int steps = Mathf.RoundToInt(0.3f / deltaTime);
			for (int step = 0; step < steps; step++)
			{
				Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro), deltaTime);
				peak = Mathf.Max(peak, _controller.Telemetry.RollRate);
			}

			Assert.That(_controller.Telemetry.TargetRollRate, Is.EqualTo(666.67f).Within(0.5f));
			Assert.That(MeasuredRollRate(), Is.EqualTo(target).Within(target * 0.03f));
			Assert.That(peak, Is.LessThan(target * 1.1f));
		}

		[Test]
		public void WhenStickReleasedInAcro_ThenAttitudeHolds()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);
			Run(new RcCommand(0.4f, 0.3f, 0f, 0.3f, true, FlightMode.Acro), 0.3f);
			Run(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro), 0.3f);

			Quaternion held = _body.rotation;
			Assert.That(AngleController.GetTiltAngle(held), Is.GreaterThan(20f), "Precondition: the drone is tilted.");

			Run(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro), 3f);

			Assert.That(Quaternion.Angle(held, _body.rotation), Is.LessThan(1f));
			Assert.That(_body.angularVelocity.magnitude * Mathf.Rad2Deg, Is.LessThan(1f));
		}

		[Test]
		public void WhenRollHeldForOneTurn_ThenFlipCompletesAndStops()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);

			float rolled = 0f;
			int guard = 0;
			while (rolled < 340f && guard++ < 200)
			{
				Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));
				rolled += MeasuredRollRate() * DELTA_TIME;
			}

			for (int step = 0; step < 25; step++)
			{
				Step(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro));
				rolled += MeasuredRollRate() * DELTA_TIME;
			}

			Assert.That(rolled, Is.InRange(345f, 420f));
			Assert.That(Mathf.Abs(MeasuredRollRate()), Is.LessThan(5f));
			Assert.That(AngleController.GetTiltAngle(_body.rotation), Is.LessThan(60f));
		}

		[Test]
		public void WhenPitchStickHeldInAngle_ThenTiltConvergesToStickTimesLimit()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);

			Run(new RcCommand(0f, 0.5f, 0f, 0.3f, true, FlightMode.Angle), 2f);

			Assert.That(_controller.Telemetry.TiltAngle, Is.EqualTo(0.5f * _assets.Profile.AngleLimit).Within(2f));
			Assert.That(Vector3.Dot(_body.rotation * Vector3.forward, Vector3.down), Is.GreaterThan(0f), "Positive pitch stick must lower the nose.");
		}

		[Test]
		public void WhenSwitchedToAngleWhileInverted_ThenDroneLevels()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);
			_body.rotation = Quaternion.Euler(10f, 30f, 175f);
			_body.angularVelocity = Vector3.zero;

			Run(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Angle), 1.5f);

			Assert.That(_controller.Telemetry.TiltAngle, Is.LessThan(5f));
		}

		[Test]
		public void WhenModeToggledInFlight_ThenNoRateSpike()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);
			Run(new RcCommand(0.3f, 0f, 0f, 0.3f, true, FlightMode.Acro), 0.15f);
			Run(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro), 0.3f);
			float tilt = _controller.Telemetry.TiltAngle;

			float peak = 0f;
			for (int step = 0; step < 50; step++)
			{
				Step(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Angle));
				peak = Mathf.Max(peak, Mathf.Abs(MeasuredRollRate()));
			}

			Assert.That(peak, Is.LessThan(tilt * _assets.Profile.AngleStrength * 1.1f + 5f));
			Assert.That(_controller.Telemetry.TiltAngle, Is.LessThan(3f));
		}

		[Test]
		public void WhenThrottleSearchedInSimulation_ThenHoverMatchesDerivedValue()
		{
			float low = 0f;
			float high = 1f;
			float estimate = 0f;
			for (int iteration = 0; iteration < 14; iteration++)
			{
				float throttle = 0.5f * (low + high);
				float verticalAcceleration = SimulateVerticalAcceleration(throttle, out estimate);
				if (verticalAcceleration < 0f)
				{
					low = throttle;
				}
				else
				{
					high = throttle;
				}
			}

			float simulated = 0.5f * (low + high);
			Assert.That(simulated, Is.InRange(0.1f, 0.7f));
			Assert.That(simulated, Is.EqualTo(estimate).Within(0.015f));
		}

		[Test]
		public void WhenHeavierPayload_ThenHoverThrottleRises()
		{
			SimulateVerticalAcceleration(0.3f, out float light);
			SimulateVerticalAcceleration(0.3f, out float heavy, 0.8f);
			Assert.That(heavy, Is.GreaterThan(light + 0.03f));
			Assert.That(_body.mass, Is.EqualTo(AirframeMath.TotalMass(_assets.Airframe, _assets.Battery, 0.8f)).Within(1e-4f));
		}

		[Test]
		public void WhenFullThrottleAtTopSpeedTilt_ThenLevelSpeedMatchesDerivedValue()
		{
			_assets.Profile.Configure(85f, 5f, _assets.Profile.Failsafe);
			CreateDrone(hasBattery: false);
			float mass = AirframeMath.TotalMass(_assets.Airframe, null, 0f);
			float expected = AirframeMath.MaxLevelSpeed(_assets.Airframe, mass, Physics.gravity.magnitude, 1f, out float tilt);

			ArmAtThrottle(1f);
			Run(new RcCommand(0f, tilt / 85f, 0f, 1f, true, FlightMode.Angle), 30f);

			Vector3 velocity = _body.linearVelocity;
			float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
			Assert.That(horizontal, Is.EqualTo(expected).Within(expected * 0.05f));
			Assert.That(Mathf.Abs(velocity.y), Is.LessThan(1.5f));
		}

		[Test]
		public void WhenFallingDisarmed_ThenTerminalSpeedMatchesDrag()
		{
			CreateDrone();
			Run(RcCommand.Neutral, 15f);

			float expected = AirframeMath.TerminalDescentSpeed(_assets.Airframe, _body.mass, Physics.gravity.magnitude);
			Assert.That(-_body.linearVelocity.y, Is.EqualTo(expected).Within(expected * 0.03f));
		}

		[Test]
		public void WhenLinkLostInFlight_ThenFailsafeDropsTheDrone()
		{
			CreateDrone();
			FakeLinkModel link = new();
			_controller.SetLinkModel(link);
			ArmAtThrottle(0.4f);
			Run(new RcCommand(0f, 0f, 0f, 0.4f, true, FlightMode.Angle), 0.5f);

			link.State = RcLinkState.Lost;
			Run(new RcCommand(0f, 0f, 0f, 0.4f, true, FlightMode.Angle), 1.8f);

			Assert.IsFalse(_controller.IsArmed);
			Assert.That(_controller.Telemetry.FailsafeStage, Is.EqualTo(FailsafeStage.StageTwo));

			// Stage 1 already cut the throttle, so the drone is sinking; after the drop it falls toward free-fall terminal speed.
			float before = _body.linearVelocity.y;
			Run(new RcCommand(0f, 0f, 0f, 0.4f, true, FlightMode.Angle), 5f);
			float terminal = AirframeMath.TerminalDescentSpeed(_assets.Airframe, _body.mass, Physics.gravity.magnitude);
			Assert.That(_body.linearVelocity.y, Is.LessThan(before));
			Assert.That(-_body.linearVelocity.y, Is.GreaterThan(terminal * 0.85f));
			Assert.That(_controller.MotorOutput, Is.LessThan(0.01f));
		}

		// Vertical acceleration over the last half second of a 1.5 s run; it is independent of the arming transient.
		private float SimulateVerticalAcceleration(float throttle, out float hoverEstimate, float payload = 0f)
		{
			if (_drone)
			{
				Object.DestroyImmediate(_drone);
			}

			CreateDrone();
			_controller.SetPayloadMass(payload);
			ArmAtThrottle(throttle);
			RcCommand command = new(0f, 0f, 0f, throttle, true, FlightMode.Angle);
			Run(command, 1f);
			float before = _body.linearVelocity.y;
			Run(command, 0.5f);
			hoverEstimate = _controller.Telemetry.HoverThrottleEstimate;
			return (_body.linearVelocity.y - before) / 0.5f;
		}

		private void CreateDrone(bool hasBattery = true)
		{
			_drone = new GameObject("FpvTestDrone");
			SceneManager.MoveGameObjectToScene(_drone, _scene);
			_drone.transform.position = new Vector3(0f, 500f, 0f);
			_body = _drone.AddComponent<Rigidbody>();
			_body.useGravity = true;
			_body.interpolation = RigidbodyInterpolation.None;
			_controller = _drone.AddComponent<FpvFlightController>();
			_controller.Configure(_assets.Airframe, hasBattery ? _assets.Battery : null, _assets.Rates, _assets.Profile);
		}

		// Arms on the switch edge at zero throttle, then sets the requested throttle.
		private void ArmAtThrottle(float throttle, float deltaTime = DELTA_TIME)
		{
			Step(new RcCommand(0f, 0f, 0f, 0f, false, FlightMode.Acro), deltaTime);
			Step(new RcCommand(0f, 0f, 0f, 0f, true, FlightMode.Acro), deltaTime);
			Assert.IsTrue(_controller.IsArmed, "Precondition: armed.");
			Step(new RcCommand(0f, 0f, 0f, throttle, true, FlightMode.Acro), deltaTime);
		}

		private void Run(RcCommand command, float seconds)
		{
			int steps = Mathf.RoundToInt(seconds / DELTA_TIME);
			for (int step = 0; step < steps; step++)
			{
				Step(command);
			}
		}

		private void Step(RcCommand command, float deltaTime = DELTA_TIME)
		{
			_controller.Tick(command, deltaTime);
			if (_physics.IsValid() && _physics != Physics.defaultPhysicsScene)
			{
				_physics.Simulate(deltaTime);
			}
			else
			{
				Physics.Simulate(deltaTime);
			}
		}

		// Roll right is negative about the body forward axis.
		private float MeasuredRollRate() => -(Quaternion.Inverse(_body.rotation) * _body.angularVelocity).z * Mathf.Rad2Deg;
	}
}
