using UnityEngine;

namespace UForce.FpvFlight.Audio
{
	// Motor sound of an FPV drone, ported from Interceptor Drones (DroneAudio, P1-SUN prefab values). On arm the loop spools
	// up over 1.2 s; volume and pitch follow the motor output. At speed the wind in the camera's microphone masks the motors:
	// up to 12 dB quieter and their low end cut (10 to 900 Hz) between 20 and 150 km/h, fast to duck and slower to recover,
	// like the camera's automatic gain. Feeds the onboard microphone's wind and prop wash.
	[DisallowMultipleComponent]
	public sealed class FpvMotorAudio : MonoBehaviour
	{
		private const float KMH_TO_MS = 1f / 3.6f;

		[SerializeField] private AudioSource _audioSource;
		[Tooltip("Must implement IFpvMotorSource. Empty: the FpvFlightController in the parents.")]
		[SerializeField] private MonoBehaviour _motorSourceBehaviour;
		[Tooltip("Optional. Empty: the first one in the children (on the camera's AudioListener).")]
		[SerializeField] private FpvOnboardMicrophone _microphone;

		[Header("Motors")]
		[SerializeField, Range(0f, 1f)] private float _minVolume = 0.3f;
		[SerializeField, Range(0f, 1f)] private float _maxVolume = 1f;
		[SerializeField] private float _minPitch = 0.8f;
		[SerializeField] private float _maxPitch = 1.5f;
		[SerializeField, Min(0f)] private float _lerpSpeed = 1f;
		[SerializeField, Min(0.01f)] private float _spoolUpDuration = 1.2f;

		[Header("Ambient breeze in the microphone")]
		[SerializeField, Range(0f, 1f)] private float _breezeAirflow = 0.5f;
		[SerializeField, Range(0f, 1f)] private float _breezeVariation = 0.4f;
		[SerializeField] private float _breezeGustSpeed = 0.15f;
		[Tooltip("Speed at which the wind in the microphone is at full strength, km/h.")]
		[SerializeField, Min(10f)] private float _windFullSpeedKmh = 160f;

		[Header("Wind masking")]
		[Tooltip("How much quieter the motors get when the wind is at full strength, dB.")]
		[SerializeField, Range(0f, 24f)] private float _windDuckDb = 12f;
		[Tooltip("Speed at which the motors start to drown in the wind, and at which they are fully masked, km/h.")]
		[SerializeField] private Vector2 _windDuckSpeedKmh = new(20f, 150f);
		[Tooltip("Low cut of the motors without and with full airflow, Hz.")]
		[SerializeField] private Vector2 _windLowCutHz = new(10f, 900f);
		[Tooltip("How fast the gain ducks on a gust and recovers, s.")]
		[SerializeField] private Vector2 _windDuckAttackRelease = new(0.25f, 0.6f);

		private IFpvMotorSource _motorSource;
		private AudioHighPassFilter _lowCut;
		private bool _wasArmed;
		private float _breezeNoiseOffset;
		private float _engineSpool;
		private float _engineVolume;
		private float _windMask;
		private float _effectsVolume = 1f;

		private void Awake()
		{
			_motorSource = _motorSourceBehaviour as IFpvMotorSource ?? GetComponentInParent<FpvFlightController>();
			if (!_microphone)
			{
				_microphone = GetComponentInParent<FpvFlightController>() is { } controller
					? controller.GetComponentInChildren<FpvOnboardMicrophone>(true)
					: GetComponentInChildren<FpvOnboardMicrophone>(true);
			}

			_breezeNoiseOffset = Random.Range(0f, 1000f);
			ConfigureAudioSource();
		}

		public void SetMotorSource(IFpvMotorSource motorSource) => _motorSource = motorSource;

		// Game effects volume 0..1: scales what the microphone adds (wind, prop wash), as Interceptor's master × sfx volume.
		public void SetEffectsVolume(float volume) => _effectsVolume = Mathf.Clamp01(volume);

		private void OnDisable()
		{
			StopEngine();
			_wasArmed = false;
		}

