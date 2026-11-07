using System;

namespace UForce.FpvFlight.Audio
{
	// Allocation-free single-capsule microphone DSP, ported from PortableAudio (TIR) MicrophoneProcessor: synthesized wind
	// and prop wash gated by the input level, capsule high/low pass, preamp with motor boost, soft saturation, DC block and
	// a soft-knee compressor. Build it outside the audio callback; afterwards its state belongs to the audio thread.
	public sealed class FpvMicrophoneProcessor
	{
		private readonly FpvBiquadFilter r_highPassFilter;
		private readonly FpvBiquadFilter r_inputLowPassFilter;
		private readonly FpvBiquadFilter r_outputLowPassFilter;
		private readonly FpvBiquadFilter r_dcBlockFilter;

		private readonly double r_preampGain;
		private readonly double r_compressorThresholdDb;
		private readonly double r_compressorRatio;
		private readonly double r_windLevel;
		private readonly double r_propWashLevel;
		private readonly double r_noiseFloor;
		private readonly double r_outputGain;
		private readonly double r_attackResponse;
		private readonly double r_releaseResponse;
		private readonly double r_gainResponse;
		private readonly double r_flightConditionResponse;
		private readonly double r_windResponse;
		private readonly double r_gustResponse;
		private readonly double r_noiseNormalization;
		private readonly double r_modeFadeStep;

		private FpvNoiseGenerator _noiseGenerator;
		private double _wetMix;
		private double _audibleMix;
		private double _motorPower;
		private double _airflow;
		private double _windNoise;
		private double _gustNoise;
		private double _inputActivity;
		private double _compressorEnvelope;
		private double _compressorGain = 1d;
		private double _targetCompressorGain = 1d;
		private double _previousPreampSample;
		private int _framesUntilGainUpdate;

