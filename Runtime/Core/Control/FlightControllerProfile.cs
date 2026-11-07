using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Control
{
	// Flight controller tuning: how the drone answers the sticks. Physical limits live in AirframeDefinition.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Flight Controller Profile", fileName = "FlightControllerProfile")]
	public sealed class FlightControllerProfile : ScriptableObject
	{
		[Header("Rate loop")]
		[Tooltip("Closed-loop time constant of the roll and pitch rate loop. Clamped to at least 1.5 physics steps.")]
		[SerializeField, Min(0.005f)] private float _rollPitchResponseTime = 0.04f;
		[SerializeField, Min(0.005f)] private float _yawResponseTime = 0.06f;
		[Tooltip("I-term time constant. 0 disables the I-term.")]
		[SerializeField, Min(0f)] private float _integralTime = 0.6f;
		[Tooltip("I-term limit as a fraction of the airframe's maximum angular acceleration.")]
		[SerializeField, Range(0f, 1f)] private float _integralLimit = 0.3f;

		[Header("Angle mode")]
		[Tooltip("Maximum tilt at full stick (Betaflight angle_limit).")]
		[SerializeField, Range(10f, 85f)] private float _angleLimit = 60f;
		[Tooltip("Rotation rate per unit of tilt error, 1/s (Betaflight angle_p_gain 50 = 5).")]
		[SerializeField, Min(0.1f)] private float _angleStrength = 5f;

		[Header("Horizon mode")]
		[Tooltip("Pull towards level at centre stick, 1/s. Same units as the angle strength.")]
		[SerializeField, Min(0f)] private float _horizonLevelStrength = 5f;
		[Tooltip("Stick deflection at which levelling has faded out completely (Betaflight horizon_transition).")]
		[SerializeField, Range(0.1f, 1f)] private float _horizonTransition = 0.75f;

		[Header("Throttle")]
		[SerializeField, Range(0f, 1f)] private float _throttleMid = 0.5f;
		[SerializeField, Range(0f, 1f)] private float _throttleExpo;
		[Tooltip("Arming is allowed only below this throttle (Betaflight min_check).")]
		[SerializeField, Range(0f, 0.3f)] private float _armThrottleMax = 0.05f;

		[Header("Radio link")]
		[SerializeField] private FailsafeSettings _failsafe = new();
		[Tooltip("How fast link latency may grow, seconds per second.")]
		[SerializeField, Min(0f)] private float _latencySlewRate = 1f;

		[Header("Disturbances")]
		[Tooltip("Random angular acceleration at turbulence 1, deg/s².")]
		[SerializeField, Min(0f)] private float _turbulenceAngularAcceleration = 900f;

		public float RollPitchResponseTime => _rollPitchResponseTime;
		public float YawResponseTime => _yawResponseTime;
		public float IntegralTime => _integralTime;
		public float IntegralLimit => _integralLimit;
		public float AngleLimit => _angleLimit;
		public float AngleStrength => _angleStrength;
		public float HorizonLevelStrength => _horizonLevelStrength;
		public float HorizonTransition => _horizonTransition;
		public float ThrottleMid => _throttleMid;
		public float ThrottleExpo => _throttleExpo;
		public float ArmThrottleMax => _armThrottleMax;
		public FailsafeSettings Failsafe => _failsafe;
		public float LatencySlewRate => _latencySlewRate;
		public float TurbulenceAngularAcceleration => _turbulenceAngularAcceleration;

		// Betaflight thr_mid / thr_expo curve. The defaults (0.5, 0) leave throttle linear.
		public float ApplyThrottleCurve(float throttle)
		{
			float value = Mathf.Clamp01(throttle);
			if (_throttleExpo <= 0f)
			{
				return value;
			}

			float offset = value - _throttleMid;
			float range = offset > 0f ? 1f - _throttleMid : _throttleMid;
			if (range <= 0f)
			{
				return value;
			}

			float normalized = offset / range;
			return Mathf.Clamp01(_throttleMid + offset * (1f - _throttleExpo + _throttleExpo * normalized * normalized));
		}

		public void Configure(float angleLimit, float angleStrength, FailsafeSettings failsafe)
		{
			_angleLimit = Mathf.Clamp(angleLimit, 10f, 85f);
			_angleStrength = Mathf.Max(0.1f, angleStrength);
			_failsafe = failsafe ?? new FailsafeSettings();
		}

		public void ConfigureRateLoop(float rollPitchResponseTime, float yawResponseTime, float integralTime, float integralLimit)
		{
			_rollPitchResponseTime = Mathf.Max(0.005f, rollPitchResponseTime);
			_yawResponseTime = Mathf.Max(0.005f, yawResponseTime);
			_integralTime = Mathf.Max(0f, integralTime);
			_integralLimit = Mathf.Clamp01(integralLimit);
		}

		public void ConfigureHorizon(float levelStrength, float transition)
		{
			_horizonLevelStrength = Mathf.Max(0f, levelStrength);
			_horizonTransition = Mathf.Clamp(transition, 0.1f, 1f);
		}

		public void ConfigureDisturbances(float turbulenceAngularAcceleration)
		{
			_turbulenceAngularAcceleration = Mathf.Max(0f, turbulenceAngularAcceleration);
		}
	}
}