		private void Update()
		{
			if (!_audioSource || _motorSource == null)
			{
				return;
			}

			bool isArmed = _motorSource.IsArmed;
			if (isArmed != _wasArmed)
			{
				_wasArmed = isArmed;
				if (isArmed)
				{
					StartEngine();
				}
				else
				{
					StopEngine();
				}
			}

			float motor = isArmed ? Mathf.Clamp01(_motorSource.MotorOutput) : 0f;
			float deltaTime = Time.deltaTime;
			_engineSpool = Mathf.MoveTowards(_engineSpool, isArmed ? 1f : 0f, deltaTime / _spoolUpDuration);
			float spoolEnvelope = Mathf.SmoothStep(0f, 1f, _engineSpool);

			float targetVolume = Mathf.Lerp(_minVolume, _maxVolume, motor) * spoolEnvelope;
			float targetPitch = Mathf.Lerp(_minPitch, _maxPitch, motor);
			_engineVolume = Mathf.Lerp(_engineVolume, targetVolume, deltaTime * _lerpSpeed);
			_audioSource.pitch = Mathf.Lerp(_audioSource.pitch, targetPitch, deltaTime * _lerpSpeed);

			float speed = _motorSource.Velocity.magnitude;
			UpdateMicrophone(motor, speed);
			MaskEngineByWind(speed / KMH_TO_MS, deltaTime);
			_audioSource.volume = _engineVolume * Mathf.Pow(10f, -_windDuckDb * _windMask / 20f);
		}

		private void StartEngine()
		{
			if (_audioSource.isPlaying)
			{
				return;
			}

			_engineSpool = 0f;
			_engineVolume = 0f;
			_audioSource.volume = 0f;
			_audioSource.Play();
		}

		private void StopEngine()
		{
			if (!_audioSource)
			{
				return;
			}

			_audioSource.Stop();
			_audioSource.volume = 0f;
			_engineVolume = 0f;
			_engineSpool = 0f;
		}

		private void MaskEngineByWind(float speedKmh, float deltaTime)
		{
			float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_windDuckSpeedKmh.x, _windDuckSpeedKmh.y, speedKmh));
			float seconds = target > _windMask ? _windDuckAttackRelease.x : _windDuckAttackRelease.y;
			_windMask = Mathf.MoveTowards(_windMask, target, deltaTime / Mathf.Max(seconds, 0.01f));

			if (_lowCut)
			{
				_lowCut.cutoffFrequency = Mathf.Lerp(_windLowCutHz.x, _windLowCutHz.y, _windMask);
			}
		}

		private void UpdateMicrophone(float motor, float speed)
		{
			if (!_microphone)
			{
				return;
			}

			float speedAirflow = speed / (_windFullSpeedKmh * KMH_TO_MS);
			float gust = Mathf.PerlinNoise(Time.time * _breezeGustSpeed + _breezeNoiseOffset, 0f);
			float breeze = _breezeAirflow * Mathf.Lerp(1f - _breezeVariation, 1f + _breezeVariation, gust);
			float airflow = Mathf.Clamp01(Mathf.Sqrt(speedAirflow * speedAirflow + breeze * breeze));
			_microphone.SetFlightConditions(motor * _effectsVolume, airflow * Mathf.Sqrt(_effectsVolume));
		}

		private void ConfigureAudioSource()
		{
			if (!_audioSource)
			{
				_audioSource = GetComponent<AudioSource>();
			}

			if (!_audioSource)
			{
				Debug.LogError($"[{nameof(FpvMotorAudio)}] No AudioSource assigned.", this);
				return;
			}

			_audioSource.playOnAwake = false;
			_audioSource.loop = true;
			_audioSource.dopplerLevel = 0f;
			_audioSource.volume = 0f;
			_audioSource.pitch = _minPitch;

			if (!_audioSource.TryGetComponent(out _lowCut))
			{
				_lowCut = _audioSource.gameObject.AddComponent<AudioHighPassFilter>();
			}

			_lowCut.cutoffFrequency = _windLowCutHz.x;
			_lowCut.highpassResonanceQ = 1f;
		}
	}
}
