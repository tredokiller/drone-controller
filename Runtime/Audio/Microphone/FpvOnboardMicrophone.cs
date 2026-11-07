using System.Threading;
using UnityEngine;

namespace UForce.FpvFlight.Audio
{
	// The FPV camera's onboard microphone on the active AudioListener, ported from PortableAudio (TIR) OnboardMicrophoneFilter:
	// everything the pilot hears goes through the capsule, with wind and prop wash from FpvMotorAudio (SetFlightConditions).
	// Put it on the AudioListener's GameObject, without an AudioSource there.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(AudioListener))]
	public sealed class FpvOnboardMicrophone : MonoBehaviour
	{
		[SerializeField] private FpvMicrophoneMode _mode = FpvMicrophoneMode.Microphone;
		[SerializeField] private FpvMicrophoneSettings _settings = new();

		private AudioListener _listener;
		private FpvMicrophoneProcessor _processor;
		private int _isSettingsDirty;
		private int _requestedMode;
		private int _isListening;
		private float _motorPower;
		private float _airflow;

		public FpvMicrophoneMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				Volatile.Write(ref _requestedMode, (int)_mode);
			}
		}

		public FpvMicrophoneSettings Settings => _settings;

		// Main thread: motor power and airflow, both 0..1.
		public void SetFlightConditions(float motorPower, float normalizedAirflow)
		{
			Volatile.Write(ref _motorPower, motorPower);
			Volatile.Write(ref _airflow, normalizedAirflow);
		}

		// Main thread: the DSP is rebuilt safely on the next Update.
		public void ApplySettings(FpvMicrophoneSettings settings)
		{
			if (settings == null)
			{
				return;
			}

			_settings = settings.Clone();
			Interlocked.Exchange(ref _isSettingsDirty, 1);
		}

		private void OnEnable()
		{
			_listener = GetComponent<AudioListener>();
			Mode = _mode;
			RebuildProcessor();
			SynchronizeListenerState();
			AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
		}

		private void OnDisable()
		{
			Volatile.Write(ref _isListening, 0);
			AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
		}

		private void OnValidate()
		{
			Interlocked.Exchange(ref _isSettingsDirty, 1);
		}

		private void Update()
		{
			if (Interlocked.Exchange(ref _isSettingsDirty, 0) != 0)
			{
				Mode = _mode;
				RebuildProcessor();
			}

			SynchronizeListenerState();
		}

		private void HandleAudioConfigurationChanged(bool isDeviceChanged)
		{
			Interlocked.Exchange(ref _isSettingsDirty, 1);
		}

		private void SynchronizeListenerState()
		{
			bool isListenerActive = isActiveAndEnabled && _listener != null && _listener.isActiveAndEnabled;
			Volatile.Write(ref _isListening, isListenerActive ? 1 : 0);
		}

		private void RebuildProcessor()
		{
			Volatile.Write(ref _processor, new FpvMicrophoneProcessor(AudioSettings.outputSampleRate, _settings.Clone(), _mode));
		}

		private void OnAudioFilterRead(float[] data, int channels)
		{
			if (Volatile.Read(ref _isListening) == 0)
			{
				return;
			}

			Volatile.Read(ref _processor)?.Process(
				data,
				channels,
				(FpvMicrophoneMode)Volatile.Read(ref _requestedMode),
				Volatile.Read(ref _motorPower),
				Volatile.Read(ref _airflow));
		}
	}
}
