using UForce.FpvFlight.Video;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Default OSD data: the flight core's telemetry (arming, mode, failsafe, battery, link), the video signal,
	// and speed, altitude and heading from the drone body. Put it on the video camera or anywhere under the drone.
	public sealed class FpvOsdFlightSource : MonoBehaviour, IFpvOsdDataSource
	{
		private const float MAX_GROUND_DISTANCE = 2000f;

		[Tooltip("Empty: the FpvFlightController in the parents.")]
		[SerializeField] private FpvFlightController _flight;
		[Tooltip("Empty: the FpvVideoCamera on this object or in the children.")]
		[SerializeField] private FpvVideoCamera _video;
		[SerializeField] private FpvOsdAltitudeMode _altitudeMode = FpvOsdAltitudeMode.AboveGround;
		[SerializeField] private LayerMask _groundMask = ~0;

		private Transform _body;
		private FpvBodyVelocityTracker _velocityTracker;
		private float _flightTime;
		private bool _hasPayload;
		private bool _isPayloadArmed;

		private void Awake()
		{
			if (_flight == null)
			{
				_flight = GetComponentInParent<FpvFlightController>();
			}

			if (_video == null)
			{
				_video = GetComponentInChildren<FpvVideoCamera>();
			}

			_body = _flight != null ? _flight.transform : transform;
			_velocityTracker = new FpvBodyVelocityTracker(_body);
		}

		private void Update()
		{
			_velocityTracker.Tick(Time.deltaTime);
			if (_flight != null && _flight.IsArmed)
			{
				_flightTime += Time.deltaTime;
			}
		}

		public void SetPayload(bool hasPayload, bool isArmed)
		{
			_hasPayload = hasPayload;
			_isPayloadArmed = isArmed;
		}

		public void ResetFlightTime()
		{
			_flightTime = 0f;
		}

		public bool TryGetData(out FpvOsdData data)
		{
			data = default;
			if (_flight == null || !_flight.IsInitialized)
			{
				return false;
			}

			FlightTelemetry telemetry = _flight.Telemetry;
			Vector3 velocity = _velocityTracker.Velocity;
			Vector3 forward = Vector3.ProjectOnPlane(_body.forward, Vector3.up);

			data.IsArmed = telemetry.IsArmed;
			data.Mode = telemetry.Mode;
			data.FailsafeStage = telemetry.FailsafeStage;
			data.ArmingBlockReason = telemetry.ArmingBlockReason;
			data.CellVoltage = telemetry.BatteryCellVoltage;
			data.PackVoltage = telemetry.BatteryVoltage;
			data.Current = telemetry.BatteryCurrent;
			data.UsedMah = telemetry.BatteryUsedMah;
			data.BatteryCharge = telemetry.BatteryStateOfCharge;
			data.LinkQuality = telemetry.IsLinkHealthy ? telemetry.LinkQuality : 0f;
			data.VideoSignal = _video != null ? 1f - _video.SignalLoss : 1f;
			data.Altitude = GetAltitude();
			data.SpeedKmh = velocity.magnitude * 3.6f;
			data.VerticalSpeed = velocity.y;
			data.Heading = forward.sqrMagnitude > 1e-6f ? Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f) : 0f;
			data.Throttle = telemetry.Throttle;
			data.FlightTime = _flightTime;
			data.Pitch = Mathf.Asin(Mathf.Clamp(_body.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
			data.Roll = Mathf.Atan2(-_body.right.y, _body.up.y) * Mathf.Rad2Deg;
			data.HasPayload = _hasPayload;
			data.IsPayloadArmed = _isPayloadArmed;
			return true;
		}

		private float GetAltitude()
		{
			Vector3 position = _body.position;
			if (_altitudeMode == FpvOsdAltitudeMode.WorldHeight)
			{
				return position.y;
			}

			return Physics.Raycast(position, Vector3.down, out RaycastHit hit, MAX_GROUND_DISTANCE, _groundMask, QueryTriggerInteraction.Ignore)
				? hit.distance
				: position.y;
		}
	}
}
