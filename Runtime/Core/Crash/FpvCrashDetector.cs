using System;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight.Crash
{
	// Crash detection with a landing tolerance, ported from Interceptor Drones (TriggerManualDrone, DroneDeactivationBehaviour):
	// a hit faster than 1 m/s on the crash layers is a crash, unless it is a landing (a contact normal within ~41° of up
	// and at most 10 m/s). On a crash the drone disarms, arming is inhibited until Recover, and an optional stop-motion
	// settle ramps the damping up over 0.25 s so it does not bounce away.
	[RequireComponent(typeof(FpvFlightController))]
	[DisallowMultipleComponent]
	public sealed class FpvCrashDetector : MonoBehaviour
	{
		public const float SOFT_CONTACT_SPEED = 1f;
		public const float HARD_IMPACT_SPEED = 10f;
		public const float MIN_LANDING_NORMAL_DOT = 0.75f;

		[SerializeField] private LayerMask _crashLayers = ~0;
		[Tooltip("Disarm, inhibit arming and settle the body on a crash. Off: only the Crashed event.")]
		[SerializeField] private bool _isHandlingCrash = true;
		[SerializeField, Min(0f)] private float _settleDuration = 0.25f;
		[SerializeField, Min(0f)] private float _settleAngularDamping = 8f;
		[SerializeField, Min(0f)] private float _settleLinearDamping = 3f;

		private FpvFlightController _controller;
		private float _settleTime = -1f;
		private bool _wasRecovering;

		public event Action<Collision, float> Crashed;

		public bool IsCrashed { get; private set; }

		private void Awake()
		{
			_controller = GetComponent<FpvFlightController>();
		}

		// Interceptor's rule. Pass the largest dot(normal, up) of the contacts.
		public static bool IsCrash(float impactSpeed, float bestNormalUpDot)
		{
			if (impactSpeed <= SOFT_CONTACT_SPEED)
			{
				return false;
			}

			bool isLanding = bestNormalUpDot >= MIN_LANDING_NORMAL_DOT && impactSpeed <= HARD_IMPACT_SPEED;
			return !isLanding;
		}

		// After Recover, or to rearm the detector by hand.
		public void Clear()
		{
			IsCrashed = false;
			_settleTime = -1f;
			_controller.SetArmingInhibit(ArmingInhibit.Crashed, false);
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (!enabled || IsCrashed || (_crashLayers.value & (1 << collision.gameObject.layer)) == 0)
			{
				return;
			}

			float speed = collision.relativeVelocity.magnitude;
			float bestDot = -1f;
			int contactCount = collision.contactCount;
			for (int index = 0; index < contactCount; index++)
			{
				bestDot = Mathf.Max(bestDot, Vector3.Dot(collision.GetContact(index).normal, Vector3.up));
			}

			if (!IsCrash(speed, bestDot))
			{
				return;
			}

			IsCrashed = true;
			if (_isHandlingCrash)
			{
				_controller.ForceDisarm();
				_controller.SetArmingInhibit(ArmingInhibit.Crashed, true);
				_settleTime = 0f;
			}

			Crashed?.Invoke(collision, speed);
		}

		private void FixedUpdate()
		{
			// FpvFlightController.Recover releases the drone on its feet: the detector re-arms at that moment.
			bool isRecovering = _controller.IsRecovering;
			if (_wasRecovering && !isRecovering && IsCrashed)
			{
				Clear();
			}

			_wasRecovering = isRecovering;
			if (isRecovering)
			{
				_settleTime = -1f;
				return;
			}

			if (_settleTime < 0f || _settleTime > _settleDuration)
			{
				return;
			}

			Rigidbody body = _controller.Body;
			_settleTime += Time.fixedDeltaTime;
			float t = _settleDuration > 0f ? Mathf.Clamp01(_settleTime / _settleDuration) : 1f;
			body.angularDamping = Mathf.Lerp(0f, _settleAngularDamping, t);
			body.linearDamping = Mathf.Lerp(0f, _settleLinearDamping, t);
			body.angularVelocity *= 0.92f;
			body.linearVelocity *= 0.96f;

			if (t >= 1f)
			{
				body.angularVelocity *= 0.4f;
				body.linearVelocity *= 0.6f;
				_settleTime = _settleDuration + 1f;
			}
		}
	}
}
