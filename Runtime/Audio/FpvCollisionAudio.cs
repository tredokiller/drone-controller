using UnityEngine;

namespace UForce.FpvFlight.Audio
{
	// Touch and hit sounds, as Interceptor Drones' DroneMovementController: contacts under 0.1 m/s are ignored, under 2 m/s
	// play the touch sound, faster ones the hit sound. Put it on the drone's Rigidbody.
	[DisallowMultipleComponent]
	public sealed class FpvCollisionAudio : MonoBehaviour
	{
		private const float IGNORED_SPEED = 0.1f;
		private const float HIT_SPEED = 2f;

		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _touchClip;
		[SerializeField] private AudioClip _hitClip;
		[SerializeField, Range(0f, 1f)] private float _volume = 1f;
		[Tooltip("Minimum time between two sounds, s.")]
		[SerializeField, Min(0f)] private float _cooldown = 0.1f;

		private float _nextTime;

		private void OnCollisionEnter(Collision collision)
		{
			if (!_audioSource || Time.time < _nextTime)
			{
				return;
			}

			float speed = collision.relativeVelocity.magnitude;
			if (speed < IGNORED_SPEED)
			{
				return;
			}

			AudioClip clip = speed < HIT_SPEED ? _touchClip : _hitClip;
			if (!clip)
			{
				return;
			}

			_audioSource.PlayOneShot(clip, _volume);
			_nextTime = Time.time + _cooldown;
		}
	}
}
