using UForce.FpvFlight.RadioInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
using UForce.FpvFlight.Video;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace UForce.FpvFlight.Samples.Editor
{
	// Builds a flyable demo scene next to this sample: ground, obstacles, a spawn point and the demo driver with the five
	// drone types (strike FPV and the Interceptor Drones interceptors). The drone prefabs carry their own camera, FPV video,
	// OSD, sound and rotor wash. With URP the video renderer feature is added to the project's URP renderers.
	public static class FpvFlightDemoSceneBuilder
	{
		private const string TYPES_PATH = "Packages/com.uforce.fpv-flight/Runtime/DroneTypes";
		private static readonly string[] r_typeFolders = { "StrikeFpv", "InterceptorP1Sun", "InterceptorSting", "InterceptorZorelitV5", "InterceptorSpys" };

		[MenuItem("Tools/UForce/FPV Flight/Build Demo Scene")]
		public static void Build()
		{
			string folder = GetSampleFolder();
			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
			EnsureVideoFeature();
			EnsureTextMeshProEssentials();
#else
			Debug.LogWarning($"[{nameof(FpvFlightDemoSceneBuilder)}] URP or uGUI is missing: the drones have no FPV video and no OSD.");
#endif

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

			Camera main = Camera.main;
			if (main)
			{
				Object.DestroyImmediate(main.gameObject);
			}

			var spawn = new GameObject("Spawn Point").transform;
			spawn.position = new Vector3(0f, 0.5f, 0f);

			var demo = new GameObject("FPV Demo");
			var driver = demo.AddComponent<FpvFlightDemoDriver>();
			SetReference(driver, "_actions", Load<InputActionAsset>($"{folder}/FpvDemoInputActions.inputactions"));
			SetReference(driver, "_calibration", Load<RcCalibrationProfile>($"{folder}/Presets/DefaultRcCalibration.asset"));
			SetReference(driver, "_spawnPoint", spawn);

			var serialized = new SerializedObject(driver);
			SerializedProperty types = serialized.FindProperty("_types");
			types.arraySize = r_typeFolders.Length;
			for (int index = 0; index < r_typeFolders.Length; index++)
			{
				string typeFolder = r_typeFolders[index];
				var type = Load<FpvDroneType>($"{TYPES_PATH}/{typeFolder}/{typeFolder}_Type.asset");
				if (type == null)
				{
					Debug.LogError($"[{nameof(FpvFlightDemoSceneBuilder)}] Drone type {typeFolder} is missing: run Tools > UForce > FPV Flight > Build Drone Types.");
				}

				types.GetArrayElementAtIndex(index).objectReferenceValue = type;
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();

			var overlay = demo.AddComponent<FpvTelemetryOverlay>();
			SetReference(overlay, "_driver", driver);

			string path = $"{folder}/FpvFlightDemo.unity";
			EditorSceneManager.SaveScene(scene, path);
			Debug.Log($"[{nameof(FpvFlightDemoSceneBuilder)}] Saved {path}.");
		}

#if UFORCE_FPV_URP && UFORCE_FPV_UGUI
		// Adds FpvVideoRendererFeature to every URP renderer the project's pipeline assets use, as the renderer inspector does.
		private static void EnsureVideoFeature()
		{
			var pipelines = new System.Collections.Generic.HashSet<UniversalRenderPipelineAsset>();
			if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultPipeline)
			{
				pipelines.Add(defaultPipeline);
			}

			for (int level = 0; level < QualitySettings.count; level++)
			{
				if (QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset levelPipeline)
				{
					pipelines.Add(levelPipeline);
				}
			}

			foreach (UniversalRenderPipelineAsset pipeline in pipelines)
			{
				SerializedProperty renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
				for (int index = 0; renderers != null && index < renderers.arraySize; index++)
				{
					if (renderers.GetArrayElementAtIndex(index).objectReferenceValue is ScriptableRendererData renderer)
					{
						AddVideoFeature(renderer);
					}
				}
			}

			AssetDatabase.SaveAssets();
		}

		private static void AddVideoFeature(ScriptableRendererData renderer)
		{
			foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
			{
				if (existing is FpvVideoRendererFeature)
				{
					return;
				}
			}

			var feature = ScriptableObject.CreateInstance<FpvVideoRendererFeature>();
			feature.name = "FpvVideo";
			AssetDatabase.AddObjectToAsset(feature, renderer);
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

			var serialized = new SerializedObject(renderer);
			SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
			SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
			features.arraySize++;
			features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
			map.arraySize++;
			map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			renderer.SetDirty();
			EditorUtility.SetDirty(renderer);
			Debug.Log($"[{nameof(FpvFlightDemoSceneBuilder)}] Added the FPV video feature to {AssetDatabase.GetAssetPath(renderer)}.");
		}

		// The OSD fonts use the TextMesh Pro SDF shader from TMP Essential Resources.
		private static void EnsureTextMeshProEssentials()
		{
			if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
			{
				return;
			}

			Debug.Log($"[{nameof(FpvFlightDemoSceneBuilder)}] Importing TMP Essential Resources for the OSD fonts.");
			TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
		}
#endif

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

		private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

		private static void SetReference(Object target, string property, Object value)
		{
			var serialized = new SerializedObject(target);
			serialized.FindProperty(property).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
