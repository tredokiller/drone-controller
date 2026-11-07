using UnityEngine;

namespace UForce.FpvFlight.Interceptor
{
	// Interceptor Drones' Manual-mode physics (DroneMovementController.FixedUpdate and ApplyInstantAttitude), step for step.
	// Rates and forces are returned for FpvFlightController to apply in Interceptor's order: thrust, thrust assist, rate
	// acceleration, zero-throttle gravity, then the rate and speed limits.
	public sealed class InterceptorFlightModel
	{
		private const float THRUST_ASSIST_MIN_ANGLE = 15f;
		private const float RATE_INPUT_EPSILON = 0.0001f;

		private Vector3 _previousRateInput;
		private bool _isBrakingPitch;
		private bool _isBrakingYaw;
		private bool _isBrakingRoll;

		public void Reset()
		{
			_previousRateInput = Vector3.zero;
			_isBrakingPitch = false;
			_isBrakingYaw = false;
			_isBrakingRoll = false;
		}

		// Body thrust along body up, N: throttle 0..1 times ThrottlePower times the weight.
		public static float Thrust(InterceptorFlightProfile profile, float throttle, float mass, float gravity) =>
			Mathf.Clamp01(throttle) * profile.ThrottlePower * mass * gravity;

		// World-space horizontal force when pitched or rolled past 15° (Interceptor's thrust assist).
		public static Vector3 ThrustAssist(InterceptorFlightProfile profile, Quaternion rotation, float mass, float gravity)
		{
			if (profile.ThrustAssist == 0f)
			{
				return Vector3.zero;
			}

			Vector3 euler = rotation.eulerAngles;
			float pitch = Mathf.DeltaAngle(0f, euler.x);
			float roll = Mathf.DeltaAngle(0f, euler.z);
			float scale = profile.ThrustAssist * profile.HorizontalThrust * mass * gravity;

			float forwardShare = Mathf.Abs(pitch) > THRUST_ASSIST_MIN_ANGLE ? Mathf.Sin(pitch * Mathf.Deg2Rad) : 0f;
			float rightShare = Mathf.Abs(roll) > THRUST_ASSIST_MIN_ANGLE ? -Mathf.Sin(roll * Mathf.Deg2Rad) : 0f;
			Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
			Vector3 right = Vector3.ProjectOnPlane(rotation * Vector3.right, Vector3.up).normalized;
			return scale * (forwardShare * forward + rightShare * right);
		}

		// Extra downward acceleration near zero throttle, m/s², faded out by the fall-fade throttle. None on the ground.
		public static Vector3 ZeroThrottleGravity(InterceptorFlightProfile profile, float throttle, bool isGrounded, float gravity)
		{
			if (isGrounded || profile.ZeroThrottleExtraGravity <= 0f)
			{
				return Vector3.zero;
			}

			float fallStrength = 1f - Mathf.InverseLerp(0f, profile.ZeroThrottleFallFade, Mathf.Abs(throttle));
			return fallStrength <= 0f ? Vector3.zero : Vector3.down * (gravity * profile.ZeroThrottleExtraGravity * fallStrength);
		}

		// Body angular acceleration, rad/s² (x pitch, y yaw, z roll), toward the target body rates: (target - rate) × sensitivity,
		// or the release brake after the stick has just been centred on an axis.
		public Vector3 AngularAcceleration(InterceptorFlightProfile profile, Vector3 rateInput, Vector3 targetRate, Vector3 bodyRate, float deltaTime)
		{
			if (profile.IsHardStopOnRelease)
			{
				UpdateBrake(_previousRateInput.x, rateInput.x, ref _isBrakingPitch);
				UpdateBrake(_previousRateInput.y, rateInput.y, ref _isBrakingYaw);
				UpdateBrake(_previousRateInput.z, rateInput.z, ref _isBrakingRoll);
			}
			else
			{
				_isBrakingPitch = false;
				_isBrakingYaw = false;
				_isBrakingRoll = false;
			}

			_previousRateInput = rateInput;
			Vector3 sensitivity = profile.Sensitivity;
			return new Vector3(
				AxisAcceleration(profile, bodyRate.x, targetRate.x, sensitivity.x, ref _isBrakingPitch, deltaTime),
				AxisAcceleration(profile, bodyRate.y, targetRate.y, sensitivity.y, ref _isBrakingYaw, deltaTime),
				AxisAcceleration(profile, bodyRate.z, targetRate.z, sensitivity.z, ref _isBrakingRoll, deltaTime));
		}

		private static void UpdateBrake(float previousInput, float input, ref bool isBraking)
		{
			if (Mathf.Abs(input) > RATE_INPUT_EPSILON)
			{
				isBraking = false;
			}
			else if (Mathf.Abs(previousInput) > RATE_INPUT_EPSILON)
			{
				isBraking = true;
			}
		}

		private static float AxisAcceleration(InterceptorFlightProfile profile, float rate, float target, float sensitivity, ref bool isBraking, float deltaTime)
		{
			if (isBraking)
			{
				if (Mathf.Abs(rate) <= profile.AngularBrakeStopThreshold)
				{
					isBraking = false;
				}
				else
				{
					return BrakeAcceleration(profile, rate, deltaTime);
				}
			}

			return (target - rate) * sensitivity;
		}

		private static float BrakeAcceleration(InterceptorFlightProfile profile, float rate, float deltaTime)
		{
			float absRate = Mathf.Abs(rate);
			if (profile.AngularBrakeAccel <= 0f || absRate <= profile.AngularBrakeStopThreshold)
			{
				return 0f;
			}

			float maxWithoutOvershoot = absRate / Mathf.Max(deltaTime, 0.0001f);
			return -Mathf.Sign(rate) * Mathf.Min(profile.AngularBrakeAccel, maxWithoutOvershoot);
		}
	}
}
