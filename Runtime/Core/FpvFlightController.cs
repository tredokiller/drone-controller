using System;
using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Environment;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.Rc;
using UnityEngine;

namespace UForce.FpvFlight
{
	// Puts the flight core on a Rigidbody. The project calls Tick once per FixedUpdate with the latest radio command.
	[RequireComponent(typeof(Rigidbody))]
	[DisallowMultipleComponent]
	public sealed class FpvFlightController : MonoBehaviour
	{
		// Rigidbody.maxAngularVelocity must stay above the highest commanded rate (1998 deg/s ≈ 34.9 rad/s).
		private const float MIN_MAX_ANGULAR_VELOCITY = 40f;

		[SerializeField] private AirframeDefinition _airframe;
		[SerializeField] private BatteryDefinition _battery;
		[SerializeField] private RatesProfile _rates;
		[SerializeField] private FlightControllerProfile _profile;
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
		public FpvFlightSimulation Simulation => _simulation;
		public Rigidbody Body => _rigidbody;

		private void Awake()
		{
			Initialize();
		}

		public void Configure(AirframeDefinition airframe, BatteryDefinition battery, RatesProfile rates, FlightControllerProfile profile)
		{
			_airframe = airframe;
			_battery = battery;
			_rates = rates;
			_profile = profile;
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
			ApplyRigidbodySetup();
		}

		public void Tick(in RcCommand command, float deltaTime)
		{
			Initialize();
			if (_simulation == null || _rigidbody.isKinematic)
			{
				return;
			}

			_time += deltaTime;
			FlightBodyState body = new(_rigidbody.position, _rigidbody.rotation, _rigidbody.linearVelocity, _rigidbody.angularVelocity);
			WindSample wind = _windProvider != null ? _windProvider.Sample(body.Position, _time) : WindSample.Calm;
			FlightStepResult result = _simulation.Step(command, body, wind, Physics.gravity.magnitude, deltaTime);

			_rigidbody.AddForce(result.ThrustForce + result.DragForce, ForceMode.Force);
			_rigidbody.AddTorque(result.AngularAcceleration, ForceMode.Acceleration);

			RaiseEvents();
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

		private void ApplyRigidbodySetup()
		{
			if (!_isRigidbodyConfigured || !_rigidbody || !_airframe)
			{
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
