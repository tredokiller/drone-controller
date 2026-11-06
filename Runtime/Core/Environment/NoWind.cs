using UnityEngine;

namespace UForce.FpvFlight.Environment
{
	public sealed class NoWind : IWindProvider
	{
		public static readonly NoWind Instance = new();

		public WindSample Sample(Vector3 position, float time) => WindSample.Calm;
	}
}
