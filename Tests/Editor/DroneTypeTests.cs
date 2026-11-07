using NUnit.Framework;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Crash;
using UForce.FpvFlight.Visuals;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Tests
{
	public sealed class DroneTypeTests
	{
		private const string TYPES_PATH = "Packages/com.uforce.fpv-flight/Runtime/DroneTypes";
		private const float GRAVITY = 9.81f;

		// Derived top speed of each preset against the speed configured in Interceptor Drones (strike FPV: Komandor's 145).
		[TestCase("StrikeFpv", 145f)]
		[TestCase("InterceptorP1Sun", 300f)]
		[TestCase("InterceptorSting", 315f)]
		[TestCase("InterceptorZorelitV5", 416f)]
		[TestCase("InterceptorSpys", 450f)]
		public void WhenPresetLoaded_ThenDerivedTopSpeedMatchesInterceptor(string folder, float expectedKmh)
		{
			var airframe = AssetDatabase.LoadAssetAtPath<AirframeDefinition>($"{TYPES_PATH}/{folder}/{folder}_Airframe.asset");
			var battery = AssetDatabase.LoadAssetAtPath<BatteryDefinition>($"{TYPES_PATH}/{folder}/{folder}_Battery.asset");
			Assert.IsNotNull(airframe, $"{folder} airframe");
			Assert.IsNotNull(battery, $"{folder} battery");

			float mass = AirframeMath.TotalMass(airframe, battery, 0f);
			float speedKmh = AirframeMath.MaxLevelSpeed(airframe, mass, GRAVITY, 1f, out _) * 3.6f;

			Assert.That(speedKmh, Is.EqualTo(expectedKmh).Within(expectedKmh * 0.06f));
		}

		[Test]
		public void WhenTypesLoaded_ThenEachHasFlightAssetsAndPrefab()
		{
			string[] folders = { "StrikeFpv", "InterceptorP1Sun", "InterceptorSting", "InterceptorZorelitV5", "InterceptorSpys" };
			foreach (string folder in folders)
			{
				var type = AssetDatabase.LoadAssetAtPath<FpvDroneType>($"{TYPES_PATH}/{folder}/{folder}_Type.asset");
				Assert.IsNotNull(type, folder);
				Assert.IsNotNull(type.Airframe, $"{folder} airframe");
				Assert.IsNotNull(type.Rates, $"{folder} rates");
				Assert.IsNotNull(type.Profile, $"{folder} profile");
				Assert.IsNotNull(type.Prefab, $"{folder} prefab");
				Assert.IsNotNull(type.Prefab.GetComponent<FpvFlightController>(), $"{folder} controller");
				Assert.IsNotNull(type.Prefab.GetComponentInChildren<FpvCameraTilt>(true), $"{folder} camera tilt");
				Assert.That(type.HasCameraSwitch, Is.EqualTo(folder != "StrikeFpv"), $"{folder}: only interceptors have the camera switch.");
			}
		}

		[TestCase(0.5f, -1f, false)]
		[TestCase(5f, 0.9f, false)]
		[TestCase(5f, 0.2f, true)]
		[TestCase(12f, 0.95f, true)]
		[TestCase(1.5f, 0f, true)]
		public void WhenCollisionClassified_ThenInterceptorRulesApply(float speed, float normalUpDot, bool isCrash)
		{
			Assert.That(FpvCrashDetector.IsCrash(speed, normalUpDot), Is.EqualTo(isCrash));
		}

		[Test]
		public void WhenCameraTiltPositionSet_ThenUptiltMatchesInterceptorAngles()
		{
			var camera = new GameObject("TiltTestCamera");
			try
			{
				var tilt = camera.AddComponent<FpvCameraTilt>();
				tilt.SetAngles(new[] { 90f, 45f, 0f }, 0);
				Assert.That(Vector3.Dot(camera.transform.forward, Vector3.up), Is.EqualTo(1f).Within(1e-3f), "Position 0 looks along the thrust axis.");

				tilt.SetPosition(1, true);
				Assert.That(tilt.CurrentUptilt, Is.EqualTo(45f).Within(1e-3f));

				tilt.SetPosition(2, true);
				Assert.That(Vector3.Dot(camera.transform.forward, Vector3.forward), Is.EqualTo(1f).Within(1e-3f), "Position 2 looks along the body.");

				tilt.Cycle();
				Assert.That(tilt.Position, Is.EqualTo(0), "Cycling wraps around.");
			}
			finally
			{
				Object.DestroyImmediate(camera);
			}
		}
	}
}
