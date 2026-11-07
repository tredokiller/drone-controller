using UnityEngine;

namespace UForce.FpvFlight.RotorWash
{
	// World conditions under the drone: water, puddles, snow, rain and light. Without one the ground is dry land under
	// plain daylight. The GeoWorld bridge implements it with WaterSurfaces, GroundWetness, Seasons and Enviro.
	public interface IFpvRotorWashEnvironment
	{
		// Water surface under the drone (river, lake). isFrozen: ice, which dusts like snowy ground.
		public bool TryGetWaterHeight(Vector3 position, out float height, out bool isFrozen);
		// Standing water on the ground at a point (puddle, marsh), 0..1.
		public float GroundWaterAt(Vector3 point);
		// Snow lying on the ground, 0..1.
		public float SnowCover { get; }
		public float RainIntensity { get; }
		public float SnowfallIntensity { get; }
		// Dust is not lit like a surface; this tints it by sky and sun.
		public Color DustLight { get; }
		// Air speed at the water surface from the jet, m/s, and the jet radius at a height (GeoWorld's water model).
		public float JetAir(float height, float spin);
		public float JetRadiusAt(float height);
	}
}
