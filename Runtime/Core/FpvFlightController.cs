using System;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Environment;
using UForce.FpvFlight.Interceptor;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight
{
	// Puts the flight core on a Rigidbody. The project calls Tick once per FixedUpdate with the latest radio command.
	[RequireComponent(typeof(Rigidbody))]
	[DisallowMultipleComponent]
	public sealed class FpvFlightController : MonoBehaviour, IFpvMotorSource
	{
		// Rigidbody.maxAngularVelocity must stay above the highest commanded rate (1998 deg/s ≈ 34.9 rad/s).
		private const float MIN_MAX_ANGULAR_VELOCITY = 40f;
		// Interceptor Drones' project default (DynamicsManager max angular speed).
		private const float INTERCEPTOR_MAX_ANGULAR_VELOCITY = 50f;
		// Recover: as Interceptor Drones' RestartTransform.
		private const float RECOVER_GROUND_CLEARANCE = 0.02f;
		private const float RECOVER_PROBE_HEIGHT = 2f;
		private const float RECOVER_PROBE_DISTANCE = 10f;
		private const float RECOVER_MIN_HOLD = 1f;

		[SerializeField] private AirframeDefinition _airframe;
		[SerializeField] private BatteryDefinition _battery;
		[SerializeField] private RatesProfile _rates;
		[SerializeField] private FlightControllerProfile _profile;
		[Tooltip("Optional. Flies with Interceptor Drones' flight model and this preset instead of the Betaflight physics.")]
		[SerializeField] private InterceptorFlightProfile _interceptorFlight;
		[Tooltip("Warhead or other payload carried on top of the frame and battery, kg.")]
		[SerializeField, Min(0f)] private float _payloadMass;
		[SerializeField, Range(0f, 1f)] private float _initialStateOfCharge = 1f;
		[Tooltip("Optional. Must implement IWindProvider. Calm air when empty.")]
		[SerializeField] private MonoBehaviour _windProviderBehaviour;
		[Tooltip("Sets mass, inertia, center of mass, damping and max angular velocity on the Rigidbody.")]
		[SerializeField] private bool _isRigidbodyConfigured = true;
		[SerializeField] private uint _randomSeed = 1u;

		private Rigidbody _rigidbody;
		private FpvFlightSimulation _simulation;
		private IWindProvider _windProvider;
		private float _time;
		private bool _wasArmed;
		private FailsafeStage _previousFailsafeStage;
		private bool _isRecovering;
		private float _recoverTime;
		private readonly RaycastHit[] r_recoverHits = new RaycastHit[8];
		private readonly Collider[] r_groundHits = new Collider[8];

		public event Action OnArmed;
		public event Action OnDisarmed;
		public event Action<FailsafeStage> OnFailsafeStageChanged;

		public bool IsInitialized => _simulation != null;
		public FlightTelemetry Telemetry => _simulation != null ? _simulation.Telemetry : default;
		public bool IsArmed => _simulation != null && _simulation.IsArmed;
		public float MotorOutput => _simulation != null ? _simulation.MotorOutput : 0f;
		public float PayloadMass => _payloadMass;
		public float TotalMass => _simulation != null ? _simulation.TotalMass : AirframeMath.TotalMass(_airframe, _battery, _payloadMass);
		public AirframeDefinition Airframe => _airframe;
		public BatteryDefinition Battery => _battery;
		public RatesProfile Rates => _rates;
		public FlightControllerProfile Profile => _profile;
		public InterceptorFlightProfile InterceptorFlight => _interceptorFlight;
		public FpvFlightSimulation Simulation => _simulation;
		public Rigidbody Body => _rigidbody;
		public Vector3 Velocity => _rigidbody ? _rigidbody.linearVelocity : Vector3.zero;
		// True from Recover until the drone is released: 1 s on the ground, then throttle at minimum.
		public bool IsRecovering => _isRecovering;

		private void Awake()
		{
			Initialize();
		}

		public void Configure(AirframeDefinition airframe, BatteryDefinition battery, RatesProfile rates, FlightControllerProfile profile)
		{
			Configure(airframe, battery, rates, profile, _interceptorFlight);
		}

		// interceptorFlight: null for the Betaflight physics, a preset for Interceptor Drones' flight model.
		public void Configure(AirframeDefinition airframe, BatteryDefinition battery, RatesProfile rates, FlightControllerProfile profile, InterceptorFlightProfile interceptorFlight)
		{
			_airframe = airframe;
			_battery = battery;
			_rates = rates;
			_profile = profile;
			_interceptorFlight = interceptorFlight;
			_simulation = null;
			Initialize();
		}

		public void Initialize()
		{
			if (_simulation != null)
			{
				return;
			}

			if (!_airframe || !_rates || !_profile)
			{
				Debug.LogError($"[{nameof(FpvFlightController)}] Airframe, rates and flight controller profile must be assigned.", this);
				return;
			}

			_rigidbody = GetComponent<Rigidbody>();
			_windProvider ??= _windProviderBehaviour as IWindProvider;
			if (_windProviderBehaviour && _windProvider == null)
			{
				Debug.LogError($"[{nameof(FpvFlightController)}] {_windProviderBehaviour.name} does not implement {nameof(IWindProvider)}.", this);
			}

			_simulation = new FpvFlightSimulation(_airframe, _battery, _rates, _profile, _randomSeed);
			_simulation.SetPayloadMass(_payloadMass);
			_simulation.SetBattery(_battery, _initialStateOfCharge);
			_simulation.SetInterceptorFlight(_interceptorFlight);
			ApplyRigidbodySetup();
		}

		public void Tick(in RcCommand command, float deltaTime)
		{
			Initialize();
			if (_simulation == null)
			{
				return;
			}

			if (_isRecovering)
			{
				TickRecovery(command, deltaTime);
				return;
			}

			if (_rigidbody.isKinematic)
			{
				return;
			}

			_time += deltaTime;
			bool isGrounded = _interceptorFlight && IsGrounded(_interceptorFlight.GroundHeight);
			FlightBodyState body = new(_rigidbody.position, _rigidbody.rotation, _rigidbody.linearVelocity, _rigidbody.angularVelocity, isGrounded);
			WindSample wind = _windProvider != null && !_interceptorFlight ? _windProvider.Sample(body.Position, _time) : WindSample.Calm;
			FlightStepResult result = _simulation.Step(command, body, wind, Physics.gravity.magnitude, deltaTime);

			_rigidbody.AddForce(result.ThrustForce + result.DragForce, ForceMode.Force);
			_rigidbody.AddTorque(result.AngularAcceleration, ForceMode.Acceleration);
			if (result.Acceleration != Vector3.zero)
			{
				_rigidbody.AddForce(result.Acceleration, ForceMode.Acceleration);
			}

			if (result.HasLimits)
			{
				ApplyLimits(result.MaxSpeed, result.MaxBodyRates);
			}

			RaiseEvents();
		}

		// As Interceptor Drones' ApplyAngularSpeedLimit and ApplyLinearSpeedLimit: body rates clamped per axis, speed clamped.
		private void ApplyLimits(float maxSpeed, Vector3 maxBodyRates)
		{
			Quaternion rotation = _rigidbody.rotation;
			Vector3 right = rotation * Vector3.right;
			Vector3 up = rotation * Vector3.up;
			Vector3 forward = rotation * Vector3.forward;
			Vector3 angular = _rigidbody.angularVelocity;
			float pitch = Mathf.Clamp(Vector3.Dot(angular, right), -maxBodyRates.x, maxBodyRates.x);
			float yaw = Mathf.Clamp(Vector3.Dot(angular, up), -maxBodyRates.y, maxBodyRates.y);
			float roll = Mathf.Clamp(Vector3.Dot(angular, forward), -maxBodyRates.z, maxBodyRates.z);
			_rigidbody.angularVelocity = right * pitch + up * yaw + forward * roll;

			Vector3 velocity = _rigidbody.linearVelocity;
			float speed = velocity.magnitude;
			if (speed > maxSpeed)
			{
				_rigidbody.linearVelocity = velocity * (maxSpeed / speed);
			}
		}

		// Interceptor Drones' CheckGrounded: anything but the drone itself within the ground radius.
		private bool IsGrounded(float radius)
		{
			int count = Physics.OverlapSphereNonAlloc(_rigidbody.position, radius, r_groundHits, ~0, QueryTriggerInteraction.Ignore);
			for (int index = 0; index < count; index++)
			{
				if (!r_groundHits[index].transform.IsChildOf(transform))
				{
					return true;
				}
			}

			return false;
		}

		public void SetInterceptorFlight(InterceptorFlightProfile interceptorFlight)
		{
			_interceptorFlight = interceptorFlight;
			Initialize();
			_simulation?.SetInterceptorFlight(interceptorFlight);
			ApplyRigidbodySetup();
		}

		public void SetLinkModel(IRcLinkModel linkModel)
		{
			Initialize();
			_simulation?.SetLinkModel(linkModel);
		}

		public void SetWindProvider(IWindProvider windProvider)
		{
			_windProvider = windProvider;
		}

		public void SetPayloadMass(float payloadMass)
		{
			_payloadMass = Mathf.Max(0f, payloadMass);
			Initialize();
			_simulation?.SetPayloadMass(_payloadMass);
			ApplyRigidbodySetup();
		}

		public void SetBattery(BatteryDefinition battery, float stateOfCharge)
		{
			_battery = battery;
			_initialStateOfCharge = Mathf.Clamp01(stateOfCharge);
			Initialize();
			_simulation?.SetBattery(battery, _initialStateOfCharge);
			ApplyRigidbodySetup();
		}

		public void SetArmingBlocked(bool isBlocked)
		{
			Initialize();
			_simulation?.SetArmingBlocked(isBlocked);
		}

		// Payload armed, crashed or another project-side reason; disarmed drones cannot arm while any is set.
		public void SetArmingInhibit(ArmingInhibit inhibit, bool isOn)
		{
			Initialize();
			_simulation?.SetArmingInhibit(inhibit, isOn);
		}

		public void ForceDisarm()
		{
			_simulation?.ForceDisarm();
			RaiseEvents();
		}

		public void ResetState()
		{
			_simulation?.ResetState();
			_wasArmed = false;
			_previousFailsafeStage = FailsafeStage.None;
		}

		// Puts the drone back on the surface below the given pose, level, keeping the yaw, as Interceptor Drones' respawn does.
		// It stays frozen for 1 s and then until throttle is at minimum, so it cannot leap off with the stick up.
		// Clears the Crashed inhibit when released. The arm switch must still be cycled.
		public void Recover(Vector3 position, Quaternion rotation)
		{
			Initialize();
			if (_simulation == null)
			{
				return;
			}

			_simulation.ForceDisarm();
			_simulation.ResetState();
			RaiseEvents();

			float yaw = rotation.eulerAngles.y;
			Quaternion level = Quaternion.Euler(0f, yaw, 0f);
			_rigidbody.isKinematic = true;
			_rigidbody.position = PlaceOnSurface(position, level);
			_rigidbody.rotation = level;
			transform.SetPositionAndRotation(_rigidbody.position, level);
			ApplyRigidbodySetup();

			_isRecovering = true;
			_recoverTime = 0f;
		}

		// While held, the core still runs on the real command (telemetry, link, battery), with arming inhibited, so an arm
		// switch that is on at release must be cycled first, as on the radio.
		private void TickRecovery(in RcCommand command, float deltaTime)
		{
			_recoverTime += deltaTime;
			_simulation.SetArmingInhibit(ArmingInhibit.Recovering, true);
			FlightBodyState body = new(_rigidbody.position, _rigidbody.rotation, Vector3.zero, Vector3.zero, true);
			_simulation.Step(command, body, WindSample.Calm, Physics.gravity.magnitude, deltaTime);

			if (_recoverTime < RECOVER_MIN_HOLD || command.Throttle > _profile.ArmThrottleMax)
			{
				return;
			}

			_isRecovering = false;
			_rigidbody.isKinematic = false;
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
			_simulation.SetArmingInhibit(ArmingInhibit.Recovering | ArmingInhibit.Crashed, false);
		}

		private Vector3 PlaceOnSurface(Vector3 position, Quaternion rotation)
		{
			Vector3 origin = position + Vector3.up * RECOVER_PROBE_HEIGHT;
			int count = Physics.RaycastNonAlloc(origin, Vector3.down, r_recoverHits, RECOVER_PROBE_HEIGHT + RECOVER_PROBE_DISTANCE, ~0, QueryTriggerInteraction.Ignore);
			float nearest = float.PositiveInfinity;
			Vector3 surface = position;
			for (int index = 0; index < count; index++)
			{
				RaycastHit hit = r_recoverHits[index];
				if (hit.rigidbody == _rigidbody || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
				{
					continue;
				}

				nearest = hit.distance;
				surface = hit.point;
			}

			if (float.IsPositiveInfinity(nearest))
			{
				return position;
			}

			// Lift so the bottom of the drone's colliders sits just above the surface.
			float bottomOffset = 0f;
			bool hasCollider = false;
			transform.SetPositionAndRotation(surface, rotation);
			Physics.SyncTransforms();
			foreach (Collider part in GetComponentsInChildren<Collider>())
			{
				if (part.isTrigger || !part.enabled)
				{
					continue;
				}

				float bottom = part.bounds.min.y - surface.y;
				bottomOffset = hasCollider ? Mathf.Min(bottomOffset, bottom) : bottom;
				hasCollider = true;
			}

			return surface + Vector3.up * (RECOVER_GROUND_CLEARANCE - (hasCollider ? bottomOffset : 0f));
		}

		private void ApplyRigidbodySetup()
		{
			if (!_isRigidbodyConfigured || !_rigidbody || !_airframe)
			{
				return;
			}

			if (_interceptorFlight)
			{
				// As the Interceptor Drones prefabs: Unity's automatic centre of mass and inertia, the preset's damping.
				_rigidbody.mass = _interceptorFlight.BaseMassKg + (_battery ? _battery.Mass : 0f) + _payloadMass;
				_rigidbody.linearDamping = _interceptorFlight.LinearDamping;
				_rigidbody.angularDamping = _interceptorFlight.AngularDamping;
				_rigidbody.automaticCenterOfMass = true;
				_rigidbody.automaticInertiaTensor = true;
				_rigidbody.maxAngularVelocity = INTERCEPTOR_MAX_ANGULAR_VELOCITY;
				return;
			}

			float mass = AirframeMath.TotalMass(_airframe, _battery, _payloadMass);
			_rigidbody.mass = mass;
			_rigidbody.linearDamping = 0f;
			_rigidbody.angularDamping = 0f;
			_rigidbody.automaticCenterOfMass = false;
			_rigidbody.centerOfMass = _airframe.CenterOfMass;
			_rigidbody.automaticInertiaTensor = false;
			_rigidbody.inertiaTensor = AirframeMath.BoxInertia(mass, _airframe.BodySize);
			_rigidbody.inertiaTensorRotation = Quaternion.identity;
			float rateLimit = _rates ? _rates.RateLimit * Mathf.Deg2Rad * 1.2f : 0f;
			_rigidbody.maxAngularVelocity = Mathf.Max(MIN_MAX_ANGULAR_VELOCITY, rateLimit);
		}

		private void RaiseEvents()
		{
			if (_simulation == null)
			{
				return;
			}

			bool isArmed = _simulation.IsArmed;
			if (isArmed != _wasArmed)
			{
				_wasArmed = isArmed;
				if (isArmed)
				{
					OnArmed?.Invoke();
				}
				else
				{
					OnDisarmed?.Invoke();
				}
			}

			FailsafeStage stage = _simulation.FailsafeStage;
			if (stage != _previousFailsafeStage)
			{
				_previousFailsafeStage = stage;
				OnFailsafeStageChanged?.Invoke(stage);
			}
		}
	}
}
