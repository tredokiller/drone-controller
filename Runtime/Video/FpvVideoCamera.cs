using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Video
{
	// The FPV drone video of one camera, ported from Interceptor Drones (FpvCameraLook, EffectsRawImage, CameraNoiseEffect):
	// lens water, fisheye, auto exposure, glow and sun veil, analog or digital development, radio interference, static,
	// thermal and night modes, and the OSD on top. FpvVideoRendererFeature draws it into the camera's own target, so it
	// reaches any RenderTexture output (web page, second monitor) as one finished picture.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	public sealed class FpvVideoCamera : MonoBehaviour
	{
		public const int MAX_LENS_DROPS = 8;

		private const string SHADER_NAME = "UForce/FPV Flight/Video";
		private const int METER_WIDTH = 32;
		private const float NOISE_CURVE_POWER = 2f;

		private static readonly int r_lensId = Shader.PropertyToID("_FpvLens");
		private static readonly int r_exposureId = Shader.PropertyToID("_FpvExposure");
		private static readonly int r_analogId = Shader.PropertyToID("_FpvAnalog");
		private static readonly int r_toneId = Shader.PropertyToID("_FpvTone");
		private static readonly int r_shadowLiftId = Shader.PropertyToID("_FpvShadowLift");
		private static readonly int r_lensLightId = Shader.PropertyToID("_LensLight");
		private static readonly int r_lensLightColorId = Shader.PropertyToID("_LensLightColor");
		private static readonly int r_lensDropsId = Shader.PropertyToID("_LensDrops");
		private static readonly int r_lensDropShapesId = Shader.PropertyToID("_LensDropShapes");
		private static readonly int r_lensDropSnowId = Shader.PropertyToID("_LensDropSnow");
		private static readonly int r_lensDropCountId = Shader.PropertyToID("_LensDropCount");
		private static readonly int r_lensWetnessId = Shader.PropertyToID("_LensWetness");
		private static readonly int r_interferenceId = Shader.PropertyToID("_FpvVideoInterference");
		private static readonly int r_noiseIntensityId = Shader.PropertyToID("_NoiseIntensity");
		private static readonly int r_noiseSpeedId = Shader.PropertyToID("_Speed");
		private static readonly int r_stripeIntensityId = Shader.PropertyToID("_StripeIntensity");
		private static readonly int r_stripeFrequencyId = Shader.PropertyToID("_StripeFrequency");
		private static readonly int r_noiseOpacityId = Shader.PropertyToID("_NoiseOpacity");
		private static readonly int r_grayscaleId = Shader.PropertyToID("_UseGrayscale");
		private static readonly int r_thermalModeId = Shader.PropertyToID("_ThermalMode");
		private static readonly int r_seasonId = Shader.PropertyToID("_Season");
		private static readonly int r_hudTextureId = Shader.PropertyToID("_HudTex");
		private static readonly int r_hudOnId = Shader.PropertyToID("_HudOn");
		private static readonly int r_thermalMaskId = Shader.PropertyToID("_ThermalMask");
		private static readonly int r_thermalMaskTexelSizeId = Shader.PropertyToID("_ThermalMaskTexelSize");
		private static readonly int r_thermalMaskOnId = Shader.PropertyToID("_ThermalMaskOn");
		private static readonly int r_videoTexelSizeId = Shader.PropertyToID("_VideoTexelSize");
		private static readonly int r_meterLodId = Shader.PropertyToID("_MeterLod");
		private static readonly Vector4 r_noSun = new(-10f, -10f, 0f, 0f);
		private static readonly Vector4 r_neutralTone = new(0.7f, 0f, 1f, 1f);

		private static int s_hdrUsers;
		private static UniversalRenderPipelineAsset s_gradedAsset;
		private static ColorGradingMode s_gradingBefore;

		[SerializeField] private Shader _shader;
		[Tooltip("Digital FPV or Analog. Empty: the frame as it is, without lens, exposure or glow.")]
		[SerializeField] private FpvVideoProfile _profile;
		[SerializeField] private FpvVideoCameraMode _mode = FpvVideoCameraMode.Day;
		[Tooltip("Layer of this camera's own post-processing volume (the profile's colour adjustments). -1: not used.")]
		[SerializeField] private int _volumeLayer = -1;
		[Tooltip("While a profile is on, URP colour grading runs in HDR, so a sky brighter than 1 reaches the exposure (Interceptor's FpvCameraLook does the same).")]
		[SerializeField] private bool _isHdrGradingForced = true;
		[Tooltip("Thermal background by season: x spring, y summer, z autumn, w winter. All zero: by brightness.")]
		[SerializeField] private Vector4 _season;

		private readonly Vector4[] r_emptyLensDrops = new Vector4[MAX_LENS_DROPS];

		private Camera _camera;
		private UniversalAdditionalCameraData _cameraData;
		private Material _material;
		private Volume _volume;
		private RenderTexture _meterTexture;
		private RTHandle _meterHandle;
		private RenderTexture _thermalMask;
		private RTHandle _thermalMaskHandle;
		private Texture _hudTexture;
		private bool _isMeterPending;
		private float _metered = -1f;
		private float _exposureEv;
		private bool _hasExposure;
		private float _signalLoss;
		private float _staticNoise;
		private bool _isHdrHeld;
		private bool _savedAllowHdr;
		private int _videoHeight = 1080;

		public Camera Camera => _camera;
		public Material Material => _material;
		public FpvVideoProfile Profile => _profile;
		public FpvVideoCameraMode Mode => _mode;
		public float SignalLoss => _signalLoss;
		public float ExposureEv => _exposureEv;
		public float MeteredLuminance => _metered;
		public bool IsLookOn => _profile != null;
		public bool IsThermal => _mode == FpvVideoCameraMode.ThermalWhiteHot || _mode == FpvVideoCameraMode.ThermalColor;
		public float Fisheye => _profile != null ? _profile.Fisheye : 0f;

		internal RTHandle MeterHandle => _meterHandle;
		internal RTHandle ThermalMaskHandle => _thermalMaskHandle;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			s_hdrUsers = 0;
			s_gradedAsset = null;
		}

		private void Awake()
		{
			_camera = GetComponent<Camera>();
			_cameraData = _camera.GetUniversalAdditionalCameraData();

			if (_shader == null)
			{
				_shader = Shader.Find(SHADER_NAME);
			}

			if (_shader == null)
			{
				Debug.LogError($"[{nameof(FpvVideoCamera)}] Shader '{SHADER_NAME}' is missing.", this);
				enabled = false;
				return;
			}

			_material = new Material(_shader)
			{
				name = "FPV Video Material",
				hideFlags = HideFlags.DontSave
			};

			ClearLensWater();
			SetLensLight(r_noSun, Color.white);
			ApplyVolume();
		}

		private void OnEnable()
		{
			if (_material == null)
			{
				return;
			}

			UpdateHdr();
			ApplyCameraData();
		}

		private void OnDisable()
		{
			ReleaseHdr();
			_hasExposure = false;
		}

		private void OnDestroy()
		{
			ReleaseHdr();
			DestroyTexture(ref _meterTexture, ref _meterHandle);
			DestroyTexture(ref _thermalMask, ref _thermalMaskHandle);
			if (_volume != null)
			{
				Destroy(_volume.gameObject);
			}

			if (_material != null)
			{
				Destroy(_material);
			}
		}

		private void LateUpdate()
		{
			if (_material == null)
			{
				return;
			}

			// Only a rendering camera holds HDR grading and its volume: a scene full of idle drone cameras costs nothing.
			bool isRendering = _camera.isActiveAndEnabled;
			UpdateHdr();
			if (_volume != null)
			{
				_volume.enabled = isRendering && _profile != null && _profile.VolumeProfile != null;
			}

			if (!isRendering)
			{
				return;
			}

			EnsureTextures();
			RequestMeter();
			Adapt(Time.unscaledDeltaTime);
			Publish();
		}

		public void SetProfile(FpvVideoProfile profile)
		{
			_profile = profile;
			_hasExposure = false;
			ApplyVolume();
			UpdateHdr();
			ApplyCameraData();
		}

		public void SetMode(FpvVideoCameraMode mode)
		{
			_mode = mode;
			ApplyCameraData();
		}

		// 0 is a clean link, 1 is lost: interference grows, then static takes over.
		public void SetSignalLoss(float signalLoss)
		{
			_signalLoss = Mathf.Clamp01(signalLoss);
		}

		// Full-frame static independent of the link, for example right after a crash or a detonation.
		public void SetStaticNoise(float staticNoise)
		{
			_staticNoise = Mathf.Clamp01(staticNoise);
		}

		public void SetSeason(Vector4 season)
		{
			_season = season;
		}

		// The OSD rendered into its own texture (premultiplied alpha). Null hides it.
		public void SetOsdTexture(Texture texture)
		{
			_hudTexture = texture;
		}

		public void SetLensWater(Vector4[] drops, Vector4[] shapes, Vector4[] snow, int dropCount, float wetness)
		{
			if (_material == null)
			{
				return;
			}

			_material.SetVectorArray(r_lensDropsId, drops);
			_material.SetVectorArray(r_lensDropShapesId, shapes);
			_material.SetVectorArray(r_lensDropSnowId, snow);
			_material.SetFloat(r_lensDropCountId, dropCount);
			_material.SetFloat(r_lensWetnessId, wetness);
		}

		public void ClearLensWater()
		{
			SetLensWater(r_emptyLensDrops, r_emptyLensDrops, r_emptyLensDrops, 0, 0f);
		}

		// light: xy sun on screen (through the lens), z how much the camera faces it, w daylight.
		public void SetLensLight(Vector4 light, Color color)
		{
			if (_material == null)
			{
				return;
			}

			_material.SetVector(r_lensLightId, light);
			_material.SetColor(r_lensLightColorId, color);
		}

		// Camera frame point (viewport) -> where the lens shows it on screen.
		public Vector2 ViewportToScreen(Vector2 viewport) => FpvLensMath.ViewportToScreen(viewport, _camera, Fisheye);

		internal void SetVideoSize(int width, int height)
		{
			_videoHeight = height;
			_material.SetVector(r_videoTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
			_material.SetFloat(r_meterLodId, Mathf.Max(0f, Mathf.Log(width / (float)METER_WIDTH, 2f)));
		}

		private void Publish()
		{
			float aspect = Mathf.Max(0.1f, _camera.aspect);

			if (_profile != null)
			{
				float softLod = _profile.IsAnalog ? _profile.Softness * Mathf.Max(0f, Mathf.Log(_videoHeight / 480f, 2f)) : 0f;
				_material.SetVector(r_lensId, new Vector4(_profile.Fisheye, FpvLensMath.TanCorner(_camera), aspect, 1f));
				_material.SetVector(r_exposureId, new Vector4(Mathf.Pow(2f, _exposureEv), _profile.Glow, _profile.SunVeil, _profile.Vignette));
				_material.SetFloat(r_shadowLiftId, _profile.ShadowLift);
				_material.SetVector(r_analogId, _profile.IsAnalog ? new Vector4(1f, softLod, _profile.AnalogNoise, _profile.ChromaBleed) : Vector4.zero);
				_material.SetVector(r_toneId, _profile.Tone);
			}
			else
			{
				_material.SetVector(r_lensId, Vector4.zero);
				_material.SetVector(r_exposureId, new Vector4(1f, 0f, 0f, 0f));
				_material.SetFloat(r_shadowLiftId, 0f);
				_material.SetVector(r_analogId, Vector4.zero);
				_material.SetVector(r_toneId, r_neutralTone);
			}

			// Static follows Interceptor's camera noise curve: it grows slowly and becomes full static at total loss.
			float noise = Mathf.Max(Mathf.Pow(_signalLoss, NOISE_CURVE_POWER), _staticNoise);
			float noiseFps = _profile != null ? _profile.NoiseFps : 60f;
			_material.SetVector(r_interferenceId, new Vector4(_signalLoss, noiseFps, 0f, 0f));
			_material.SetFloat(r_noiseIntensityId, Mathf.Lerp(0f, 3f, noise));
			_material.SetFloat(r_noiseSpeedId, Mathf.Lerp(0f, 10f, noise));
			_material.SetFloat(r_stripeIntensityId, Mathf.Lerp(0f, 0.1f, noise));
			_material.SetFloat(r_stripeFrequencyId, Mathf.Lerp(1f, 30f, noise));
			_material.SetFloat(r_noiseOpacityId, noise);

			_material.SetFloat(r_grayscaleId, _mode == FpvVideoCameraMode.Night ? 1f : 0f);
			_material.SetFloat(r_thermalModeId, _mode == FpvVideoCameraMode.ThermalWhiteHot ? 1f : _mode == FpvVideoCameraMode.ThermalColor ? 2f : 0f);
			_material.SetVector(r_seasonId, _season);

			bool hasThermalMask = IsThermal && _thermalMask != null && FpvThermalSignature.Active.Count > 0;
			_material.SetFloat(r_thermalMaskOnId, hasThermalMask ? 1f : 0f);
			if (_thermalMask != null)
			{
				_material.SetTexture(r_thermalMaskId, _thermalMask);
				_material.SetVector(r_thermalMaskTexelSizeId, new Vector4(1f / _thermalMask.width, 1f / _thermalMask.height, _thermalMask.width, _thermalMask.height));
			}

			_material.SetFloat(r_hudOnId, _hudTexture != null ? 1f : 0f);
			if (_hudTexture != null)
			{
				_material.SetTexture(r_hudTextureId, _hudTexture);
			}
		}

		private void EnsureTextures()
		{
			float aspect = Mathf.Max(0.1f, _camera.aspect);
			int meterHeight = Mathf.Max(1, Mathf.RoundToInt(METER_WIDTH / aspect));
			if (_profile != null && (_meterTexture == null || _meterTexture.height != meterHeight))
			{
				DestroyTexture(ref _meterTexture, ref _meterHandle);
				_meterTexture = new RenderTexture(METER_WIDTH, meterHeight, 0, RenderTextureFormat.ARGBHalf)
				{
					name = "FPV Video Meter",
					hideFlags = HideFlags.DontSave
				};
				_meterTexture.Create();
				_meterHandle = RTHandles.Alloc(_meterTexture);
			}
			else if (_profile == null && _meterTexture != null)
			{
				DestroyTexture(ref _meterTexture, ref _meterHandle);
			}

			int width = Mathf.Max(1, _camera.pixelWidth);
			int height = Mathf.Max(1, _camera.pixelHeight);
			if (IsThermal && (_thermalMask == null || _thermalMask.width != width || _thermalMask.height != height))
			{
				DestroyTexture(ref _thermalMask, ref _thermalMaskHandle);
				_thermalMask = new RenderTexture(width, height, 0, RenderTextureFormat.R8)
				{
					name = "FPV Thermal Mask",
					hideFlags = HideFlags.DontSave
				};
				_thermalMask.Create();
				_thermalMaskHandle = RTHandles.Alloc(_thermalMask);
			}
			else if (!IsThermal && _thermalMask != null)
			{
				DestroyTexture(ref _thermalMask, ref _thermalMaskHandle);
			}
		}

		private void RequestMeter()
		{
			if (_profile == null || _isMeterPending || _meterTexture == null || !SystemInfo.supportsAsyncGPUReadback)
			{
				return;
			}

			_isMeterPending = true;
			int width = _meterTexture.width;
			int height = _meterTexture.height;
			AsyncGPUReadback.Request(_meterTexture, 0, TextureFormat.RGBAFloat, request => HandleMetered(request, width, height));
		}

		// Average brightness: logarithmic (the sky does not dominate), weighted to the centre and the bottom, like FPV cameras.
		private void HandleMetered(AsyncGPUReadbackRequest request, int width, int height)
		{
			_isMeterPending = false;
			if (this == null || request.hasError || _profile == null)
			{
				return;
			}

			NativeArray<Color> pixels = request.GetData<Color>();
			float sum = 0f;
			float weights = 0f;
			float ceiling = _profile.Key * _profile.MeterCeiling;

			for (int y = 0; y < height; y++)
			{
				float v = (y + 0.5f) / height;
				for (int x = 0; x < width; x++)
				{
					Color color = pixels[y * width + x];
					float luminance = Mathf.Clamp(color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f, 1e-4f, ceiling);
					float u = (x + 0.5f) / width - 0.5f;
					float dv = v - 0.45f;
					float weight = Mathf.Exp(-(u * u + dv * dv) / 0.12f) + 0.2f;
					weight *= v < 0.5f ? 1.3f : 1f;
					sum += Mathf.Log(luminance) * weight;
					weights += weight;
				}
			}

			_metered = weights > 0f ? Mathf.Exp(sum / weights) : -1f;
		}

		private void Adapt(float deltaTime)
		{
			if (_profile == null || _metered <= 0f)
			{
				return;
			}

			Vector2 range = _profile.ExposureRange;
			float target = Mathf.Clamp(Mathf.Log(_profile.Key / _metered, 2f), range.x, range.y);
			if (!_hasExposure)
			{
				_exposureEv = target;
				_hasExposure = true;
				return;
			}

			float seconds = target < _exposureEv ? _profile.AdaptSeconds.x : _profile.AdaptSeconds.y;
			_exposureEv += (target - _exposureEv) * (1f - Mathf.Exp(-deltaTime / Mathf.Max(0.01f, seconds)));
		}

		private void ApplyVolume()
		{
			VolumeProfile volumeProfile = _profile != null ? _profile.VolumeProfile : null;
			if (_volumeLayer < 0 || _volumeLayer > 31)
			{
				return;
			}

			if (_volume == null)
			{
				var volumeObject = new GameObject("FPV Video Volume")
				{
					layer = _volumeLayer,
					hideFlags = HideFlags.DontSave
				};
				volumeObject.transform.SetParent(transform, false);
				_volume = volumeObject.AddComponent<Volume>();
				_volume.isGlobal = true;
				_volume.priority = 100f;
			}

			_volume.sharedProfile = volumeProfile;
			_volume.enabled = volumeProfile != null && _camera != null && _camera.isActiveAndEnabled;
		}

		private void ApplyCameraData()
		{
			if (_cameraData == null)
			{
				return;
			}

			if (_volumeLayer >= 0 && _volumeLayer <= 31)
			{
				_cameraData.volumeLayerMask |= 1 << _volumeLayer;
				// The thermal camera sees heat, not colour, so the colour grading of the preset is off (as in Interceptor).
				_cameraData.renderPostProcessing = _profile != null && _profile.VolumeProfile != null && !IsThermal;
			}

			_cameraData.requiresDepthOption = IsThermal ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
		}

		private void UpdateHdr()
		{
			if (isActiveAndEnabled && _camera != null && _camera.isActiveAndEnabled && _isHdrGradingForced && _profile != null)
			{
				AcquireHdr();
			}
			else
			{
				ReleaseHdr();
			}
		}

		private void AcquireHdr()
		{
			if (_isHdrHeld)
			{
				return;
			}

			_isHdrHeld = true;
			_savedAllowHdr = _camera.allowHDR;
			_camera.allowHDR = true;

			if (s_hdrUsers++ == 0)
			{
				s_gradedAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
				if (s_gradedAsset != null)
				{
					s_gradingBefore = s_gradedAsset.colorGradingMode;
					s_gradedAsset.colorGradingMode = ColorGradingMode.HighDynamicRange;
				}
			}
		}

		private void ReleaseHdr()
		{
			if (!_isHdrHeld)
			{
				return;
			}

			_isHdrHeld = false;
			if (_camera != null)
			{
				_camera.allowHDR = _savedAllowHdr;
			}

			if (--s_hdrUsers == 0 && s_gradedAsset != null)
			{
				s_gradedAsset.colorGradingMode = s_gradingBefore;
				s_gradedAsset = null;
			}
		}

		private static void DestroyTexture(ref RenderTexture texture, ref RTHandle handle)
		{
			handle?.Release();
			handle = null;
			if (texture != null)
			{
				texture.Release();
				Destroy(texture);
				texture = null;
			}
		}
	}
}
