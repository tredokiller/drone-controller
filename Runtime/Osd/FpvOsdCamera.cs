using UForce.FpvFlight.Video;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Osd
{
	// Renders the OSD canvas with its own camera into a transparent texture that FpvVideoCamera composites over the
	// finished video, so the OSD stays crisp under lens water and fisheye but fades with the signal, as in Interceptor
	// Drones (HudOriginCameraBinder). The OSD therefore reaches every output of the video texture (web page, second monitor).
	[DisallowMultipleComponent]
	[RequireComponent(typeof(FpvVideoCamera))]
	public sealed class FpvOsdCamera : MonoBehaviour
	{
		private const string CAMERA_NAME = "[FPV OSD Camera]";

		[Tooltip("OSD canvas prefab, instantiated for this camera. Screen Space - Camera.")]
		[SerializeField] private Canvas _osdPrefab;
		[Tooltip("Layer only the OSD camera renders. The video camera stops rendering it.")]
		[SerializeField, Range(0, 31)] private int _osdLayer = 31;
		[Tooltip("OSD texture size relative to the video.")]
		[SerializeField, Range(0.25f, 1f)] private float _resolutionScale = 1f;

		private FpvVideoCamera _video;
		private Camera _osdCamera;
		private Canvas _canvas;
		private RenderTexture _texture;

		public Canvas Canvas => _canvas;

		private void Awake()
		{
			_video = GetComponent<FpvVideoCamera>();
			CreateCamera();
			CreateCanvas();
		}

		private void OnDisable()
		{
			if (_osdCamera != null)
			{
				_osdCamera.enabled = false;
			}

			if (_video != null)
			{
				_video.SetOsdTexture(null);
			}
		}

		private void OnDestroy()
		{
			ReleaseTexture();
			if (_osdCamera != null)
			{
				Destroy(_osdCamera.gameObject);
			}
		}

		private void LateUpdate()
		{
			Camera videoCamera = _video.Camera;
			bool isRendering = videoCamera != null && videoCamera.isActiveAndEnabled && _canvas != null;
			_osdCamera.enabled = isRendering;
			if (_canvas != null)
			{
				_canvas.enabled = isRendering;
			}

			if (!isRendering)
			{
				_video.SetOsdTexture(null);
				return;
			}

			videoCamera.cullingMask &= ~(1 << _osdLayer);
			_osdCamera.depth = videoCamera.depth - 1f;
			EnsureTexture(videoCamera);
			_osdCamera.aspect = videoCamera.aspect;
			_video.SetOsdTexture(_texture);
		}

		private void CreateCamera()
		{
			var cameraObject = new GameObject(CAMERA_NAME)
			{
				layer = _osdLayer,
				hideFlags = HideFlags.DontSave
			};
			cameraObject.transform.SetParent(transform, false);

			_osdCamera = cameraObject.AddComponent<Camera>();
			_osdCamera.clearFlags = CameraClearFlags.SolidColor;
			_osdCamera.backgroundColor = Color.clear;
			_osdCamera.cullingMask = 1 << _osdLayer;
			_osdCamera.nearClipPlane = 0.01f;
			_osdCamera.farClipPlane = 10f;
			_osdCamera.allowHDR = false;
			_osdCamera.allowMSAA = false;
			_osdCamera.useOcclusionCulling = false;
			_osdCamera.enabled = false;

			UniversalAdditionalCameraData cameraData = _osdCamera.GetUniversalAdditionalCameraData();
			cameraData.renderType = CameraRenderType.Base;
			cameraData.renderPostProcessing = false;
			cameraData.renderShadows = false;
			cameraData.antialiasing = AntialiasingMode.None;
			cameraData.requiresColorOption = CameraOverrideOption.Off;
			cameraData.requiresDepthOption = CameraOverrideOption.Off;
		}

		private void CreateCanvas()
		{
			if (_osdPrefab == null)
			{
				Debug.LogWarning($"[{nameof(FpvOsdCamera)}] No OSD prefab assigned; the video has no OSD.", this);
				return;
			}

			_canvas = Instantiate(_osdPrefab, transform, false);
			_canvas.renderMode = RenderMode.ScreenSpaceCamera;
			_canvas.worldCamera = _osdCamera;
			SetLayer(_canvas.transform, _osdLayer);
		}

		private void EnsureTexture(Camera videoCamera)
		{
			int width = Mathf.Max(16, Mathf.RoundToInt(videoCamera.pixelWidth * _resolutionScale));
			int height = Mathf.Max(16, Mathf.RoundToInt(videoCamera.pixelHeight * _resolutionScale));
			if (_texture != null && _texture.width == width && _texture.height == height)
			{
				return;
			}

			ReleaseTexture();
			// URP's render graph wants a depth buffer on every camera output, even one that only draws UI.
			_texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
			{
				name = "FPV OSD",
				hideFlags = HideFlags.DontSave
			};
			_texture.Create();
			_osdCamera.targetTexture = _texture;
		}

		private void ReleaseTexture()
		{
			if (_texture == null)
			{
				return;
			}

			if (_osdCamera != null)
			{
				_osdCamera.targetTexture = null;
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
