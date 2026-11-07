using System;
using UnityEngine;

namespace UForce.FpvFlight.Audio
{
	// Onboard microphone settings, ported from PortableAudio (TIR) MicrophoneProcessorSettings. Defaults are the
	// Interceptor P1-SUN prefab's (wind level 0.2). Values are sanitized when a processor is built, so a malformed
	// configuration cannot destabilize the audio thread.
	[Serializable]
	public sealed class FpvMicrophoneSettings
	{
		public const float MINIMUM_HIGH_PASS_HZ = 20f;
		public const float MAXIMUM_HIGH_PASS_HZ = 1000f;
		public const float MINIMUM_LOW_PASS_HZ = 1500f;
		public const float MAXIMUM_LOW_PASS_HZ = 12000f;
		public const float MAXIMUM_PREAMP_GAIN_DB = 24f;
		public const float MINIMUM_COMPRESSOR_THRESHOLD_DB = -40f;
		public const float MAXIMUM_COMPRESSOR_THRESHOLD_DB = -3f;
		public const float MINIMUM_COMPRESSOR_RATIO = 1f;
		public const float MAXIMUM_COMPRESSOR_RATIO = 12f;
		public const float MAXIMUM_WIND_LEVEL = 0.5f;
		public const float MAXIMUM_PROP_WASH_LEVEL = 0.2f;
		public const float MINIMUM_NOISE_FLOOR_DB = -100f;
		public const float MAXIMUM_NOISE_FLOOR_DB = -40f;
		public const float MINIMUM_OUTPUT_GAIN_DB = -24f;
		public const float MAXIMUM_OUTPUT_GAIN_DB = 6f;

		public const float DEFAULT_HIGH_PASS_HZ = 100f;
		public const float DEFAULT_LOW_PASS_HZ = 5200f;
		public const float DEFAULT_PREAMP_GAIN_DB = 9f;
		public const float DEFAULT_COMPRESSOR_THRESHOLD_DB = -18f;
		public const float DEFAULT_COMPRESSOR_RATIO = 4f;
		public const float DEFAULT_WIND_LEVEL = 0.2f;
		public const float DEFAULT_PROP_WASH_LEVEL = 0.025f;
		public const float DEFAULT_NOISE_FLOOR_DB = -64f;
		public const float DEFAULT_OUTPUT_GAIN_DB = -3f;

		[Header("Capsule / preamp")]
		[SerializeField, Range(MINIMUM_HIGH_PASS_HZ, MAXIMUM_HIGH_PASS_HZ)] private float _highPassHz = DEFAULT_HIGH_PASS_HZ;
		[SerializeField, Range(MINIMUM_LOW_PASS_HZ, MAXIMUM_LOW_PASS_HZ)] private float _lowPassHz = DEFAULT_LOW_PASS_HZ;
		[SerializeField, Range(0f, MAXIMUM_PREAMP_GAIN_DB)] private float _preampGainDb = DEFAULT_PREAMP_GAIN_DB;
		[SerializeField, Range(MINIMUM_COMPRESSOR_THRESHOLD_DB, MAXIMUM_COMPRESSOR_THRESHOLD_DB)] private float _compressorThresholdDb = DEFAULT_COMPRESSOR_THRESHOLD_DB;
		[SerializeField, Range(MINIMUM_COMPRESSOR_RATIO, MAXIMUM_COMPRESSOR_RATIO)] private float _compressorRatio = DEFAULT_COMPRESSOR_RATIO;

		[Header("Airflow / electronics")]
		[SerializeField, Range(0f, MAXIMUM_WIND_LEVEL)] private float _windLevel = DEFAULT_WIND_LEVEL;
		[SerializeField, Range(0f, MAXIMUM_PROP_WASH_LEVEL)] private float _propWashLevel = DEFAULT_PROP_WASH_LEVEL;
		[SerializeField, Range(MINIMUM_NOISE_FLOOR_DB, MAXIMUM_NOISE_FLOOR_DB)] private float _noiseFloorDb = DEFAULT_NOISE_FLOOR_DB;
		[SerializeField, Range(MINIMUM_OUTPUT_GAIN_DB, MAXIMUM_OUTPUT_GAIN_DB)] private float _outputGainDb = DEFAULT_OUTPUT_GAIN_DB;

		public float HighPassHz => _highPassHz;
		public float LowPassHz => _lowPassHz;
		public float PreampGainDb => _preampGainDb;
		public float CompressorThresholdDb => _compressorThresholdDb;
		public float CompressorRatio => _compressorRatio;
		public float WindLevel => _windLevel;
		public float PropWashLevel => _propWashLevel;
		public float NoiseFloorDb => _noiseFloorDb;
		public float OutputGainDb => _outputGainDb;

		public void SetAirflow(float windLevel, float propWashLevel)
		{
			_windLevel = windLevel;
			_propWashLevel = propWashLevel;
		}

		public FpvMicrophoneSettings Clone() => (FpvMicrophoneSettings)MemberwiseClone();
	}
}
