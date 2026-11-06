using UnityEngine;

namespace UForce.FpvFlight.Environment
{
	// Implemented by the project (weather system, wind zones). Sampled once per physics step at the drone position.
	public interface IWindProvider
	{
		public WindSample Sample(Vector3 position, float time);
	}
}