		public FpvMicrophoneProcessor(int sampleRate, FpvMicrophoneSettings settings = null, FpvMicrophoneMode initialMode = FpvMicrophoneMode.Microphone)
		{
			int validSampleRate = Math.Max(FpvMicrophoneDsp.MINIMUM_SAMPLE_RATE, Math.Min(FpvMicrophoneDsp.MAXIMUM_SAMPLE_RATE, sampleRate));
			FpvMicrophoneSettings source = settings ?? new FpvMicrophoneSettings();

			double highPassHz = ClampFinite(source.HighPassHz, FpvMicrophoneSettings.MINIMUM_HIGH_PASS_HZ, FpvMicrophoneSettings.MAXIMUM_HIGH_PASS_HZ, FpvMicrophoneSettings.DEFAULT_HIGH_PASS_HZ);
			double lowPassHz = ClampFinite(
				source.LowPassHz,
				highPassHz + FpvMicrophoneDsp.MINIMUM_FILTER_SEPARATION_HZ,
				validSampleRate * FpvMicrophoneDsp.FILTER_NYQUIST_SAFETY_FACTOR,
				FpvMicrophoneSettings.DEFAULT_LOW_PASS_HZ);

			r_highPassFilter = new FpvBiquadFilter(validSampleRate, highPassHz, true);
			r_inputLowPassFilter = new FpvBiquadFilter(validSampleRate, lowPassHz, false);
			r_outputLowPassFilter = new FpvBiquadFilter(validSampleRate, lowPassHz, false);
			r_dcBlockFilter = new FpvBiquadFilter(validSampleRate, FpvMicrophoneDsp.DC_BLOCK_FREQUENCY_HZ, true);

			r_preampGain = DecibelsToLinear(ClampFinite(source.PreampGainDb, 0d, FpvMicrophoneSettings.MAXIMUM_PREAMP_GAIN_DB, FpvMicrophoneSettings.DEFAULT_PREAMP_GAIN_DB));
			r_compressorThresholdDb = ClampFinite(
				source.CompressorThresholdDb,
				FpvMicrophoneSettings.MINIMUM_COMPRESSOR_THRESHOLD_DB,
				FpvMicrophoneSettings.MAXIMUM_COMPRESSOR_THRESHOLD_DB,
				FpvMicrophoneSettings.DEFAULT_COMPRESSOR_THRESHOLD_DB);
			r_compressorRatio = ClampFinite(source.CompressorRatio, FpvMicrophoneSettings.MINIMUM_COMPRESSOR_RATIO, FpvMicrophoneSettings.MAXIMUM_COMPRESSOR_RATIO, FpvMicrophoneSettings.DEFAULT_COMPRESSOR_RATIO);
			r_windLevel = ClampFinite(source.WindLevel, 0d, FpvMicrophoneSettings.MAXIMUM_WIND_LEVEL, FpvMicrophoneSettings.DEFAULT_WIND_LEVEL);
			r_propWashLevel = ClampFinite(source.PropWashLevel, 0d, FpvMicrophoneSettings.MAXIMUM_PROP_WASH_LEVEL, FpvMicrophoneSettings.DEFAULT_PROP_WASH_LEVEL);
			r_noiseFloor = DecibelsToLinear(ClampFinite(source.NoiseFloorDb, FpvMicrophoneSettings.MINIMUM_NOISE_FLOOR_DB, FpvMicrophoneSettings.MAXIMUM_NOISE_FLOOR_DB, FpvMicrophoneSettings.DEFAULT_NOISE_FLOOR_DB));
			r_outputGain = DecibelsToLinear(ClampFinite(source.OutputGainDb, FpvMicrophoneSettings.MINIMUM_OUTPUT_GAIN_DB, FpvMicrophoneSettings.MAXIMUM_OUTPUT_GAIN_DB, FpvMicrophoneSettings.DEFAULT_OUTPUT_GAIN_DB));

			r_attackResponse = CalculateResponse(validSampleRate, FpvMicrophoneDsp.COMPRESSOR_ATTACK_SECONDS);
			r_releaseResponse = CalculateResponse(validSampleRate, FpvMicrophoneDsp.COMPRESSOR_RELEASE_SECONDS);
			r_gainResponse = CalculateResponse(validSampleRate, FpvMicrophoneDsp.GAIN_SMOOTHING_SECONDS);
			r_flightConditionResponse = CalculateResponse(validSampleRate, FpvMicrophoneDsp.FLIGHT_CONDITION_SMOOTHING_SECONDS);
			r_windResponse = CalculateNoiseResponse(validSampleRate, FpvMicrophoneDsp.WIND_NOISE_CUTOFF_HZ);
			r_gustResponse = CalculateNoiseResponse(validSampleRate, FpvMicrophoneDsp.GUST_NOISE_CUTOFF_HZ);
			r_noiseNormalization = Math.Sqrt(3d * (2d - r_windResponse) / r_windResponse);
			r_modeFadeStep = 1d / (validSampleRate * FpvMicrophoneDsp.MODE_TRANSITION_SECONDS);
			_noiseGenerator = new FpvNoiseGenerator(FpvMicrophoneDsp.NOISE_SEED);

			FpvMicrophoneMode validInitialMode = NormalizeMode(initialMode);
			_wetMix = validInitialMode == FpvMicrophoneMode.Microphone ? 1d : 0d;
			_audibleMix = validInitialMode == FpvMicrophoneMode.Muted ? 0d : 1d;
		}

