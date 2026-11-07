using UnityEngine;
using UnityEngine.Rendering;

namespace UForce.FpvFlight.Video
{
	// How the FPV camera develops its picture, ported from Interceptor Drones (FpvCameraLook presets "Digital FPV" and
	// "Analog"). A camera without a profile shows the frame as it is: no lens, exposure, glow or sun veil.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Video Profile", fileName = "FpvVideoProfile")]
	public sealed class FpvVideoProfile : ScriptableObject
	{
		[Header("Lens")]
		[Tooltip("Fisheye strength: 0 is rectilinear, 1 is an FPV lens.")]
		[SerializeField, Range(0f, 1f)] private float _fisheye = 1f;
		[SerializeField, Range(0f, 1f)] private float _vignette = 0.28f;

		[Header("Exposure")]
		[Tooltip("Average (log) frame brightness the exposure aims for, linear. A frame over forest and fields meters ~0.05-0.06.")]
		[SerializeField, Min(0.01f)] private float _key = 0.12f;
		[Tooltip("Brightness above this many keys is not metered: a bright sky and sun do not push the ground into black.")]
		[SerializeField, Min(1f)] private float _meterCeiling = 4f;
		[Tooltip("Exposure limits in stops.")]
		[SerializeField] private Vector2 _exposureRange = new(-2.5f, 1f);
		[Tooltip("Seconds the exposure takes to follow a frame that got brighter (x) or darker (y).")]
		[SerializeField] private Vector2 _adaptSeconds = new(0.35f, 0.9f);

		[Header("Light bleed")]
		[SerializeField, Range(0f, 2f)] private float _glow = 0.15f;
		[SerializeField, Range(0f, 2f)] private float _sunVeil = 0.3f;
		[Tooltip("Shadow lift: the ground against the sun does not sink into black.")]
		[SerializeField, Range(0f, 0.4f)] private float _shadowLift = 0.14f;

		[Header("Tone")]
		[Tooltip("x highlight knee, y black lift, z saturation, w contrast.")]
		[SerializeField] private Vector4 _tone = new(0.62f, 0f, 1.04f, 1f);

		[Header("Analog")]
		[SerializeField] private bool _isAnalog;
		[SerializeField, Range(0f, 2f)] private float _analogNoise = 1f;
		[Tooltip("Colour bleeds along the line by this part of the frame width.")]
		[SerializeField, Range(0f, 0.03f)] private float _chromaBleed = 0.007f;
		[Tooltip("Extra mip per halving of the height above 480 lines, so a large frame is as soft as analog video.")]
		[SerializeField, Range(0f, 1.5f)] private float _softness = 0.6f;

		[Header("Post-processing")]
		[Tooltip("URP volume applied to this camera only (colour adjustments of the preset). Optional.")]
		[SerializeField] private VolumeProfile _volumeProfile;

		[Header("Radio interference")]
		[SerializeField, Range(12f, 120f)] private float _noiseFps = 60f;

		public float Fisheye => _fisheye;
		public float Vignette => _vignette;
		public float Key => _key;
		public float MeterCeiling => _meterCeiling;
		public Vector2 ExposureRange => _exposureRange;
		public Vector2 AdaptSeconds => _adaptSeconds;
		public float Glow => _glow;
		public float SunVeil => _sunVeil;
		public float ShadowLift => _shadowLift;
		public Vector4 Tone => _tone;
		public bool IsAnalog => _isAnalog;
		public float AnalogNoise => _analogNoise;
		public float ChromaBleed => _chromaBleed;
		public float Softness => _softness;
		public VolumeProfile VolumeProfile => _volumeProfile;
		public float NoiseFps => _noiseFps;
	}
}
