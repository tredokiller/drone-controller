namespace UForce.FpvFlight
{
	// Xorshift32. Deterministic per seed, allocation-free, independent of UnityEngine.Random.
	public struct DeterministicRandom
	{
		private uint _state;

		public DeterministicRandom(uint seed)
		{
			_state = seed == 0u ? 0x9E3779B9u : seed;
		}

		public uint NextUInt()
		{
			uint state = _state;
			state ^= state << 13;
			state ^= state >> 17;
			state ^= state << 5;
			_state = state;
			return state;
		}

		// Uniform in [0, 1).
		public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

		// Uniform in [-1, 1).
		public float NextSigned() => NextFloat() * 2f - 1f;
	}
}
