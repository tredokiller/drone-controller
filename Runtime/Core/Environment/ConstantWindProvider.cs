using UnityEngine;

namespace UForce.FpvFlight.Environment
{
	// Steady wind with Perlin gusts. Put it in the scene and assign it to FpvFlightController, or replace it with the project's weather.
	public sealed class ConstantWindProvider : MonoBehaviour, IWindProvider
	{
		[Tooltip("Direction the wind blows toward, degrees clockwise from world +Z.")]
		[SerializeField, Range(0f, 360f)] private float _heading;
		[SerializeField, Min(0f)] private float _speed = 3f;
		[SerializeField, Min(0f)] private float _gustAmplitude = 1.5f;
		[SerializeField, Min(0.01f)] private float _gustFrequency = 0.3f;
		[SerializeField, Range(0f, 1f)] private float _turbulence = 0.2f;

		public float Speed => _speed;

		public WindSample Sample(Vector3 position, float time)
		{
			Vector3 direction = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
			float gust = (Mathf.PerlinNoise(time * _gustFrequency, position.x * 0.01f) - 0.5f) * 2f * _gustAmplitude;
			float vertical = (Mathf.PerlinNoise(position.z * 0.01f, time * _gustFrequency * 1.7f) - 0.5f) * _gustAmplitude * 0.3f;
			Vector3 velocity = direction * Mathf.Max(0f, _speed + gust) + Vector3.up * vertical;
			return new WindSample(velocity, _turbulence);
		}

		public void Configure(float heading, float speed, float gustAmplitude, float turbulence)
		{
			_heading = Mathf.Repeat(heading, 360f);
			_speed = Mathf.Max(0f, speed);
			_gustAmplitude = Mathf.Max(0f, gustAmplitude);
			_turbulence = Mathf.Clamp01(turbulence);
		}
	}
}
