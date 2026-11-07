using UnityEngine;

namespace UForce.FpvFlight.Video
{
	public readonly struct FpvWeatherSample
	{
		// 0..1 precipitation intensities.
		public readonly float Rain;
		public readonly float Snow;
		// World-space wind velocity, m/s.
		public readonly Vector3 Wind;
		// The sun (or moon) light. Null: no direct light.
		public readonly Light Sun;

		public FpvWeatherSample(float rain, float snow, Vector3 wind, Light sun)
		{
			Rain = Mathf.Clamp01(rain);
			Snow = Mathf.Clamp01(snow);
			Wind = wind;
			Sun = sun;
		}

		public static FpvWeatherSample Clear(Light sun) => new(0f, 0f, Vector3.zero, sun);
	}
}
