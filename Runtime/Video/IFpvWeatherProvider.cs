using UnityEngine;

namespace UForce.FpvFlight.Video
{
	// Implemented by the project's weather (Enviro, a custom system). FpvLensRain samples it every frame.
	public interface IFpvWeatherProvider
	{
		public FpvWeatherSample Sample(Vector3 position);
	}
}
