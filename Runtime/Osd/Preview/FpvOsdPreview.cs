using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace UForce.FpvFlight.Osd
{
	// The OSD without a drone, for menus: its own camera renders a picture and the real OSD prefab with fixed telemetry
	// into an opaque texture of the requested size. Size it like the drone video (for example the screen), so the canvas
	// scaler lays the OSD out as in flight. The rig sits far below the world on one layer, so no other camera sees it.
	[DisallowMultipleComponent]
	public sealed class FpvOsdPreview : MonoBehaviour
	{
		private const string ROOT_NAME = "[FPV OSD Preview]";
		private static readonly Vector3 s_rigPosition = new(0f, -20000f, 0f);

		private Camera _camera;
		private RenderTexture _texture;
		private FpvOsdView _view;

		public RenderTexture Texture => _texture;
		public FpvOsdView View => _view;

		// Null when the package defaults are missing and no prefab is given.
		public static FpvOsdPreview Create(int width, int height, Canvas osdPrefab = null, Texture background = null, int layer = 31)
		{
			FpvOsdDefaults defaults = FpvOsdDefaults.Load();
			if (osdPrefab == null && defaults != null)
			{
				osdPrefab = defaults.OsdPrefab;
			}

			if (background == null && defaults != null)
			{
				background = defaults.PreviewBackground;
			}

			if (osdPrefab == null)
			{
				Debug.LogError($"[{nameof(FpvOsdPreview)}] No OSD prefab: give one or keep {FpvOsdDefaults.RESOURCE_PATH} in Resources.");
				return null;
			}

			var root = new GameObject(ROOT_NAME) { hideFlags = HideFlags.DontSave };
			root.transform.position = s_rigPosition;
			var preview = root.AddComponent<FpvOsdPreview>();
			preview.Build(width, height, osdPrefab, background, layer);
			return preview;
		}

		public void Resize(int width, int height)
		{
			width = Mathf.Max(16, width);
			height = Mathf.Max(16, height);
			if (_texture != null && _texture.width == width && _texture.height == height)
			{
				return;
			}

			ReleaseTexture();
			// URP's render graph wants a depth buffer on every camera output.
			_texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
			{
				name = "FPV OSD Preview",
				hideFlags = HideFlags.DontSave,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};

			_texture.Create();
			_camera.targetTexture = _texture;
			_camera.aspect = (float)width / height;
		}

		private void OnDestroy()
		{
			ReleaseTexture();
		}

		private void Build(int width, int height, Canvas osdPrefab, Texture background, int layer)
		{
			gameObject.layer = layer;
			_camera = gameObject.AddComponent<Camera>();
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = Color.black;
			_camera.cullingMask = 1 << layer;
			_camera.nearClipPlane = 0.01f;
			_camera.farClipPlane = 10f;
			_camera.allowHDR = false;
			_camera.allowMSAA = false;
			_camera.useOcclusionCulling = false;

			UniversalAdditionalCameraData cameraData = _camera.GetUniversalAdditionalCameraData();
			cameraData.renderType = CameraRenderType.Base;
			cameraData.renderPostProcessing = false;
			cameraData.renderShadows = false;
			cameraData.antialiasing = AntialiasingMode.None;
			cameraData.requiresColorOption = CameraOverrideOption.Off;
			cameraData.requiresDepthOption = CameraOverrideOption.Off;

			Resize(width, height);
			CreateBackground(background, layer);

			Canvas osd = Instantiate(osdPrefab, transform, false);
			osd.renderMode = RenderMode.ScreenSpaceCamera;
			osd.worldCamera = _camera;
			osd.planeDistance = 0.5f;
			osd.sortingOrder = 1;
			SetLayer(osd.transform, layer);

			_view = osd.GetComponent<FpvOsdView>();
			if (_view != null)
			{
				_view.SetDataSource(new FpvOsdPreviewDataSource());
			}
		}

		private void CreateBackground(Texture background, int layer)
		{
			var canvasObject = new GameObject("Background", typeof(RectTransform)) { layer = layer };
			canvasObject.transform.SetParent(transform, false);
			var canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceCamera;
			canvas.worldCamera = _camera;
			canvas.planeDistance = 1f;
			canvas.sortingOrder = -1;

			var imageObject = new GameObject("Picture", typeof(RectTransform)) { layer = layer };
			var rect = (RectTransform)imageObject.transform;
			rect.SetParent(canvasObject.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			var image = imageObject.AddComponent<RawImage>();
			image.texture = background;
			image.color = background != null ? Color.white : new Color(0.16f, 0.2f, 0.18f, 1f);
			image.raycastTarget = false;
			if (background != null)
			{
				var fitter = imageObject.AddComponent<AspectRatioFitter>();
				fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
				fitter.aspectRatio = (float)background.width / background.height;
			}
		}

		private void ReleaseTexture()
		{
			if (_texture == null)
			{
				return;
			}

			if (_camera != null)
			{
				_camera.targetTexture = null;
			}

			_texture.Release();
			Destroy(_texture);
			_texture = null;
		}

		private static void SetLayer(Transform root, int layer)
		{
			root.gameObject.layer = layer;
			for (int index = 0; index < root.childCount; index++)
			{
				SetLayer(root.GetChild(index), layer);
			}
		}
	}
}
