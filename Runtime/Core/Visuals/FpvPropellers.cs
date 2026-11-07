using UnityEngine;

namespace UForce.FpvFlight.Visuals
{
	// Spins the propeller meshes from the motor output, in place of Interceptor's MGAssets Spinning script.
	// Speed goes from the idle factor to full with motor output (Interceptor P1-SUN: 0.15 to 1 of 3600 deg/s) and spins
	// down after disarm. Optional counter-rotation for the second and fourth propeller.
	[DisallowMultipleComponent]
	public sealed class FpvPropellers : MonoBehaviour
	{
		[Tooltip("Must implement IFpvMotorSource. Empty: the FpvFlightController in the parents.")]
		[SerializeField] private MonoBehaviour _motorSourceBehaviour;
		[SerializeField] private Transform[] _propellers;
		[Tooltip("Local spin axis of every propeller pivot.")]
		[SerializeField] private Vector3 _spinAxis = Vector3.up;
		[Tooltip("Full-speed rotation, degrees per second.")]
		[SerializeField, Min(0f)] private float _maxSpeed = 3600f;
		[SerializeField, Range(0f, 1f)] private float _idleFactor = 0.15f;
		[SerializeField] private bool _isCounterRotating = true;
		[Tooltip("Seconds to stop after disarm.")]
		[SerializeField, Min(0.01f)] private float _spinDownTime = 0.6f;

		private IFpvMotorSource _motorSource;
		private float _factor;

		public float SpinFactor => _factor;

		private void Awake()
		{
			_motorSource = _motorSourceBehaviour as IFpvMotorSource ?? GetComponentInParent<FpvFlightController>();
		}

		public void SetMotorSource(IFpvMotorSource motorSource) => _motorSource = motorSource;

		private void Update()
		{
			if (_motorSource == null || _propellers == null)
			{
				return;
			}

			if (_motorSource.IsArmed)
			{
				_factor = Mathf.Lerp(_idleFactor, 1f, Mathf.Clamp01(_motorSource.MotorOutput));
			}
			else
			{
				_factor = Mathf.MoveTowards(_factor, 0f, Time.deltaTime / _spinDownTime);
			}

			if (_factor <= 0f)
			{
				return;
			}

			float step = _factor * _maxSpeed * Time.deltaTime;
			for (int index = 0; index < _propellers.Length; index++)
			{
				Transform propeller = _propellers[index];
				if (!propeller)
				{
					continue;
				}

				float direction = _isCounterRotating && index % 2 == 1 ? -1f : 1f;
				propeller.Rotate(_spinAxis, step * direction, Space.Self);
			}
		}
	}
}
