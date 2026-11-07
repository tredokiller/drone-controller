namespace UForce.FpvFlight.Audio
{
	// Xorshift white noise in -1..1, ported from PortableAudio (TIR) DeterministicNoiseGenerator.
	internal struct FpvNoiseGenerator
	{
		private uint _state;

		public FpvNoiseGenerator(uint seed)
		{
			_state = seed == 0u ? FpvMicrophoneDsp.NOISE_SEED : seed;
		}

		public double NextSample()
		{
			_state ^= _state << 13;
			_state ^= _state >> 17;
			_state ^= _state << 5;
			return (_state >> 8) * (2d / 16777215d) - 1d;
		}
	}
}
