using NUnit.Framework;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.Rc;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UForce.FpvFlight.Tests
{
	// The strike FPV on the Betaflight physics, tuned realistic but snappy: linear rates and a fast rate loop,
	// with real thrust, motor lag and drag. Uses the preset assets of Runtime/DroneTypes/StrikeFpv.
	public sealed class StrikeTuningTests
	{
		private const string TYPES_PATH = "Packages/com.uforce.fpv-flight/Runtime/DroneTypes";
		private const float DELTA_TIME = 0.02f;

		private Scene _scene;
		private PhysicsScene _physics;
		private GameObject _drone;
		private FpvFlightController _controller;
		private Rigidbody _body;
		private SimulationMode _previousMode;

		[SetUp]
		public void SetUp()
		{
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
		}

		[Test]
		public void WhenStrikeRatesRead_ThenTheyAreLinearInTheStick()
		{
			var rates = AssetDatabase.LoadAssetAtPath<RatesProfile>($"{TYPES_PATH}/StrikeFpv/StrikeFpv_Rates.asset");
			Assert.That(rates.RollRate(1f), Is.EqualTo(670f).Within(2f));
			Assert.That(rates.RollRate(0.5f), Is.EqualTo(rates.RollRate(1f) * 0.5f).Within(1f));
			Assert.That(rates.YawRate(1f), Is.EqualTo(450f).Within(2f));
		}

		[TestCase(0f)]
		[TestCase(0.6f)]
		public void WhenFullRollStick_ThenStrikeReachesFullRateQuicklyWithoutOvershoot(float payloadKg)
		{
			CreateStrike(payloadKg);
			ArmAtThrottle(0.4f);

			float target = _controller.Rates.MaxRollRate;
			float peak = 0f;
			float reachedAt = float.PositiveInfinity;
			for (int step = 1; step <= 25; step++)
			{
				Step(new RcCommand(1f, 0f, 0f, 0.4f, true, FlightMode.Acro));
				float rate = MeasuredRollRate();
				peak = Mathf.Max(peak, rate);
				if (rate >= target * 0.9f && float.IsPositiveInfinity(reachedAt))
				{
					reachedAt = step * DELTA_TIME;
				}
			}

			float limit = payloadKg > 0f ? 0.16f : 0.12f;
			Assert.That(reachedAt, Is.LessThanOrEqualTo(limit), $"90% of {target:0}°/s must come within {limit} s.");
			Assert.That(peak, Is.LessThan(target * 1.1f));
		}

		private void CreateStrike(float payloadKg)
		{
			_drone = new GameObject("StrikeTestDrone");
			SceneManager.MoveGameObjectToScene(_drone, _scene);
			_drone.transform.position = new Vector3(0f, 500f, 0f);
			_body = _drone.AddComponent<Rigidbody>();
			_body.interpolation = RigidbodyInterpolation.None;
			_controller = _drone.AddComponent<FpvFlightController>();
			_controller.Configure(
				AssetDatabase.LoadAssetAtPath<AirframeDefinition>($"{TYPES_PATH}/StrikeFpv/StrikeFpv_Airframe.asset"),
				AssetDatabase.LoadAssetAtPath<BatteryDefinition>($"{TYPES_PATH}/StrikeFpv/StrikeFpv_Battery.asset"),
				AssetDatabase.LoadAssetAtPath<RatesProfile>($"{TYPES_PATH}/StrikeFpv/StrikeFpv_Rates.asset"),
				AssetDatabase.LoadAssetAtPath<FlightControllerProfile>($"{TYPES_PATH}/DefaultFlightController.asset"),
				null);
			_controller.SetPayloadMass(payloadKg);
		}

		private void ArmAtThrottle(float throttle)
		{
			Step(new RcCommand(0f, 0f, 0f, 0f, false, FlightMode.Acro));
			Step(new RcCommand(0f, 0f, 0f, 0f, true, FlightMode.Acro));
			Assert.IsTrue(_controller.IsArmed, "Precondition: armed.");
			for (int step = 0; step < 10; step++)
			{
				Step(new RcCommand(0f, 0f, 0f, throttle, true, FlightMode.Acro));
			}
		}

		private void Step(RcCommand command)
		{
			_controller.Tick(command, DELTA_TIME);
			_physics.Simulate(DELTA_TIME);
		}

		private float MeasuredRollRate() => -(Quaternion.Inverse(_body.rotation) * _body.angularVelocity).z * Mathf.Rad2Deg;
	}
}
