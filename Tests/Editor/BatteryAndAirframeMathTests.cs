using NUnit.Framework;
using UForce.FpvFlight.Airframe;
using UnityEngine;

namespace UForce.FpvFlight.Tests
{
	public sealed class BatteryAndAirframeMathTests
	{
		private const float GRAVITY = 9.81f;

		private TestAssets _assets;

		[SetUp]
		public void SetUp()
		{
			_assets = new TestAssets();
		}

		[TearDown]
		public void TearDown()
		{
			_assets.Dispose();
		}

		[Test]
		public void WhenLoaded_ThenVoltageSagsBelowRestingVoltage()
		{
			BatteryModel battery = new();
			battery.Reset(_assets.Battery, 1f);
			float resting = battery.Voltage;

			battery.Update(_assets.Battery, _assets.Airframe, _assets.Airframe.MaxThrust, 0.02f);

			Assert.That(battery.Voltage, Is.LessThan(resting - 0.5f));
			Assert.That(battery.Current, Is.GreaterThan(20f));
		}

		[Test]
		public void WhenDrawingCurrent_ThenUsedCapacityMatchesTheIntegral()
		{
			BatteryModel battery = new();
			battery.Reset(_assets.Battery, 1f);
			float integral = 0f;

			for (int step = 0; step < 3000; step++)
			{
				battery.Update(_assets.Battery, _assets.Airframe, 20f, 0.02f);
				integral += battery.Current * 0.02f / 3.6f;
			}

			Assert.That(battery.UsedMah, Is.EqualTo(integral).Within(0.5f));
			Assert.That(battery.StateOfCharge, Is.EqualTo(1f - integral / _assets.Battery.CapacityMah).Within(1e-3f));
		}

		[Test]
		public void WhenChargeLow_ThenThrustScaleDrops()
		{
			BatteryModel full = new();
			full.Reset(_assets.Battery, 1f);
			full.Update(_assets.Battery, _assets.Airframe, 20f, 0.02f);

			BatteryModel low = new();
			low.Reset(_assets.Battery, 0.1f);
			low.Update(_assets.Battery, _assets.Airframe, 20f, 0.02f);

			Assert.That(low.ThrustScale, Is.LessThan(full.ThrustScale * 0.8f));
		}

		[Test]
		public void WhenBatteryEmpty_ThenNoThrustIsAvailable()
		{
			BatteryModel battery = new();
			battery.Reset(_assets.Battery, 0f);
			battery.Update(_assets.Battery, _assets.Airframe, 0f, 0.02f);
			Assert.That(battery.ThrustScale, Is.EqualTo(0f).Within(1e-5f));
			Assert.IsTrue(battery.IsDepleted);
		}

		[Test]
		public void WhenHoverThrottleDerived_ThenItLiftsExactlyTheWeight()
		{
			float mass = AirframeMath.TotalMass(_assets.Airframe, _assets.Battery, 0.5f);
			float hover = AirframeMath.HoverThrottle(_assets.Airframe, _assets.Profile, mass, GRAVITY, 1f);

			Assert.That(hover, Is.InRange(0.1f, 0.6f));
			Assert.That(AirframeMath.ThrustAtThrottle(_assets.Airframe, _assets.Profile, hover, 1f), Is.EqualTo(mass * GRAVITY).Within(0.01f));
		}

		[Test]
		public void WhenPayloadAdded_ThenHoverThrottleRises()
		{
			float light = AirframeMath.HoverThrottle(_assets.Airframe, _assets.Profile, AirframeMath.TotalMass(_assets.Airframe, _assets.Battery, 0f), GRAVITY, 1f);
			float heavy = AirframeMath.HoverThrottle(_assets.Airframe, _assets.Profile, AirframeMath.TotalMass(_assets.Airframe, _assets.Battery, 0.8f), GRAVITY, 1f);
			Assert.That(heavy, Is.GreaterThan(light + 0.03f));
		}

		[Test]
		public void WhenTooHeavy_ThenHoverIsImpossible()
		{
			float hover = AirframeMath.HoverThrottle(_assets.Airframe, _assets.Profile, 50f, GRAVITY, 1f);
			Assert.IsTrue(float.IsPositiveInfinity(hover));
		}

		[Test]
		public void WhenTopSpeedDerived_ThenThrustAndDragBalance()
		{
			float mass = AirframeMath.TotalMass(_assets.Airframe, _assets.Battery, 0f);
			float speed = AirframeMath.MaxLevelSpeed(_assets.Airframe, mass, GRAVITY, 1f, out float tilt);

			float tiltRadians = tilt * Mathf.Deg2Rad;
			Vector3 bodyUp = new(0f, Mathf.Cos(tiltRadians), Mathf.Sin(tiltRadians));
			Vector3 bodyForward = new(0f, -Mathf.Sin(tiltRadians), Mathf.Cos(tiltRadians));
			Vector3 velocity = new(0f, 0f, speed);
			Quaternion rotation = Quaternion.LookRotation(bodyForward, bodyUp);
			Vector3 drag = rotation * DragModel.BodyDrag(Quaternion.Inverse(rotation) * velocity, _assets.Airframe.DragArea);
			Vector3 net = bodyUp * _assets.Airframe.MaxThrust + drag + Vector3.down * mass * GRAVITY;

			Assert.That(speed, Is.GreaterThan(20f));
			Assert.That(net.magnitude, Is.LessThan(0.05f));
		}

		[Test]
		public void WhenOutputConverted_ThenThrustCurveRoundTrips()
		{
			for (float output = 0f; output <= 1f; output += 0.1f)
			{
				float fraction = ThrustModel.ThrustFraction(output, 0.5f);
				Assert.That(ThrustModel.MotorOutputFromThrustFraction(fraction, 0.5f), Is.EqualTo(output).Within(1e-4f));
			}
		}
	}
}