		// Processes an interleaved buffer in place. Allocates nothing and calls no Unity API.
		public void Process(float[] data, int channels, FpvMicrophoneMode mode, float motorPower, float airflow)
		{
			if (data == null || channels <= 0 || data.Length % channels != 0)
			{
				return;
			}

			FpvMicrophoneMode validMode = NormalizeMode(mode);
			double targetWetMix = validMode == FpvMicrophoneMode.Microphone ? 1d : 0d;
			double targetAudibleMix = validMode == FpvMicrophoneMode.Muted ? 0d : 1d;

			if (targetWetMix == 0d && _wetMix == 0d && _audibleMix == targetAudibleMix)
			{
				if (targetAudibleMix == 0d)
				{
					Array.Clear(data, 0, data.Length);
				}

				ResetSignalState();
				return;
			}

			double targetMotorPower = ClampFinite(motorPower, 0d, 1d, 0d);
			double targetAirflow = ClampFinite(airflow, 0d, 1d, 0d);
			for (int frame = 0; frame < data.Length; frame += channels)
			{
				_wetMix = MoveTowards(_wetMix, targetWetMix, r_modeFadeStep);
				_audibleMix = MoveTowards(_audibleMix, targetAudibleMix, r_modeFadeStep);
				_motorPower += (targetMotorPower - _motorPower) * r_flightConditionResponse;
				_airflow += (targetAirflow - _airflow) * r_flightConditionResponse;

				double microphoneInput = MixCapsuleInput(data, frame, channels, out double peak);
				_inputActivity += (peak - _inputActivity) * (peak > _inputActivity ? r_attackResponse : r_releaseResponse);
				double microphoneSample = ProcessMicrophoneSignal(microphoneInput);

				WriteOutputFrame(data, frame, channels, microphoneSample);
			}
		}

		private static double MixCapsuleInput(float[] data, int frame, int channels, out double peak)
		{
			double mono = 0d;
			peak = 0d;
			int capsuleChannels = 0;

			for (int channel = 0; channel < channels; channel++)
			{
				if (IsLfeChannel(channels, channel))
				{
					continue;
				}

				double sample = ClampFinite(data[frame + channel], -FpvMicrophoneDsp.INPUT_SAFETY_CEILING, FpvMicrophoneDsp.INPUT_SAFETY_CEILING, 0d);
				mono += sample;
				peak = Math.Max(peak, Math.Abs(sample));
				capsuleChannels++;
			}

			return capsuleChannels > 0 ? mono / capsuleChannels : 0d;
		}

		private double ProcessMicrophoneSignal(double capsuleInput)
		{
			double broadNoise = _noiseGenerator.NextSample();
			_windNoise += (broadNoise - _windNoise) * r_windResponse;
			_gustNoise += (_noiseGenerator.NextSample() - _gustNoise) * r_gustResponse;

			double gustGain = Math.Max(FpvMicrophoneDsp.MINIMUM_GUST_GAIN, Math.Min(FpvMicrophoneDsp.MAXIMUM_GUST_GAIN, 1d + _gustNoise * FpvMicrophoneDsp.GUST_DEPTH));
			double windAmount = r_windLevel * _airflow * _airflow + r_propWashLevel * _motorPower;
			double noiseGate = Math.Min(1d, _inputActivity * FpvMicrophoneDsp.NOISE_ACTIVITY_GAIN);
			double capsule = capsuleInput + noiseGate * (_windNoise * r_noiseNormalization * windAmount * gustGain + broadNoise * r_noiseFloor);

			double filteredInput = r_inputLowPassFilter.Process(r_highPassFilter.Process(capsule));
			double preampSample = filteredInput * r_preampGain * (1d + FpvMicrophoneDsp.MOTOR_PREAMP_BOOST * _motorPower);
			double saturated = 0.5d * (ApplySaturation((_previousPreampSample + preampSample) * 0.5d) + ApplySaturation(preampSample));
			_previousPreampSample = preampSample;

			double microphoneSample = r_dcBlockFilter.Process(r_outputLowPassFilter.Process(saturated));
			UpdateCompressor(Math.Abs(microphoneSample));

			return Math.Max(-FpvMicrophoneDsp.OUTPUT_CEILING, Math.Min(FpvMicrophoneDsp.OUTPUT_CEILING, microphoneSample * _compressorGain * r_outputGain));
		}

		private void UpdateCompressor(double level)
		{
			_compressorEnvelope += (level - _compressorEnvelope) * (level > _compressorEnvelope ? r_attackResponse : r_releaseResponse);
			if (--_framesUntilGainUpdate <= 0)
			{
				_framesUntilGainUpdate = FpvMicrophoneDsp.GAIN_UPDATE_INTERVAL_FRAMES;
				double levelOverThresholdDb = 20d * Math.Log10(Math.Max(_compressorEnvelope, FpvMicrophoneDsp.MINIMUM_ENVELOPE)) - r_compressorThresholdDb;
				double reductionDb = CalculateSoftKneeReduction(levelOverThresholdDb);
				_targetCompressorGain = DecibelsToLinear(-reductionDb * (1d - 1d / r_compressorRatio));
			}

			_compressorGain += (_targetCompressorGain - _compressorGain) * r_gainResponse;
		}

