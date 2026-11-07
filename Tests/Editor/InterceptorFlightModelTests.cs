using NUnit.Framework;
using UForce.FpvFlight.Interceptor;
using UForce.FpvFlight.Rc;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UForce.FpvFlight.Tests
{
	// Interceptor Drones' flight model with the P1-SUN preset, on a real Rigidbody in a preview physics scene.
	public sealed class InterceptorFlightModelTests
	{
		private const float DELTA_TIME = 0.02f;

		private TestAssets _assets;
		private InterceptorFlightProfile _interceptor;
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
			_interceptor = _assets.Create<InterceptorFlightProfile>();
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

		[Test]
		public void WhenFullRollStick_ThenRateReachesPresetLimitAlmostInstantly()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);

			Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));

			// As in Interceptor, the preset's angular damping holds the rate a few percent under the target.
			Assert.That(MeasuredRollRate(), Is.EqualTo(SteadyRate(700f)).Within(10f));
			for (int step = 0; step < 20; step++)
			{
				Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));
				Assert.That(MeasuredRollRate(), Is.LessThanOrEqualTo(700.5f), "The rate is clamped to the preset limit.");
			}
		}

		[Test]
		public void WhenHalfRollStick_ThenRateIsHalfTheLimit()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);
			for (int step = 0; step < 10; step++)
			{
				Step(new RcCommand(0.5f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			}

			Assert.That(MeasuredRollRate(), Is.EqualTo(SteadyRate(350f)).Within(5f), "Interceptor rates are linear in the stick.");
		}

		[Test]
		public void WhenStickReleasedWithHardStop_ThenRateBrakesGraduallyAndStops()
		{
			CreateDrone();
			ArmAtThrottle(0.3f);
			for (int step = 0; step < 10; step++)
			{
				Step(new RcCommand(1f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			}

			float previous = MeasuredRollRate();
			Step(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			Assert.That(MeasuredRollRate(), Is.GreaterThan(previous * 0.8f), "The release brake is gradual, not the instant rate law.");

			for (int step = 0; step < 30; step++)
			{
				Step(new RcCommand(0f, 0f, 0f, 0.3f, true, FlightMode.Acro));
			}

			Assert.That(Mathf.Abs(MeasuredRollRate()), Is.LessThan(2f));
		}

		[Test]
		public void WhenTiltedAtFullThrottle_ThenSpeedStopsAtThePresetLimit()
		{
			CreateDrone();
			ArmAtThrottle(1f);
			Run(new RcCommand(0f, 1f, 0f, 1f, true, FlightMode.Angle), 6f);

			// The speed is clamped before the step integrates its forces, as in Interceptor, so up to one step of acceleration shows above it.
			float speedKmh = _body.linearVelocity.magnitude * 3.6f;
			Assert.That(speedKmh, Is.LessThanOrEqualTo(_interceptor.MaxSpeedKmh + 12f));
			Assert.That(speedKmh, Is.GreaterThan(_interceptor.MaxSpeedKmh * 0.95f));
		}

		[Test]
		public void WhenThrottleAtTheHoverEstimate_ThenTheDroneHovers()
		{
			CreateDrone();
			float hover = FpvFlightSimulation.InterceptorHoverThrottle(_interceptor);
			Assert.That(hover, Is.GreaterThan(1f / _interceptor.ThrottlePower), "P1-SUN's 1/7 lies inside the extra-gravity fade.");
			ArmAtThrottle(hover);
			Run(new RcCommand(0f, 0f, 0f, hover, true, FlightMode.Angle), 0.5f);
			float before = _body.linearVelocity.y;
			Run(new RcCommand(0f, 0f, 0f, hover, true, FlightMode.Angle), 0.5f);

			Assert.That((_body.linearVelocity.y - before) / 0.5f, Is.EqualTo(0f).Within(0.3f));
			Assert.That(_controller.Telemetry.HoverThrottleEstimate, Is.EqualTo(hover).Within(1e-4f));
		}

		[Test]
		public void WhenThrottleCutInTheAir_ThenExtraGravityPullsTheDroneDown()
		{
			CreateDrone();
			ArmAtThrottle(0f);
			float before = _body.linearVelocity.y;
			Run(new RcCommand(0f, 0f, 0f, 0f, true, FlightMode.Angle), 0.2f);

			float acceleration = (_body.linearVelocity.y - before) / 0.2f;
			Assert.That(acceleration, Is.LessThan(-9.81f * 2.3f), "1 g of gravity plus 1.5 g of extra gravity, minus a little damping.");
		}

		[Test]
		public void WhenInterceptorModel_ThenRigidbodyUsesThePresetMassAndDamping()
		{
			CreateDrone();
			Assert.That(_body.mass, Is.EqualTo(_interceptor.BaseMassKg + _assets.Battery.Mass).Within(1e-4f));
			Assert.That(_body.linearDamping, Is.EqualTo(_interceptor.LinearDamping).Within(1e-5f));
			Assert.That(_body.angularDamping, Is.EqualTo(_interceptor.AngularDamping).Within(1e-5f));
		}

		private void CreateDrone()
		{
			_drone = new GameObject("InterceptorTestDrone");
			SceneManager.MoveGameObjectToScene(_drone, _scene);
			_drone.transform.position = new Vector3(0f, 500f, 0f);
			_body = _drone.AddComponent<Rigidbody>();
			_body.useGravity = true;
			_body.interpolation = RigidbodyInterpolation.None;
			_drone.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.6f, 0.3f);
			_controller = _drone.AddComponent<FpvFlightController>();
			_controller.Configure(_assets.Airframe, _assets.Battery, _assets.Rates, _assets.Profile, _interceptor);
		}

		private void ArmAtThrottle(float throttle)
		{
			Step(new RcCommand(0f, 0f, 0f, 0f, false, FlightMode.Acro));
			Step(new RcCommand(0f, 0f, 0f, 0f, true, FlightMode.Acro));
			Assert.IsTrue(_controller.IsArmed, "Precondition: armed.");
			Step(new RcCommand(0f, 0f, 0f, throttle, true, FlightMode.Acro));
		}

		private void Run(RcCommand command, float seconds)
		{
			int steps = Mathf.RoundToInt(seconds / DELTA_TIME);
			for (int step = 0; step < steps; step++)
			{
				Step(command);
			}
		}

		private void Step(RcCommand command)
		{
			_controller.Tick(command, DELTA_TIME);
			_physics.Simulate(DELTA_TIME);
		}

		// Rate law (target - rate) × sensitivity against Unity's angular damping, per 50 Hz step.
		private float SteadyRate(float target)
		{
			float sensitivity = _interceptor.Sensitivity.z;
			float damping = _interceptor.AngularDamping;
			return target * sensitivity / (sensitivity + damping);
		}

		// Roll right is negative about the body forward axis.
		private float MeasuredRollRate() => -(Quaternion.Inverse(_body.rotation) * _body.angularVelocity).z * Mathf.Rad2Deg;
	}
}
