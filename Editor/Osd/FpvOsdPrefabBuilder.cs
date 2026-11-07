using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UForce.FpvFlight.Osd.Editor
{
	// Rebuilds Runtime/Osd/Prefabs/FpvOsd.prefab: the MAIN OSD panel of the Interceptor Drones P1-SUN prefab, element by
	// element (positions, sizes, fonts, colours and outlines as in the original). The heading arrow of the original is an
	// MGAssets sprite, so it is drawn as a two-bar chevron here; FLIGHT MODE and WARNING are additions for the flight core,
	// and the hidden Betaflight-style extras (throttle, flight time, current, mAh, pack voltage, charge, LQ, vertical speed,
	// pitch and roll) are shown by a pilot's layout. Element names are the ids of FpvOsdElementCatalog.
	public static class FpvOsdPrefabBuilder
	{
		public const string PREFAB_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Prefabs/FpvOsd.prefab";
		public const string DEFAULTS_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Resources/UForce/FpvFlight/FpvOsdDefaults.asset";
		public const string PREVIEW_BACKGROUND_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Preview/FpvOsdPreviewBackground.jpg";

		private const string MONO_FONT_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Fonts/UAV-OSD-Mono SDF.asset";
		private const string SANS_FONT_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Fonts/UAV-OSD-Sans-Mono SDF.asset";

		private static readonly Color s_strongShadow = new(0f, 0f, 0f, 0.9f);
		private static readonly Color s_softShadow = new(0f, 0f, 0f, 0.5f);
		private static readonly Color s_sideLabelColor = new(0.84313726f, 0.84313726f, 0.84313726f, 0.5019608f);

		private static TMP_FontAsset s_mono;
		private static TMP_FontAsset s_sans;

		[MenuItem("Tools/UForce/FPV Flight/Build OSD Prefab")]
		public static void Build()
		{
			s_mono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MONO_FONT_PATH);
			s_sans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SANS_FONT_PATH);
			if (s_mono == null || s_sans == null)
			{
				Debug.LogError($"[{nameof(FpvOsdPrefabBuilder)}] UAV-OSD fonts are missing.");
				return;
			}

			var root = new GameObject("FpvOsd", typeof(RectTransform));
			try
			{
				BuildCanvas(root);
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PREFAB_PATH));
				PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
				Debug.Log($"[{nameof(FpvOsdPrefabBuilder)}] Saved {PREFAB_PATH}.");
			}
			finally
			{
				Object.DestroyImmediate(root);
			}

			SaveDefaults();
		}

		// Resources/UForce/FpvFlight/FpvOsdDefaults: the prefab and the preview picture for FpvOsdPreview.
		private static void SaveDefaults()
		{
			var defaults = AssetDatabase.LoadAssetAtPath<FpvOsdDefaults>(DEFAULTS_PATH);
			if (defaults == null)
			{
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DEFAULTS_PATH));
				defaults = ScriptableObject.CreateInstance<FpvOsdDefaults>();
				AssetDatabase.CreateAsset(defaults, DEFAULTS_PATH);
			}

			defaults.Configure(AssetDatabase.LoadAssetAtPath<Canvas>(PREFAB_PATH), AssetDatabase.LoadAssetAtPath<Texture2D>(PREVIEW_BACKGROUND_PATH));
			EditorUtility.SetDirty(defaults);
			AssetDatabase.SaveAssets();
		}

		private static void BuildCanvas(GameObject root)
		{
			var canvas = root.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceCamera;
			canvas.planeDistance = 0.16f;
			canvas.sortingOrder = 1;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

			var scaler = root.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(2000f, 2000f);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = 0.5f;

			root.AddComponent<FpvHudTextAtlasGuard>();
			root.AddComponent<CanvasGroup>();
			var view = root.AddComponent<FpvOsdView>();

			RectTransform panel = Node("MainOsdPanel", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
			panel.gameObject.AddComponent<CanvasRenderer>();

			TMP_Text vtx = Text("VtxSignal", panel, TopLeft, new Vector2(72f, -55f), "VTX 100%", s_mono, 40f, TextAlignmentOptions.TopLeft, Color.white, s_strongShadow);
			TMP_Text arm = Text("ArmStatus", panel, TopLeft, new Vector2(72f, -105f), "DISARMED", s_mono, 40f, TextAlignmentOptions.TopLeft, Color.red, s_strongShadow);
			TMP_Text payload = Text("BombStatus", panel, TopLeft, new Vector2(72f, -155f), "BOMB", s_mono, 40f, TextAlignmentOptions.TopLeft, Color.white, s_strongShadow);
			TMP_Text mode = Text("FlightMode", panel, TopLeft, new Vector2(72f, -205f), "ACRO", s_mono, 40f, TextAlignmentOptions.TopLeft, Color.white, s_strongShadow);
			TMP_Text battery = Text("BatteryVoltage", panel, BottomLeft, new Vector2(72f, 55f), "BAT 4.10V", s_mono, 40f, TextAlignmentOptions.BottomLeft, Color.white, s_strongShadow);
			TMP_Text altitude = Text("Altitude", panel, TopRight, new Vector2(-72f, -55f), "ALT 17M", s_mono, 40f, TextAlignmentOptions.TopRight, Color.white, s_strongShadow);
			TMP_Text speed = Text("Speed", panel, TopRight, new Vector2(-72f, -105f), "SPD 0KM/H", s_mono, 40f, TextAlignmentOptions.TopRight, Color.white, s_strongShadow);

			// The view turns the warning text on only while there is something to warn about; the element stays active so a
			// layout can show or hide it.
			TMP_Text warning = Text("Warning", panel, Centre, new Vector2(0f, -260f), "FAILSAFE", s_mono, 40f, TextAlignmentOptions.Center, Color.red, s_strongShadow);
			((RectTransform)warning.transform).sizeDelta = new Vector2(800f, 80f);
			warning.enabled = false;

			// Betaflight-style extras, hidden until a pilot's layout shows them.
			TMP_Text throttle = Extra("Throttle", panel, TopRight, new Vector2(-72f, -155f), "THR 0%", TextAlignmentOptions.TopRight);
			TMP_Text linkQuality = Extra("LinkQuality", panel, TopRight, new Vector2(-72f, -205f), "LQ 100", TextAlignmentOptions.TopRight);
			TMP_Text flightTime = Extra("FlightTime", panel, BottomRight, new Vector2(-72f, 55f), "FLY 00:00", TextAlignmentOptions.BottomRight);
			TMP_Text verticalSpeed = Extra("VerticalSpeed", panel, BottomRight, new Vector2(-72f, 105f), "VS +0.0M/S", TextAlignmentOptions.BottomRight);
			TMP_Text pitch = Extra("PitchAngle", panel, BottomRight, new Vector2(-72f, 155f), "PIT 0", TextAlignmentOptions.BottomRight);
			TMP_Text roll = Extra("RollAngle", panel, BottomRight, new Vector2(-72f, 205f), "ROL 0", TextAlignmentOptions.BottomRight);
			TMP_Text packVoltage = Extra("PackVoltage", panel, BottomLeft, new Vector2(72f, 105f), "16.40V", TextAlignmentOptions.BottomLeft);
			TMP_Text batteryPercent = Extra("BatteryPercent", panel, BottomLeft, new Vector2(72f, 155f), "BAT 100%", TextAlignmentOptions.BottomLeft);
			TMP_Text current = Extra("Current", panel, BottomLeft, new Vector2(72f, 205f), "0.0A", TextAlignmentOptions.BottomLeft);
			TMP_Text usedMah = Extra("UsedMah", panel, BottomLeft, new Vector2(72f, 255f), "0MAH", TextAlignmentOptions.BottomLeft);

			GameObject templates = Node("Templates", panel, Centre.Min, Centre.Max, Centre.Pivot, Vector2.zero, Vector2.zero).gameObject;
			GameObject lineTemplate = WhiteImage("TapeTick", templates.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 25f), true, null).gameObject;
			TMP_Text labelTemplate = Text("TapeLabel", templates.transform, Centre, Vector2.zero, "n", s_sans, 20f, TextAlignmentOptions.Center, Color.white, s_softShadow);
			((RectTransform)labelTemplate.transform).sizeDelta = new Vector2(20f, 50f);
			labelTemplate.richText = false;
			labelTemplate.color = s_sideLabelColor;
			labelTemplate.faceColor = Color.white;
			templates.SetActive(false);

			FpvCompassTapeAnimator compass = BuildHeading(panel, lineTemplate, labelTemplate.gameObject, out TMP_Text headingText);
			RectTransform horizon = BuildHorizon(panel);
			BuildReticle(panel);

			var serialized = new SerializedObject(view);
			serialized.FindProperty("_mainPanel").objectReferenceValue = panel.gameObject;
			serialized.FindProperty("_batteryText").objectReferenceValue = battery;
			serialized.FindProperty("_vtxText").objectReferenceValue = vtx;
			serialized.FindProperty("_altitudeText").objectReferenceValue = altitude;
			serialized.FindProperty("_speedText").objectReferenceValue = speed;
			serialized.FindProperty("_headingText").objectReferenceValue = headingText;
			serialized.FindProperty("_armText").objectReferenceValue = arm;
			serialized.FindProperty("_payloadText").objectReferenceValue = payload;
			serialized.FindProperty("_modeText").objectReferenceValue = mode;
			serialized.FindProperty("_warningText").objectReferenceValue = warning;
			serialized.FindProperty("_compassTapeAnimator").objectReferenceValue = compass;
			serialized.FindProperty("_horizon").objectReferenceValue = horizon;
			serialized.FindProperty("_throttleText").objectReferenceValue = throttle;
			serialized.FindProperty("_linkQualityText").objectReferenceValue = linkQuality;
			serialized.FindProperty("_flightTimeText").objectReferenceValue = flightTime;
			serialized.FindProperty("_verticalSpeedText").objectReferenceValue = verticalSpeed;
			serialized.FindProperty("_pitchText").objectReferenceValue = pitch;
			serialized.FindProperty("_rollText").objectReferenceValue = roll;
			serialized.FindProperty("_packVoltageText").objectReferenceValue = packVoltage;
			serialized.FindProperty("_batteryPercentText").objectReferenceValue = batteryPercent;
			serialized.FindProperty("_currentText").objectReferenceValue = current;
			serialized.FindProperty("_usedMahText").objectReferenceValue = usedMah;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static TMP_Text Extra(string name, RectTransform panel, Anchors anchors, Vector2 position, string content, TextAlignmentOptions alignment)
		{
			TMP_Text text = Text(name, panel, anchors, position, content, s_mono, 40f, alignment, Color.white, s_strongShadow);
			text.gameObject.SetActive(false);
			return text;
		}

		private static FpvCompassTapeAnimator BuildHeading(RectTransform panel, GameObject lineTemplate, GameObject labelTemplate, out TMP_Text headingText)
		{
			RectTransform heading = Node("Heading", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(600f, 180.61f));
			heading.gameObject.AddComponent<CanvasRenderer>();

			RectTransform compass = Node("Compass", heading, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(0f, -20f), new Vector2(600f, 200f));
			compass.localScale = new Vector3(0.7f, 0.7f, 0.7f);
			compass.gameObject.AddComponent<CanvasRenderer>();
			compass.gameObject.AddComponent<RectMask2D>();

			RectTransform tape = Node("TapeParent", compass, Centre.Min, Centre.Max, Centre.Pivot, Vector2.zero, new Vector2(3600f, 200f));
			tape.gameObject.AddComponent<CanvasRenderer>();
			var generator = tape.gameObject.AddComponent<FpvCompassTapeGenerator>();
			var animator = tape.gameObject.AddComponent<FpvCompassTapeAnimator>();

			WhiteImage("Image", compass, new Vector2(0f, 0f), new Vector2(1f, 0f), Centre.Pivot, Vector2.zero, new Vector2(40f, 8f), true, null);

			TMP_Text left = Text("Txt_left", heading, new Anchors(Vector2.zero, Vector2.zero, Vector2.zero), new Vector2(30f, 0f), "E", s_sans, 40f, TextAlignmentOptions.Center, Color.white, s_softShadow);
			TMP_Text right = Text("Txt_right", heading, new Anchors(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f)), new Vector2(-30f, 0f), "S", s_sans, 40f, TextAlignmentOptions.Center, Color.white, s_softShadow);
			foreach (TMP_Text side in new[] { left, right })
			{
				((RectTransform)side.transform).sizeDelta = new Vector2(20f, 50f);
				side.richText = false;
			}

			headingText = Text("headingText", heading, new Anchors(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)), new Vector2(178f, -14.000122f), "139", s_mono, 40f, TextAlignmentOptions.MidlineLeft, Color.white, null);
			((RectTransform)headingText.transform).sizeDelta = new Vector2(300f, 80f);

			TMP_Text label = Text("Label", headingText.transform, new Anchors(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)), new Vector2(-50f, 0f), "hdg", s_sans, 40f, TextAlignmentOptions.MidlineRight, Color.white, null);
			((RectTransform)label.transform).sizeDelta = new Vector2(220f, 0f);

			// The original ArrowShort sprite is MGAssets art: a chevron pointing down at the tape instead.
			RectTransform arrow = Node("Arrow", heading, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Centre.Pivot, new Vector2(0f, -45f), new Vector2(40f, 24f));
			RectTransform leftBar = WhiteImage("Left", arrow, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(-7f, 0f), new Vector2(4f, 22f), false, s_strongShadow);
			leftBar.localRotation = Quaternion.Euler(0f, 0f, 40f);
			RectTransform rightBar = WhiteImage("Right", arrow, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(7f, 0f), new Vector2(4f, 22f), false, s_strongShadow);
			rightBar.localRotation = Quaternion.Euler(0f, 0f, -40f);

			var generatorObject = new SerializedObject(generator);
			generatorObject.FindProperty("_linePrefab").objectReferenceValue = lineTemplate;
			generatorObject.FindProperty("_labelPrefab").objectReferenceValue = labelTemplate;
			generatorObject.FindProperty("_viewport").objectReferenceValue = compass;
			generatorObject.ApplyModifiedPropertiesWithoutUndo();

			var animatorObject = new SerializedObject(animator);
			animatorObject.FindProperty("_leftSideLabel").objectReferenceValue = left;
			animatorObject.FindProperty("_rightSideLabel").objectReferenceValue = right;
			animatorObject.ApplyModifiedPropertiesWithoutUndo();
			return animator;
		}

		private static RectTransform BuildHorizon(RectTransform panel)
		{
			RectTransform horizon = Node("Horizon", panel, Centre.Min, Centre.Max, Centre.Pivot, Vector2.zero, new Vector2(1000f, 1000f));
			WhiteImage("HorizonLeft", horizon, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(-200f, 0f), new Vector2(300f, 4f), false, s_strongShadow);
			WhiteImage("HorizonRight", horizon, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(200f, 0f), new Vector2(300f, 4f), false, s_strongShadow);
			WhiteImage("HorizonLeftTick", horizon, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(-350f, 7f), new Vector2(4f, 18f), false, s_strongShadow);
			WhiteImage("HorizonRightTick", horizon, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(350f, 7f), new Vector2(4f, 18f), false, s_strongShadow);
			return horizon;
		}

		private static void BuildReticle(RectTransform panel)
		{
			RectTransform reticle = Node("Reticle", panel, Centre.Min, Centre.Max, Centre.Pivot, Vector2.zero, new Vector2(180f, 40f));
			WhiteImage("Center", reticle, Centre.Min, Centre.Max, Centre.Pivot, Vector2.zero, new Vector2(8f, 8f), false, s_strongShadow);
			WhiteImage("LeftNear", reticle, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(-30f, 0f), new Vector2(5f, 5f), false, s_strongShadow);
			WhiteImage("LeftFar", reticle, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(-60f, 0f), new Vector2(5f, 5f), false, s_strongShadow);
			WhiteImage("RightNear", reticle, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(30f, 0f), new Vector2(5f, 5f), false, s_strongShadow);
			WhiteImage("RightFar", reticle, Centre.Min, Centre.Max, Centre.Pivot, new Vector2(60f, 0f), new Vector2(5f, 5f), false, s_strongShadow);
		}

		private static Anchors TopLeft => new(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
		private static Anchors TopRight => new(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
		private static Anchors BottomLeft => new(Vector2.zero, Vector2.zero, Vector2.zero);
		private static Anchors BottomRight => new(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
		private static Anchors Centre => new(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

		private static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
		{
			var node = new GameObject(name, typeof(RectTransform));
			var rect = (RectTransform)node.transform;
			rect.SetParent(parent, false);
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = pivot;
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
			return rect;
		}

		private static TMP_Text Text(string name, Transform parent, Anchors anchors, Vector2 position, string content, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color, Color? shadow)
		{
			RectTransform rect = Node(name, parent, anchors.Min, anchors.Max, anchors.Pivot, position, new Vector2(420f, 80f));
			rect.gameObject.AddComponent<CanvasRenderer>();
			var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
			text.font = font;
			text.fontSize = size;
			text.alignment = alignment;
			text.color = color;
			text.text = content;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Overflow;
			text.raycastTarget = false;
			text.isOrthographic = true;
			if (shadow.HasValue)
			{
				AddOutline(rect.gameObject, shadow.Value);
			}

			return text;
		}

		private static RectTransform WhiteImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, bool isRaycastTarget, Color? shadow)
		{
			RectTransform rect = Node(name, parent, anchorMin, anchorMax, pivot, position, size);
			rect.gameObject.AddComponent<CanvasRenderer>();
			var image = rect.gameObject.AddComponent<Image>();
			image.color = Color.white;
			image.raycastTarget = isRaycastTarget;
			if (shadow.HasValue)
			{
				AddOutline(rect.gameObject, shadow.Value);
			}

			return rect;
		}

		private static void AddOutline(GameObject target, Color color)
		{
			var outline = target.AddComponent<Outline>();
			outline.effectColor = color;
			outline.effectDistance = new Vector2(2f, -2f);
			outline.useGraphicAlpha = true;
		}

		private readonly struct Anchors
		{
			public readonly Vector2 Min;
			public readonly Vector2 Max;
			public readonly Vector2 Pivot;

			public Anchors(Vector2 min, Vector2 max, Vector2 pivot)
			{
				Min = min;
				Max = max;
				Pivot = pivot;
			}
		}
	}
}
