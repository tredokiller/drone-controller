using System.Collections.Generic;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Audio;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Crash;
using UForce.FpvFlight.Interceptor;
using UForce.FpvFlight.Osd;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.RotorWash;
using UForce.FpvFlight.Video;
using UForce.FpvFlight.Visuals;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.DroneTypes.Editor
{
	// Rebuilds the drone types of Interceptor Drones and the strike FPV: materials, the two playable prefabs (strike FPV and
	// interceptor) and the five type assets. Model offsets, camera mounts and tilt angles follow the Interceptor prefabs.
	public static class FpvDroneTypesBuilder
	{
		private const string ROOT = "Packages/com.uforce.fpv-flight/Runtime/DroneTypes";
		private const string AUDIO = "Packages/com.uforce.fpv-flight/Runtime/Audio/Clips";
		private const string VIDEO_SHADER_PATH = "Packages/com.uforce.fpv-flight/Runtime/Video/Shaders/FpvVideo.shader";
		private const string VIDEO_PROFILE_PATH = "Packages/com.uforce.fpv-flight/Runtime/Video/Profiles/FpvVideo_Digital.asset";
		private const string OSD_PREFAB_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Prefabs/FpvOsd.prefab";
		private const string LIT_SHADER = "Universal Render Pipeline/Lit";
		private const int VOLUME_LAYER = 30;
		private const int OSD_LAYER = 31;

		private static readonly float[] r_interceptorUptilts = { 90f, 45f, 0f };
		private static readonly float[] r_strikeUptilts = { 25f };

		[MenuItem("Tools/UForce/FPV Flight/Build Drone Types")]
		public static void Build()
		{
			ConfigureTexture($"{ROOT}/Models/Fpv/Fpv_BaseMap.png", TextureImporterType.Default, true);
			ConfigureTexture($"{ROOT}/Models/Fpv/Fpv_MetallicSmoothness.png", TextureImporterType.Default, false);
			ConfigureTexture($"{ROOT}/Models/Fpv/Fpv_Occlusion.png", TextureImporterType.Default, false);
			ConfigureTexture($"{ROOT}/Models/P1Sun/P1Sun_BaseMap.png", TextureImporterType.Default, true);
			ConfigureTexture($"{ROOT}/Models/P1Sun/P1Sun_Normal.png", TextureImporterType.NormalMap, false);
			ConfigureTexture($"{ROOT}/Models/P1Sun/P1Sun_MetallicSmoothness.png", TextureImporterType.Default, false);
			ConfigureModel($"{ROOT}/Models/Fpv/Fpv.fbx");
			ConfigureModel($"{ROOT}/Models/P1Sun/P1Sun.fbx");

			Material fpvMaterial = CreateLitMaterial($"{ROOT}/Models/Fpv/Fpv.mat", "Fpv", null, true);
			Material p1Material = CreateLitMaterial($"{ROOT}/Models/P1Sun/P1Sun.mat", "P1Sun", $"{ROOT}/Models/P1Sun/P1Sun_Normal.png", false);

			FpvDroneType strike = CreateType("StrikeFpv", "strike-fpv", "Strike FPV",
				"7\" strike FPV drone carrying a warhead. Fixed 25° camera uptilt.", r_strikeUptilts, 0, 90f);
			FpvDroneType p1Sun = CreateType("InterceptorP1Sun", "p1-sun", "P1-SUN",
				"Interceptor, 300 km/h. Camera tilt on switch SC: 90° (along the thrust axis), 45°, 0°.", r_interceptorUptilts, 0, 50f);
			FpvDroneType sting = CreateType("InterceptorSting", "sting", "Sting",
				"Interceptor, 315 km/h, light and agile.", r_interceptorUptilts, 0, 50f);
			FpvDroneType zorelit = CreateType("InterceptorZorelitV5", "zorelit-v5", "Zorelit v5",
				"Interceptor, 416 km/h.", r_interceptorUptilts, 0, 50f);
			FpvDroneType spys = CreateType("InterceptorSpys", "spys", "SPYS",
				"Interceptor, 450 km/h, the fastest.", r_interceptorUptilts, 0, 50f);

			// Interceptor Drones' presets (ManualDroneCustomizationConfig*): the interceptors fly with its flight model.
			p1Sun.SetInterceptorFlight(CreateInterceptorFlight("InterceptorP1Sun", 300f, new Vector3(700f, 500f, 700f), 7f, 2.037953f, true, 30.901697f));
			sting.SetInterceptorFlight(CreateInterceptorFlight("InterceptorSting", 315f, new Vector3(600f, 400f, 600f), 5f, 1.1f, false, 10f));
			zorelit.SetInterceptorFlight(CreateInterceptorFlight("InterceptorZorelitV5", 416f, new Vector3(650f, 450f, 650f), 6f, 1.3f, false, 10f));
			spys.SetInterceptorFlight(CreateInterceptorFlight("InterceptorSpys", 450f, new Vector3(700f, 450f, 700f), 8f, 0.9f, false, 10f));

			GameObject strikePrefab = BuildPrefab(new FpvDronePrefabSpec
			{
				Name = "FpvStrike",
				ModelPath = $"{ROOT}/Models/Fpv/Fpv.fbx",
				Material = fpvMaterial,
				MountPosition = new Vector3(0f, 0.192f, 0f),
				ModelPosition = new Vector3(0f, -0.184f, 0f),
				ModelRotation = new Vector3(-90f, 180f, 0f),
				// Komandor's mount for this model (0, 0.163, 0.23 from the root): in front of the frame, clear of the arms.
				CameraPosition = new Vector3(0f, -0.029f, 0.23f),
				PropellerNames = new[] { "Propeller 1", "Propeller 2", "Propeller 3", "Propeller 4" },
				IsPropellerPivotNeeded = false,
				PropellerAxis = Vector3.forward,
				Type = strike
			});

			GameObject interceptorPrefab = BuildPrefab(new FpvDronePrefabSpec
			{
				Name = "FpvInterceptor",
				ModelPath = $"{ROOT}/Models/P1Sun/P1Sun.fbx",
				Material = p1Material,
				MountPosition = new Vector3(0f, 0.192f, 0f),
				ModelPosition = new Vector3(0.0775f, -0.746f, 0.1f),
				ModelRotation = new Vector3(0f, 180f, 0f),
				CameraPosition = new Vector3(0f, 0.002f, 0.1035f),
				CameraNearClip = 0.15f,
				PropellerNames = new[] { "P1", "P2", "P3", "P4" },
				IsPropellerPivotNeeded = true,
				PropellerAxis = Vector3.up,
				Type = p1Sun
			});

			SetPrefab(strike, strikePrefab);
			SetPrefab(p1Sun, interceptorPrefab);
			SetPrefab(sting, interceptorPrefab);
			SetPrefab(zorelit, interceptorPrefab);
			SetPrefab(spys, interceptorPrefab);
			AssetDatabase.SaveAssets();
			Debug.Log($"[{nameof(FpvDroneTypesBuilder)}] Built 2 prefabs and 5 drone types in {ROOT}.");
		}

		private static void ConfigureTexture(string path, TextureImporterType type, bool isSrgb)
		{
			if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
			{
				Debug.LogError($"[{nameof(FpvDroneTypesBuilder)}] Missing texture {path}.");
				return;
			}

			importer.textureType = type;
			importer.sRGBTexture = isSrgb;
			importer.maxTextureSize = 2048;
			importer.mipmapEnabled = true;
			importer.SaveAndReimport();
		}

		private static void ConfigureModel(string path)
		{
			if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
			{
				Debug.LogError($"[{nameof(FpvDroneTypesBuilder)}] Missing model {path}.");
				return;
			}

			importer.materialImportMode = ModelImporterMaterialImportMode.None;
			importer.importAnimation = false;
			importer.importCameras = false;
			importer.importLights = false;
			importer.SaveAndReimport();
		}

		private static Material CreateLitMaterial(string path, string prefix, string normalPath, bool hasOcclusion)
		{
			string folder = path.Substring(0, path.LastIndexOf('/'));
			var material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = new Material(Shader.Find(LIT_SHADER));
				AssetDatabase.CreateAsset(material, path);
			}

			material.shader = Shader.Find(LIT_SHADER);
			material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{prefix}_BaseMap.png"));
			material.SetColor("_BaseColor", Color.white);
			material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{prefix}_MetallicSmoothness.png"));
			material.SetFloat("_Smoothness", 1f);
			material.SetFloat("_Metallic", 1f);
			material.EnableKeyword("_METALLICSPECGLOSSMAP");

			if (normalPath != null)
			{
				material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
				material.SetFloat("_BumpScale", 1f);
				material.EnableKeyword("_NORMALMAP");
			}

			if (hasOcclusion)
			{
				material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{prefix}_Occlusion.png"));
				material.SetFloat("_OcclusionStrength", 1f);
				material.EnableKeyword("_OCCLUSIONMAP");
			}

			EditorUtility.SetDirty(material);
			return material;
		}

		private static FpvDroneType CreateType(string folderName, string id, string displayName, string description, float[] uptilts, int startPosition, float fieldOfView)
		{
			string folder = $"{ROOT}/{folderName}";
			string path = $"{folder}/{folderName}_Type.asset";
			var type = AssetDatabase.LoadAssetAtPath<FpvDroneType>(path);
			if (type == null)
			{
				type = ScriptableObject.CreateInstance<FpvDroneType>();
				AssetDatabase.CreateAsset(type, path);
			}

			type.Configure(
				id,
				displayName,
				description,
				AssetDatabase.LoadAssetAtPath<AirframeDefinition>($"{folder}/{folderName}_Airframe.asset"),
				AssetDatabase.LoadAssetAtPath<BatteryDefinition>($"{folder}/{folderName}_Battery.asset"),
				AssetDatabase.LoadAssetAtPath<RatesProfile>($"{folder}/{folderName}_Rates.asset"),
				AssetDatabase.LoadAssetAtPath<FlightControllerProfile>($"{ROOT}/DefaultFlightController.asset"),
				uptilts,
				startPosition,
				fieldOfView,
				type.Prefab);
			EditorUtility.SetDirty(type);
			return type;
		}

		// maxRates: x pitch, y yaw, z roll, deg/s. Sensitivity 50, horizontal thrust, thrust assist and damping are the same
		// in every Interceptor preset.
		private static InterceptorFlightProfile CreateInterceptorFlight(string folderName, float maxSpeedKmh, Vector3 maxRates, float throttlePower, float baseMassKg,
			bool isHardStopOnRelease, float angularBrakeAccel)
		{
			string path = $"{ROOT}/{folderName}/{folderName}_InterceptorFlight.asset";
			var profile = AssetDatabase.LoadAssetAtPath<InterceptorFlightProfile>(path);
			if (profile == null)
			{
				profile = ScriptableObject.CreateInstance<InterceptorFlightProfile>();
				AssetDatabase.CreateAsset(profile, path);
			}

			profile.Configure(maxSpeedKmh, maxRates, new Vector3(50f, 50f, 50f), throttlePower, 38.736492f, 0.119012356f, baseMassKg, 0.18026066f, 1.5450835f,
				isHardStopOnRelease, angularBrakeAccel);
			EditorUtility.SetDirty(profile);
			return profile;
		}

		private static void SetPrefab(FpvDroneType type, GameObject prefab)
		{
			type.Configure(type.Id, type.DisplayName, type.Description, type.Airframe, type.Battery, type.Rates, type.Profile,
				type.CameraUptilts, type.CameraStartPosition, type.CameraFieldOfView, prefab);
			EditorUtility.SetDirty(type);
		}

		private static GameObject BuildPrefab(FpvDronePrefabSpec spec)
		{
			var root = new GameObject(spec.Name);
			try
			{
				var body = root.AddComponent<Rigidbody>();
				body.interpolation = RigidbodyInterpolation.Interpolate;
				body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

				var controller = root.AddComponent<FpvFlightController>();
				SetReference(controller, "_airframe", spec.Type.Airframe);
				SetReference(controller, "_battery", spec.Type.Battery);
				SetReference(controller, "_rates", spec.Type.Rates);
				SetReference(controller, "_profile", spec.Type.Profile);
				SetReference(controller, "_interceptorFlight", spec.Type.InterceptorFlight);
				root.AddComponent<FpvCrashDetector>();

				var mount = new GameObject("Drone").transform;
				mount.SetParent(root.transform, false);
				mount.localPosition = spec.MountPosition;

				Transform model = InstantiateModel(spec, mount);
				List<Transform> propellers = FindPropellers(model, spec);
				AddBodyCollider(root, model, propellers);
				Transform camera = BuildCamera(spec, mount);

				AddAudio(root);
				root.AddComponent<FpvRotorWash>();

				var propellerSpinner = root.AddComponent<FpvPropellers>();
				var spinner = new SerializedObject(propellerSpinner);
				SerializedProperty list = spinner.FindProperty("_propellers");
				list.arraySize = propellers.Count;
				for (int index = 0; index < propellers.Count; index++)
				{
					list.GetArrayElementAtIndex(index).objectReferenceValue = propellers[index];
				}

				spinner.FindProperty("_spinAxis").vector3Value = spec.PropellerAxis;
				spinner.ApplyModifiedPropertiesWithoutUndo();

				var signature = root.AddComponent<FpvThermalSignature>();
				SetRenderers(signature, model.GetComponentsInChildren<Renderer>(true));

				string path = $"{ROOT}/Prefabs/{spec.Name}.prefab";
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
				GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
				Debug.Log($"[{nameof(FpvDroneTypesBuilder)}] Saved {path} (camera '{camera.name}', {propellers.Count} propellers).");
				return prefab;
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static Transform InstantiateModel(FpvDronePrefabSpec spec, Transform mount)
		{
			var asset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
			var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
			PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			model.name = "Model";
			model.transform.SetParent(mount, false);
			model.transform.localPosition = spec.ModelPosition;
			model.transform.localRotation = Quaternion.Euler(spec.ModelRotation);

			foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
			{
				var materials = new Material[renderer.sharedMaterials.Length];
				for (int index = 0; index < materials.Length; index++)
				{
					materials[index] = spec.Material;
				}

				renderer.sharedMaterials = materials.Length > 0 ? materials : new[] { spec.Material };
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
			}

			return model.transform;
		}

		private static List<Transform> FindPropellers(Transform model, FpvDronePrefabSpec spec)
		{
			var propellers = new List<Transform>();
			foreach (string name in spec.PropellerNames)
			{
				Transform propeller = FindDeep(model, name);
				if (propeller == null)
				{
					Debug.LogWarning($"[{nameof(FpvDroneTypesBuilder)}] {spec.Name}: no propeller '{name}' in the model.");
					continue;
				}

				if (spec.IsPropellerPivotNeeded && propeller.TryGetComponent(out Renderer renderer))
				{
					var pivot = new GameObject($"Propeller {propellers.Count + 1}").transform;
					pivot.SetParent(propeller.parent, false);
					pivot.position = renderer.bounds.center;
					pivot.rotation = propeller.parent.rotation;
					propeller.SetParent(pivot, true);
					propeller = pivot;
				}

				propellers.Add(propeller);
			}

			return propellers;
		}

		private static Transform FindDeep(Transform parent, string name)
		{
			foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
			{
				if (child.name == name)
				{
					return child;
				}
			}

			return null;
		}

		// One box around the airframe without the propeller discs, in the root's space.
		private static void AddBodyCollider(GameObject root, Transform model, List<Transform> propellers)
		{
			bool hasBounds = false;
			var bounds = new Bounds();
			foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
			{
				bool isPropeller = false;
				foreach (Transform propeller in propellers)
				{
					if (renderer.transform.IsChildOf(propeller))
					{
						isPropeller = true;
						break;
					}
				}

				if (isPropeller)
				{
					continue;
				}

				if (hasBounds)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				else
				{
					bounds = renderer.bounds;
					hasBounds = true;
				}
			}

			var collider = root.AddComponent<BoxCollider>();
			if (hasBounds)
			{
				collider.center = root.transform.InverseTransformPoint(bounds.center);
				collider.size = bounds.size;
			}
		}

		private static Transform BuildCamera(FpvDronePrefabSpec spec, Transform mount)
		{
			var cameraObject = new GameObject("FPV Camera", typeof(Camera), typeof(AudioListener));
			cameraObject.tag = "MainCamera";
			Transform cameraTransform = cameraObject.transform;
			cameraTransform.SetParent(mount, false);
			cameraTransform.localPosition = spec.CameraPosition;

			var camera = cameraObject.GetComponent<Camera>();
			camera.fieldOfView = spec.Type.CameraFieldOfView;
			camera.nearClipPlane = spec.CameraNearClip;
			camera.farClipPlane = 3000f;

			cameraObject.AddComponent<FpvOnboardMicrophone>();

			var servo = new GameObject("Servo Audio", typeof(AudioSource));
			servo.transform.SetParent(cameraTransform, false);
			var servoAudio = servo.GetComponent<AudioSource>();
			servoAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO}/FpvCameraServo.ogg");
			servoAudio.playOnAwake = false;
			servoAudio.loop = true;
			servoAudio.spatialBlend = 0f;
			servoAudio.volume = 0.6f;

			var tilt = cameraObject.AddComponent<FpvCameraTilt>();
			var tiltObject = new SerializedObject(tilt);
			SerializedProperty angles = tiltObject.FindProperty("_uptiltAngles");
			angles.arraySize = spec.Type.CameraUptilts.Length;
			for (int index = 0; index < spec.Type.CameraUptilts.Length; index++)
			{
				angles.GetArrayElementAtIndex(index).floatValue = spec.Type.CameraUptilts[index];
			}

			tiltObject.FindProperty("_startPosition").intValue = spec.Type.CameraStartPosition;
			tiltObject.FindProperty("_servoAudio").objectReferenceValue = servoAudio;
			tiltObject.ApplyModifiedPropertiesWithoutUndo();

			var video = cameraObject.AddComponent<FpvVideoCamera>();
			SetReference(video, "_shader", AssetDatabase.LoadAssetAtPath<Shader>(VIDEO_SHADER_PATH));
			SetReference(video, "_profile", AssetDatabase.LoadAssetAtPath<FpvVideoProfile>(VIDEO_PROFILE_PATH));
			SetInt(video, "_volumeLayer", VOLUME_LAYER);
			cameraObject.AddComponent<FpvLensRain>();
			cameraObject.AddComponent<FpvCameraShake>();

			var osd = cameraObject.AddComponent<FpvOsdCamera>();
			SetReference(osd, "_osdPrefab", AssetDatabase.LoadAssetAtPath<Canvas>(OSD_PREFAB_PATH));
			SetInt(osd, "_osdLayer", OSD_LAYER);
			cameraObject.AddComponent<FpvOsdFlightSource>();
			return cameraTransform;
		}

		private static void AddAudio(GameObject root)
		{
			var engine = new GameObject("Engine Audio", typeof(AudioSource));
			engine.transform.SetParent(root.transform, false);
			var engineSource = engine.GetComponent<AudioSource>();
			engineSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO}/FpvMotorLoop.ogg");
			engineSource.loop = true;
			engineSource.playOnAwake = false;
			engineSource.spatialBlend = 1f;
			engineSource.dopplerLevel = 0f;
			engineSource.rolloffMode = AudioRolloffMode.Logarithmic;
			engineSource.minDistance = 1f;
			engineSource.maxDistance = 500f;

			var motorAudio = root.AddComponent<FpvMotorAudio>();
			SetReference(motorAudio, "_audioSource", engineSource);

			var collisionSource = root.AddComponent<AudioSource>();
			collisionSource.playOnAwake = false;
			collisionSource.spatialBlend = 1f;
			collisionSource.minDistance = 1f;
			collisionSource.maxDistance = 150f;

			var collisionAudio = root.AddComponent<FpvCollisionAudio>();
			SetReference(collisionAudio, "_audioSource", collisionSource);
			SetReference(collisionAudio, "_touchClip", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO}/FpvTouch.ogg"));
			SetReference(collisionAudio, "_hitClip", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AUDIO}/FpvHit.ogg"));
		}

		private static void SetRenderers(FpvThermalSignature signature, Renderer[] renderers)
		{
			var serialized = new SerializedObject(signature);
			SerializedProperty list = serialized.FindProperty("_renderers");
			list.arraySize = renderers.Length;
			for (int index = 0; index < renderers.Length; index++)
			{
				list.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetReference(Object target, string property, Object value)
		{
			var serialized = new SerializedObject(target);
			serialized.FindProperty(property).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetInt(Object target, string property, int value)
		{
			var serialized = new SerializedObject(target);
			serialized.FindProperty(property).intValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
