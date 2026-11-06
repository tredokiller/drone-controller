using System;
using UnityEngine;

namespace UForce.FpvFlight.Rc
{
	[Serializable]
	public sealed class FailsafeSettings
	{
		[Tooltip("Time without a valid frame before stage 1 starts (Betaflight rx frame timeout).")]
		[SerializeField, Min(0.02f)] private float _rxLossTimeout = 0.1f;
		[Tooltip("Stage 1 duration before the drop (Betaflight failsafe_delay, 15 = 1.5 s).")]
		[SerializeField, Min(0f)] private float _failsafeDelay = 1.5f;
		[SerializeField] private FailsafeThrottleMode _stageOneThrottle = FailsafeThrottleMode.Low;
		[SerializeField, Range(0f, 1f)] private float _stageOneFixedThrottle = 0.3f;

		public FailsafeSettings()
		{
		}

		public FailsafeSettings(float rxLossTimeout, float failsafeDelay, FailsafeThrottleMode stageOneThrottle, float stageOneFixedThrottle)
		{
			_rxLossTimeout = Mathf.Max(0.02f, rxLossTimeout);
			_failsafeDelay = Mathf.Max(0f, failsafeDelay);
			_stageOneThrottle = stageOneThrottle;
			_stageOneFixedThrottle = Mathf.Clamp01(stageOneFixedThrottle);
		}

		public float RxLossTimeout => _rxLossTimeout;
		public float FailsafeDelay => _failsafeDelay;
		public FailsafeThrottleMode StageOneThrottle => _stageOneThrottle;
		public float StageOneFixedThrottle => _stageOneFixedThrottle;
	}
}
