using _Game.Scripts.Drone;
using _Game.Scripts.World;
using UnityEngine;

namespace UForce.FpvFlight.RotorWash.GeoWorld
{
	// Connects FpvRotorWash to a GeoWorld (1.20+) world, as Interceptor Drones' DroneRotorWash does: water surfaces and ice,
	// puddles and marsh (GroundWetness), snow cover and winter (Seasons), rain and snowfall and dust light (Enviro through
	// EnvironmentQueries), fallen leaves blown away (RotorWashLeaves) and water ripples (RotorWashWater).
	// Put it on the drone next to FpvRotorWash.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(FpvRotorWash))]
	public sealed class FpvGeoWorldRotorWash : MonoBehaviour, IFpvRotorWashEnvironment, IFpvRotorWashReceiver
	{
		private const string RAIN_EFFECT_NAME = "Rain";
		private const string SNOW_EFFECT_NAME = "Snow";
		private const int NEARBY_WATER_SAMPLES = 32;

		public float SnowCover => Seasons.Instance != null ? Seasons.Instance.SnowCover : 0f;
		public float RainIntensity => EnvironmentQueries.EffectIntensity(RAIN_EFFECT_NAME);
		public float SnowfallIntensity => EnvironmentQueries.EffectIntensity(SNOW_EFFECT_NAME);
		public Color DustLight => EnvironmentQueries.DustLight();

		private void Awake()
		{
			RotorWashLeaves.Ensure(gameObject.scene);
			RotorWashWater.Ensure(gameObject.scene);
		}

		public bool TryGetWaterHeight(Vector3 position, out float height, out bool isFrozen)
		{
			if (WaterSurfaces.TryGetHeight(position, out height, out WaterKind kind))
			{
				// In winter still water is under ice: the wash hits it like ground, snow dust and no spray.
				isFrozen = Seasons.Frozen(kind);
				return true;
			}

			isFrozen = false;
			return false;
		}

		// Puddles and marsh freeze in winter and spray nothing.
		public float GroundWaterAt(Vector3 point) => !Seasons.IsWinter && GroundWetness.Instance != null ? GroundWetness.Instance.WaterAt(point) : 0f;

		public float JetAir(float height, float spin) => RotorWashWater.JetAir(height, spin);

		public float JetRadiusAt(float height) => RotorWashWater.JetRadiusAt(height);

		public void Blow(in FpvRotorWashState state)
		{
			RotorWashLeaves leaves = RotorWashLeaves.Instance;
			if (leaves != null)
			{
				leaves.Blow(state.Ground, state.Height, state.Spin, state.Wind, state.Terrain);
			}

			RotorWashWater water = RotorWashWater.Instance;
			if (water == null)
			{
				return;
			}

			// Ripples only where they will be seen: water under the drone or within reach of its jet over a shore,
			// puddles in rain, marsh nearby.
			Vector3 point = state.Ground;
			float height = state.Height;
			bool isWet = state.IsOverWater || (GroundWetness.Instance != null && GroundWetness.Instance.WaterNear(state.Ground));

			if (!state.IsOverWater && state.HasGround && TryGetNearbyWater(state.Position, height, out float nearbyWater))
			{
				// The jet hits the ground under the drone; the water level counts only above the ground (a cut riverbank).
				if (nearbyWater > point.y)
				{
					point.y = nearbyWater;
					height = Mathf.Max(0f, state.Position.y - nearbyWater);
				}

				isWet = true;
			}

			Vector3 velocity = state.Velocity;
			water.Blow(point, height, isWet ? state.Spin : 0f, new Vector2(velocity.x, velocity.z).magnitude);
		}

		// The highest water on four circles in eight directions: at the edge of the jet and at 8, 16 and 24 m.
		private static bool TryGetNearbyWater(Vector3 position, float height, out float water)
		{
			float jet = RotorWashWater.JetRadiusAt(height);
			water = float.NegativeInfinity;

			for (int index = 0; index < NEARBY_WATER_SAMPLES; index++)
			{
				float angle = (index % 8) * Mathf.PI * 0.25f;
				float radius = index < 8 ? Mathf.Max(2f, jet) : (index / 8) * 8f;
				Vector3 sample = position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

				if (WaterSurfaces.TryGetHeight(sample, out float surface) && surface < position.y)
				{
					water = Mathf.Max(water, surface);
				}
			}

			return water > float.NegativeInfinity;
		}
	}
}
