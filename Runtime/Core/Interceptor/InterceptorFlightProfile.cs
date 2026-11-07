using UnityEngine;

namespace UForce.FpvFlight.Interceptor
{
	// Interceptor Drones' flight model settings (DroneMovementController, Manual mode, and the drone presets of
	// ManualDroneCustomizationConfig). Defaults are the P1-SUN preset and prefab. A drone with this profile flies exactly as
	// in Interceptor: linear stick rates with an almost instant rate response, thrust as a multiple of the weight with no
	// motor lag and no aerodynamic drag, a horizontal thrust assist when tilted past 15°, Rigidbody damping, extra gravity
	// at zero throttle, and hard limits on speed and on rates.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Interceptor Flight Profile", fileName = "InterceptorFlightProfile")]
	public sealed class InterceptorFlightProfile : ScriptableObject
	{
		[Header("Limits")]
		[Tooltip("Hard speed limit, km/h (MaxSpeedKmh).")]
		[SerializeField, Min(1f)] private float _maxSpeedKmh = 300f;
		[Tooltip("Full-stick rates and hard rate limits, deg/s (MaxPitchRate, MaxRollRate, MaxYawRate).")]
		[SerializeField] private Vector3 _maxRates = new(700f, 500f, 700f);

		[Header("Rate response")]
		[Tooltip("Angular acceleration per rad/s of rate error, 1/s (Pitch/Roll/YawSensitivity). 50 corrects within one 50 Hz step.")]
		[SerializeField] private Vector3 _sensitivity = new(50f, 50f, 50f);
		[Tooltip("After the stick is released on an axis, brake at AngularBrakeAccel instead of following the rate law (HardStopOnRelease).")]
		[SerializeField] private bool _isHardStopOnRelease = true;
		[Tooltip("Braking angular acceleration after release, rad/s² (AngularBrakeAccel).")]
		[SerializeField, Min(0f)] private float _angularBrakeAccel = 30.901697f;
		[Tooltip("Rate below which the release brake ends, rad/s.")]
		[SerializeField, Min(0f)] private float _angularBrakeStopThreshold = 0.03f;

		[Header("Thrust")]
		[Tooltip("Full-throttle thrust as a multiple of the weight (ThrottlePower).")]
		[SerializeField, Min(0f)] private float _throttlePower = 7f;
		[Tooltip("Horizontal thrust factor (HorizontalThrust); scales the thrust assist.")]
		[SerializeField, Min(0f)] private float _horizontalThrust = 38.736492f;
		[Tooltip("Thrust assist (ThrustAssist): above 15° of pitch or roll, horizontal force = assist × sin(angle) × horizontal thrust × weight.")]
		[SerializeField, Min(0f)] private float _thrustAssist = 0.119012356f;
		[Tooltip("Extra gravity at zero throttle, multiples of g (fast drop with throttle cut).")]
		[SerializeField, Min(0f)] private float _zeroThrottleExtraGravity = 1.5f;
		[Tooltip("Throttle at which the extra gravity has faded out.")]
		[SerializeField, Range(0.01f, 1f)] private float _zeroThrottleFallFade = 0.15f;

		[Header("Body")]
		[Tooltip("Frame mass without battery and payload, kg (BaseMassKg).")]
		[SerializeField, Min(0.05f)] private float _baseMassKg = 2.037953f;
		[Tooltip("Rigidbody linear damping (LinearDamping).")]
		[SerializeField, Min(0f)] private float _linearDamping = 0.18026066f;
		[Tooltip("Rigidbody angular damping (AngularDamping).")]
		[SerializeField, Min(0f)] private float _angularDamping = 1.5450835f;
		[Tooltip("Radius of the ground probe around the drone, m: on the ground yaw is ignored and the extra gravity is off.")]
		[SerializeField, Min(0f)] private float _groundHeight = 0.295f;

		public float MaxSpeedKmh => _maxSpeedKmh;
		public float MaxSpeed => _maxSpeedKmh / 3.6f;
		// x pitch, y yaw, z roll, deg/s.
		public Vector3 MaxRates => _maxRates;
		public Vector3 Sensitivity => _sensitivity;
		public bool IsHardStopOnRelease => _isHardStopOnRelease;
		public float AngularBrakeAccel => _angularBrakeAccel;
		public float AngularBrakeStopThreshold => _angularBrakeStopThreshold;
		public float ThrottlePower => _throttlePower;
		public float HorizontalThrust => _horizontalThrust;
		public float ThrustAssist => _thrustAssist;
		public float ZeroThrottleExtraGravity => _zeroThrottleExtraGravity;
		public float ZeroThrottleFallFade => _zeroThrottleFallFade;
		public float BaseMassKg => _baseMassKg;
		public float LinearDamping => _linearDamping;
		public float AngularDamping => _angularDamping;
		public float GroundHeight => _groundHeight;

		public void Configure(float maxSpeedKmh, Vector3 maxRates, Vector3 sensitivity, float throttlePower, float horizontalThrust, float thrustAssist,
			float baseMassKg, float linearDamping, float angularDamping, bool isHardStopOnRelease, float angularBrakeAccel)
		{
			_maxSpeedKmh = Mathf.Max(1f, maxSpeedKmh);
			_maxRates = maxRates;
			_sensitivity = sensitivity;
			_throttlePower = Mathf.Max(0f, throttlePower);
			_horizontalThrust = Mathf.Max(0f, horizontalThrust);
			_thrustAssist = Mathf.Max(0f, thrustAssist);
			_baseMassKg = Mathf.Max(0.05f, baseMassKg);
			_linearDamping = Mathf.Max(0f, linearDamping);
			_angularDamping = Mathf.Max(0f, angularDamping);
			_isHardStopOnRelease = isHardStopOnRelease;
			_angularBrakeAccel = Mathf.Max(0f, angularBrakeAccel);
		}
	}
}
