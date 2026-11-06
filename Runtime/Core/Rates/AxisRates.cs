using System;
using UnityEngine;

namespace UForce.FpvFlight.Rates
{
	[Serializable]
	public sealed class AxisRates
	{
		[SerializeField, Range(0.01f, BetaflightRates.MAX_RC_RATE)] private float _rcRate = 1f;
		[SerializeField, Range(0f, 1f)] private float _superRate = 0.7f;
		[SerializeField, Range(0f, 1f)] private float _expo;

		public AxisRates()
		{
		}

		public AxisRates(float rcRate, float superRate, float expo)
		{
			_rcRate = Mathf.Clamp(rcRate, 0.01f, BetaflightRates.MAX_RC_RATE);
			_superRate = Mathf.Clamp01(superRate);
			_expo = Mathf.Clamp01(expo);
		}

		public float RcRate => _rcRate;
		public float SuperRate => _superRate;
		public float Expo => _expo;

		public float Evaluate(float stick, float rateLimit) => BetaflightRates.Evaluate(stick, _rcRate, _superRate, _expo, rateLimit);
	}
}
