using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.RadioInput;
using UForce.FpvFlight.Rates;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UForce.FpvFlight.Samples.Editor
{
	// Builds a flyable demo scene next to this sample: ground, obstacles and a drone with an uptilted FPV camera.
	public static class FpvFlightDemoSceneBuilder
	{
		private const float CAMERA_UPTILT = 25f;

		[MenuItem("Tools/UForce/FPV Flight/Build Demo Scene")]
		public static void Build()
		{
			string folder = GetSampleFolder();
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.localScale = new Vector3(100f, 1f, 100f);

			for (int index = 0; index < 24; index++)
			{
				GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
				obstacle.name = $"Obstacle {index}";
				float angle = index * 15f * Mathf.Deg2Rad;
				float radius = 40f + index % 4 * 25f;
				float height = 4f + index % 5 * 6f;
				obstacle.transform.position = new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius);
				obstacle.transform.localScale = new Vector3(3f, height, 3f);
			}

			GameObject drone = CreateDrone(folder);
			Camera main = Camera.main;
			if (main)
			{
				Object.DestroyImmediate(main.gameObject);
			}

			GameObject cameraObject = new("FPV Camera", typeof(Camera), typeof(AudioListener));
			cameraObject.tag = "MainCamera";
			cameraObject.transform.SetParent(drone.transform, false);
			cameraObject.transform.localPosition = new Vector3(0f, 0.05f, 0.12f);
			cameraObject.transform.localRotation = Quaternion.Euler(-CAMERA_UPTILT, 0f, 0f);
			var fpvCamera = cameraObject.GetComponent<Camera>();
			fpvCamera.fieldOfView = 90f;
			fpvCamera.nearClipPlane = 0.05f;

			string path = $"{folder}/FpvFlightDemo.unity";
			EditorSceneManager.SaveScene(scene, path);
			Debug.Log($"[{nameof(FpvFlightDemoSceneBuilder)}] Saved {path}.");
		}

		private static GameObject CreateDrone(string folder)
		{
			GameObject drone = new("FPV Drone");
			drone.transform.position = new Vector3(0f, 0.2f, 0f);
			var collider = drone.AddComponent<BoxCollider>();
			collider.size = new Vector3(0.32f, 0.1f, 0.32f);

			GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
			frame.name = "Frame";
			Object.DestroyImmediate(frame.GetComponent<BoxCollider>());
			frame.transform.SetParent(drone.transform, false);
			frame.transform.localScale = new Vector3(0.32f, 0.06f, 0.32f);

			var body = drone.AddComponent<Rigidbody>();
			body.interpolation = RigidbodyInterpolation.Interpolate;
			body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

			var controller = drone.AddComponent<FpvFlightController>();
			SetReference(controller, "_airframe", Load<AirframeDefinition>(folder, "Presets/DefaultAirframe.asset"));
			SetReference(controller, "_battery", Load<BatteryDefinition>(folder, "Presets/DefaultBattery.asset"));
			SetReference(controller, "_rates", Load<RatesProfile>(folder, "Presets/BetaflightDefaultRates.asset"));
			SetReference(controller, "_profile", Load<FlightControllerProfile>(folder, "Presets/DefaultFlightController.asset"));

			var driver = drone.AddComponent<FpvFlightDemoDriver>();
			SetReference(driver, "_actions", Load<InputActionAsset>(folder, "FpvDemoInputActions.inputactions"));
			SetReference(driver, "_calibration", Load<RcCalibrationProfile>(folder, "Presets/DefaultRcCalibration.asset"));

			var overlay = drone.AddComponent<FpvTelemetryOverlay>();
			SetReference(overlay, "_controller", controller);
			SetReference(overlay, "_driver", driver);
			return drone;
		}

		private static string GetSampleFolder()
		{
			string[] guids = AssetDatabase.FindAssets("FpvDemoInputActions t:InputActionAsset");
			if (guids.Length == 0)
			{
				return "Assets";
			}

			string path = AssetDatabase.GUIDToAssetPath(guids[0]);
			return path.Substring(0, path.LastIndexOf('/'));
		}

		private static T Load<T>(string folder, string relativePath) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{folder}/{relativePath}");

		private static void SetReference(Object target, string property, Object value)
		{
			var serialized = new SerializedObject(target);
			serialized.FindProperty(property).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
