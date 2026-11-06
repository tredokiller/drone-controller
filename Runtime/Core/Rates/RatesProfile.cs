using UnityEngine;

namespace UForce.FpvFlight.Rates
{
	// Rates are entered as in Betaflight Configurator. Defaults are Betaflight's: RC rate 1.00, super rate 0.70, expo 0.00.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Rates Profile", fileName = "RatesProfile")]
	public sealed class RatesProfile : ScriptableObject
	{
		[SerializeField] private AxisRates _roll = new();
		[SerializeField] private AxisRates _pitch = new();
		[SerializeField] private AxisRates _yaw = new();
		[SerializeField, Range(200f, BetaflightRates.DEFAULT_RATE_LIMIT)] private float _rateLimit = BetaflightRates.DEFAULT_RATE_LIMIT;

		public AxisRates Roll => _roll;
		public AxisRates Pitch => _pitch;
		public AxisRates Yaw => _yaw;
		public float RateLimit => _rateLimit;

		public float MaxRollRate => RollRate(1f);
		public float MaxPitchRate => PitchRate(1f);
		public float MaxYawRate => YawRate(1f);

		public float RollRate(float stick) => _roll.Evaluate(stick, _rateLimit);

		public float PitchRate(float stick) => _pitch.Evaluate(stick, _rateLimit);

		public float YawRate(float stick) => _yaw.Evaluate(stick, _rateLimit);

		public void Configure(AxisRates roll, AxisRates pitch, AxisRates yaw, float rateLimit = BetaflightRates.DEFAULT_RATE_LIMIT)
		{
			_roll = roll ?? new AxisRates();
			_pitch = pitch ?? new AxisRates();
			_yaw = yaw ?? new AxisRates();
			_rateLimit = Mathf.Clamp(rateLimit, 200f, BetaflightRates.DEFAULT_RATE_LIMIT);
		}
	}
}
