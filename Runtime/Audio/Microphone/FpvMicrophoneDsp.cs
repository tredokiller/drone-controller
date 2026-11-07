namespace UForce.FpvFlight.Audio
{
	// Constants of the onboard microphone DSP, ported from PortableAudio (TIR) MicrophoneDspConstants.
	internal static class FpvMicrophoneDsp
	{
		public const int MINIMUM_SAMPLE_RATE = 8000;
		public const int MAXIMUM_SAMPLE_RATE = 192000;
		public const int LFE_CHANNEL_INDEX = 3;
		public const int SURROUND_CHANNEL_COUNT = 6;
		public const int GAIN_UPDATE_INTERVAL_FRAMES = 16;

		public const double DC_BLOCK_FREQUENCY_HZ = 30d;
		public const double FILTER_NYQUIST_SAFETY_FACTOR = 0.4d;
		public const double MINIMUM_FILTER_SEPARATION_HZ = 100d;
		public const double COMPRESSOR_ATTACK_SECONDS = 0.003d;
		public const double COMPRESSOR_RELEASE_SECONDS = 0.18d;
		public const double GAIN_SMOOTHING_SECONDS = 0.002d;
		public const double FLIGHT_CONDITION_SMOOTHING_SECONDS = 0.08d;
		public const double WIND_NOISE_CUTOFF_HZ = 180d;
		public const double GUST_NOISE_CUTOFF_HZ = 2.5d;
		public const double MODE_TRANSITION_SECONDS = 0.025d;
		public const double COMPRESSOR_KNEE_HALF_WIDTH_DB = 3d;
		public const double COMPRESSOR_KNEE_WIDTH_DB = COMPRESSOR_KNEE_HALF_WIDTH_DB * 2d;
		public const double NOISE_ACTIVITY_GAIN = 12d;
		public const double GUST_DEPTH = 18d;
		public const double MINIMUM_GUST_GAIN = 0.25d;
		public const double MAXIMUM_GUST_GAIN = 1.75d;
		public const double MOTOR_PREAMP_BOOST = 0.4d;
		public const double OUTPUT_CEILING = 0.89d;
		public const double INPUT_SAFETY_CEILING = 16d;
		public const double SATURATION_INPUT_CEILING = 3d;
		public const double DENORMAL_THRESHOLD = 1e-20d;
		public const double MINIMUM_ENVELOPE = 1e-9d;
		public const uint NOISE_SEED = 0x9E3779B9u;
	}
}