		private void WriteOutputFrame(float[] data, int frame, int channels, double microphoneSample)
		{
			for (int channel = 0; channel < channels; channel++)
			{
				if (_audibleMix == 0d)
				{
					data[frame + channel] = 0f;
					continue;
				}

				if (_wetMix > 0d)
				{
					double drySample = ClampFinite(data[frame + channel], -FpvMicrophoneDsp.INPUT_SAFETY_CEILING, FpvMicrophoneDsp.INPUT_SAFETY_CEILING, 0d);
					double wetSample = IsLfeChannel(channels, channel) ? 0d : microphoneSample;
					data[frame + channel] = (float)((drySample * (1d - _wetMix) + wetSample * _wetMix) * _audibleMix);
					continue;
				}

				data[frame + channel] *= (float)_audibleMix;
			}
		}

		private void ResetSignalState()
		{
			r_highPassFilter.Reset();
			r_inputLowPassFilter.Reset();
			r_outputLowPassFilter.Reset();
			r_dcBlockFilter.Reset();
			_compressorEnvelope = 0d;
			_inputActivity = 0d;
			_previousPreampSample = 0d;
			_windNoise = 0d;
			_gustNoise = 0d;
			_compressorGain = 1d;
			_targetCompressorGain = 1d;
			_framesUntilGainUpdate = 0;
		}

		private static double CalculateSoftKneeReduction(double levelOverThresholdDb)
		{
			if (levelOverThresholdDb <= -FpvMicrophoneDsp.COMPRESSOR_KNEE_HALF_WIDTH_DB)
			{
				return 0d;
			}

			if (levelOverThresholdDb >= FpvMicrophoneDsp.COMPRESSOR_KNEE_HALF_WIDTH_DB)
			{
				return levelOverThresholdDb;
			}

			double kneePosition = levelOverThresholdDb + FpvMicrophoneDsp.COMPRESSOR_KNEE_HALF_WIDTH_DB;
			return kneePosition * kneePosition / (2d * FpvMicrophoneDsp.COMPRESSOR_KNEE_WIDTH_DB);
		}

		private static bool IsLfeChannel(int channels, int channel) =>
			channels >= FpvMicrophoneDsp.SURROUND_CHANNEL_COUNT && channel == FpvMicrophoneDsp.LFE_CHANNEL_INDEX;

		private static FpvMicrophoneMode NormalizeMode(FpvMicrophoneMode mode) =>
			mode >= FpvMicrophoneMode.Microphone && mode <= FpvMicrophoneMode.Muted ? mode : FpvMicrophoneMode.Original;

		private static double ApplySaturation(double value)
		{
			double limited = Math.Max(-FpvMicrophoneDsp.SATURATION_INPUT_CEILING, Math.Min(FpvMicrophoneDsp.SATURATION_INPUT_CEILING, value));
			double squared = limited * limited;
			return limited * (27d + squared) / (27d + 9d * squared);
		}

		private static double CalculateResponse(int sampleRate, double seconds) => 1d - Math.Exp(-1d / (sampleRate * seconds));

		private static double CalculateNoiseResponse(int sampleRate, double cutoffHz) => 1d - Math.Exp(-2d * Math.PI * cutoffHz / sampleRate);

		private static double DecibelsToLinear(double decibels) => Math.Pow(10d, decibels / 20d);

		private static double MoveTowards(double value, double target, double step) =>
			value < target ? Math.Min(value + step, target) : Math.Max(value - step, target);

		private static double ClampFinite(double value, double minimum, double maximum, double fallback)
		{
			double finiteValue = double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
			return Math.Max(minimum, Math.Min(maximum, finiteValue));
		}
	}
}
