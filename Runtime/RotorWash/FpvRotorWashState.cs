using UnityEngine;

namespace UForce.FpvFlight.RotorWash
{
	// The rotor wash of one drone this frame, for receivers such as GeoWorld's leaves and water.
	public readonly struct FpvRotorWashState
	{
		// Height used when there is no ground under the drone within reach.
		public const float NO_GROUND_HEIGHT = 1000f;

		public FpvRotorWashState(Vector3 position, Vector3 ground, bool hasGround, float height, float spin, float strength, Vector3 wind, Vector3 velocity, Terrain terrain, bool isOverWater)
		{
			Position = position;
			Ground = ground;
			HasGround = hasGround;
			Height = height;
			Spin = spin;
			Strength = strength;
			Wind = wind;
			Velocity = velocity;
			Terrain = terrain;
			IsOverWater = isOverWater;
		}

		public Vector3 Position { get; }
		// Point under the drone (on the water surface over water).
		public Vector3 Ground { get; }
		public bool HasGround { get; }
		public float Height { get; }
		// Propeller wash as a share of full, without the fade with height (0 when disarmed).
		public float Spin { get; }
		// Wash at the ground: spin faded by height.
		public float Strength { get; }
		public Vector3 Wind { get; }
		public Vector3 Velocity { get; }
		public Terrain Terrain { get; }
		public bool IsOverWater { get; }
	}
}
