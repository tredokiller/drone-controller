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
	// The flight core without a Rigidbody: radio link, failsafe, arming, rates, rate and angle loops, motor, battery and drag.
	// FpvFlightController applies the result to a Rigidbody; tests can drive it directly.
	public sealed class FpvFlightSimulation
	{
		// The I-term only integrates above this throttle, so it does not wind up while sitting on the ground.
		private const float INTEGRAL_MIN_THROTTLE = 0.05f;
		// Airspeed at which turbulence doubles, m/s.
		private const float TURBULENCE_REFERENCE_SPEED = 20f;
		// As in Interceptor Drones: at 1% charge or less the drone can no longer be armed.
		private const float ARMING_MIN_STATE_OF_CHARGE = 0.01f;

		private readonly RcLink r_link;
		private readonly Failsafe r_failsafe = new();
		private readonly ArmingLogic r_arming = new();
		private readonly MotorModel r_motor = new();
		private readonly BatteryModel r_battery = new();
		private readonly RateController r_rateController = new();
		private readonly InterceptorFlightModel r_interceptorModel = new();
		private readonly uint r_seed;

		private AirframeDefinition _airframe;
		private BatteryDefinition _battery;
		private RatesProfile _rates;
		private FlightControllerProfile _profile;
		private IRcLinkModel _linkModel;
		private DeterministicRandom _random;
		private FlightTelemetry _telemetry;
		private float _payloadMass;
		private ArmingInhibit _armingInhibits;
		private InterceptorFlightProfile _interceptor;

		public FpvFlightSimulation(AirframeDefinition airframe, BatteryDefinition battery, RatesProfile rates, FlightControllerProfile profile, uint seed = 1u)
		{
			_airframe = airframe ? airframe : throw new ArgumentNullException(nameof(airframe));
			_rates = rates ? rates : throw new ArgumentNullException(nameof(rates));
			_profile = profile ? profile : throw new ArgumentNullException(nameof(profile));
			_battery = battery;
			r_seed = seed;
			r_link = new RcLink(seed);
			_random = new DeterministicRandom(seed ^ 0x5bd1e995u);
			r_battery.Reset(battery, 1f);
		}

		public ref readonly FlightTelemetry Telemetry => ref _telemetry;
		public bool IsArmed => r_arming.IsArmed;
		public float MotorOutput => r_motor.Output;
		public FailsafeStage FailsafeStage => r_failsafe.Stage;
		public float PayloadMass => _payloadMass;
		public float TotalMass => _interceptor
			? _interceptor.BaseMassKg + (_battery ? _battery.Mass : 0f) + _payloadMass
			: AirframeMath.TotalMass(_airframe, _battery, _payloadMass);
		// Set: the drone flies with Interceptor Drones' flight model instead of the Betaflight physics.
		public InterceptorFlightProfile InterceptorFlight => _interceptor;
		public AirframeDefinition Airframe => _airframe;
		public BatteryDefinition Battery => _battery;
		public RatesProfile Rates => _rates;
		public FlightControllerProfile Profile => _profile;
		public BatteryModel BatteryState => r_battery;
		public RcLink Link => r_link;
		public ArmingInhibit ArmingInhibits => _armingInhibits;

		// Null means an ideal link.
		public void SetLinkModel(IRcLinkModel linkModel) => _linkModel = linkModel;

		public void SetPayloadMass(float payloadMass) => _payloadMass = Mathf.Max(0f, payloadMass);

		public void SetBattery(BatteryDefinition battery, float stateOfCharge)
		{
			_battery = battery;
			r_battery.Reset(battery, stateOfCharge);
		}

		// Null: the Betaflight physics. A profile: Interceptor Drones' flight model with that profile's preset.
		public void SetInterceptorFlight(InterceptorFlightProfile interceptor)
		{
			_interceptor = interceptor;
			r_interceptorModel.Reset();
		}

		public void SetRates(RatesProfile rates)
		{
			if (rates)
			{
				_rates = rates;
			}
		}

		// Blocks arming from the project side (for example while a drone is on the launch station).
		public void SetArmingBlocked(bool isBlocked) => SetArmingInhibit(ArmingInhibit.External, isBlocked);

		// Sets or clears project-side arming blocks: payload armed, crashed, or any other reason (External).
		public void SetArmingInhibit(ArmingInhibit inhibit, bool isOn)
		{
			_armingInhibits = isOn ? _armingInhibits | inhibit : _armingInhibits & ~inhibit;
		}

		public void ForceDisarm()
		{
			r_arming.ForceDisarm();
			r_rateController.Reset();
			r_interceptorModel.Reset();
		}

		// Clears link, failsafe, arming, motor and loop state. The battery keeps its charge.
		public void ResetState()
		{
			r_link.Reset();
			r_failsafe.Reset();
			r_arming.Reset();
			r_motor.Reset();
			r_rateController.Reset();
			r_interceptorModel.Reset();
			_random = new DeterministicRandom(r_seed ^ 0x5bd1e995u);
			_telemetry = default;
		}

		public FlightStepResult Step(in RcCommand command, in FlightBodyState body, in WindSample wind, float gravity, float deltaTime)
		{
			if (deltaTime <= 0f)
			{
				return default;
			}

			FailsafeSettings failsafeSettings = _profile.Failsafe;
			RcLinkState linkState = _linkModel != null ? _linkModel.Sample() : RcLinkState.Ideal;
			r_link.Tick(command, linkState, deltaTime, _profile.LatencySlewRate);
			bool isLinkHealthy = r_link.HasFrame && r_link.TimeSinceLastFrame < failsafeSettings.RxLossTimeout;

			RcCommand control = r_failsafe.Process(r_link.Current, r_link.HasFrame, r_link.TimeSinceLastFrame, r_arming.IsArmed, failsafeSettings, out bool isFailsafeDrop);
			if (isFailsafeDrop)
			{
				r_arming.ForceDisarm();
			}

			r_arming.Update(control.ArmSwitch, control.Throttle, _profile.ArmThrottleMax, isLinkHealthy, CollectArmingBlocks());
			bool isArmed = r_arming.IsArmed;
			if (!isArmed)
			{
				r_rateController.Reset();
			}

			if (_interceptor)
			{
				return StepInterceptor(control, isArmed, isLinkHealthy, linkState, body, gravity, deltaTime);
			}

			float throttle = _profile.ApplyThrottleCurve(control.Throttle);
			float motorCommand = isArmed ? ThrustModel.MotorCommand(throttle, _airframe.MotorIdle) : 0f;
			r_motor.Update(motorCommand, deltaTime, _airframe.MotorTimeConstant);

			float referenceThrust = _airframe.MaxThrust * ThrustModel.ThrustFraction(r_motor.Output, _airframe.ThrustLinearity);
			float thrust = r_battery.Update(_battery, _airframe, referenceThrust, deltaTime);
			float thrustScale = _battery ? r_battery.ThrustScale : 1f;

			Quaternion rotation = body.Rotation;
			Quaternion inverse = Quaternion.Inverse(rotation);
			Vector3 bodyRate = inverse * body.AngularVelocity;
			Vector3 targetRate = ComputeTargetRate(control, rotation);

			Vector3 angularAcceleration = -bodyRate * _airframe.AngularDrag;
			if (isArmed)
			{
				RateControllerSettings settings = CreateRateSettings(thrustScale);
				bool isIntegrating = control.Throttle > INTEGRAL_MIN_THROTTLE;
				angularAcceleration += r_rateController.Compute(targetRate, bodyRate, settings, isIntegrating, deltaTime);
			}

			Vector3 airVelocity = body.Velocity - wind.Velocity;
			float airspeed = airVelocity.magnitude;
			if (wind.Turbulence > 0f)
			{
				float magnitude = wind.Turbulence * _profile.TurbulenceAngularAcceleration * Mathf.Deg2Rad * (1f + airspeed / TURBULENCE_REFERENCE_SPEED);
				angularAcceleration += new Vector3(_random.NextSigned(), 0.5f * _random.NextSigned(), _random.NextSigned()) * magnitude;
			}

			Vector3 bodyDrag = DragModel.BodyDrag(inverse * airVelocity, _airframe.DragArea);
			Vector3 dragForce = rotation * bodyDrag;
			Vector3 thrustForce = rotation * new Vector3(0f, thrust, 0f);

			UpdateTelemetry(control, isLinkHealthy, linkState, throttle, thrust, thrustScale, gravity, targetRate, bodyRate, rotation, body.Velocity, airspeed);

			return new FlightStepResult(thrustForce, dragForce, rotation * angularAcceleration);
		}

		// Interceptor Drones' Manual mode (DroneMovementController): linear stick rates with an almost instant response,
		// thrust as a multiple of the weight with no motor lag and no aerodynamic drag, thrust assist past 15° of tilt, extra
		// gravity at zero throttle, hard speed and rate limits. Disarmed, nothing acts but gravity and Rigidbody damping.
		private FlightStepResult StepInterceptor(in RcCommand control, bool isArmed, bool isLinkHealthy, in RcLinkState linkState, in FlightBodyState body,
			float gravity, float deltaTime)
		{
			InterceptorFlightProfile interceptor = _interceptor;
			float throttle = Mathf.Clamp01(control.Throttle);
			r_motor.Update(isArmed ? throttle : 0f, deltaTime, 0f);

			float mass = TotalMass;
			float thrust = isArmed ? InterceptorFlightModel.Thrust(interceptor, throttle, mass, gravity) : 0f;
			r_battery.Update(_battery, _airframe, thrust, deltaTime);

			Quaternion rotation = body.Rotation;
			Vector3 bodyRate = Quaternion.Inverse(rotation) * body.AngularVelocity;
			float yawStick = body.IsGrounded ? 0f : control.Yaw;
			var rateInput = new Vector3(control.Pitch, yawStick, control.Roll);
			Vector3 targetRate = ComputeInterceptorTargetRate(control, yawStick, rotation, interceptor);

			if (!isArmed)
			{
				r_interceptorModel.Reset();
				UpdateTelemetry(control, isLinkHealthy, linkState, throttle, 0f, 1f, gravity, targetRate, bodyRate, rotation, body.Velocity, body.Velocity.magnitude);
				UpdateInterceptorTelemetry(interceptor);
				return new FlightStepResult(Vector3.zero, Vector3.zero, Vector3.zero);
			}

			Vector3 angularAcceleration = r_interceptorModel.AngularAcceleration(interceptor, rateInput, targetRate, bodyRate, deltaTime);
			Vector3 thrustForce = rotation * new Vector3(0f, thrust, 0f);
			Vector3 assist = InterceptorFlightModel.ThrustAssist(interceptor, rotation, mass, gravity);
			Vector3 extraGravity = InterceptorFlightModel.ZeroThrottleGravity(interceptor, throttle, body.IsGrounded, gravity);

			UpdateTelemetry(control, isLinkHealthy, linkState, throttle, thrust, 1f, gravity, targetRate, bodyRate, rotation, body.Velocity, body.Velocity.magnitude);
			UpdateInterceptorTelemetry(interceptor);
			return new FlightStepResult(thrustForce, assist, rotation * angularAcceleration, extraGravity, interceptor.MaxSpeed, interceptor.MaxRates * Mathf.Deg2Rad);
		}

		// Linear stick rates (Interceptor: target = stick × max rate). Angle and horizon level through the same targets.
		private Vector3 ComputeInterceptorTargetRate(in RcCommand control, float yawStick, Quaternion rotation, InterceptorFlightProfile interceptor)
		{
			Vector3 maxRates = interceptor.MaxRates * Mathf.Deg2Rad;
			float yaw = Mathf.Clamp(yawStick, -1f, 1f) * maxRates.y;
			var acro = new Vector3(Mathf.Clamp(control.Pitch, -1f, 1f) * maxRates.x, yaw, -Mathf.Clamp(control.Roll, -1f, 1f) * maxRates.z);

			if (control.Mode == FlightMode.Acro)
			{
				return acro;
			}

			if (control.Mode == FlightMode.Angle)
			{
				Vector3 tilt = AngleController.ComputeTiltRate(rotation, control.Roll, control.Pitch, _profile.AngleLimit, _profile.AngleStrength);
				return new Vector3(Mathf.Clamp(tilt.x, -maxRates.x, maxRates.x), yaw, Mathf.Clamp(tilt.z, -maxRates.z, maxRates.z));
			}

			float levelStrength = HorizonLevelFactor(control.Roll, control.Pitch, _profile.HorizonTransition);
			Vector3 level = levelStrength > 0f
				? AngleController.ComputeTiltRate(rotation, 0f, 0f, _profile.AngleLimit, _profile.HorizonLevelStrength) * levelStrength
				: Vector3.zero;
			return new Vector3(Mathf.Clamp(acro.x + level.x, -maxRates.x, maxRates.x), yaw, Mathf.Clamp(acro.z + level.z, -maxRates.z, maxRates.z));
		}

		private void UpdateInterceptorTelemetry(InterceptorFlightProfile interceptor)
		{
			_telemetry.ThrustToWeight = interceptor.ThrottlePower;
			_telemetry.HoverThrottleEstimate = InterceptorHoverThrottle(interceptor);
		}

		// Throttle where thrust equals weight plus the zero-throttle extra gravity that is still fading out
		// (P1-SUN: 1/7 = 0.143 is inside the 0.15 fade, so it hovers at about 0.147).
		public static float InterceptorHoverThrottle(InterceptorFlightProfile interceptor)
		{
			float power = interceptor.ThrottlePower;
			if (power <= 0f)
			{
				return float.PositiveInfinity;
			}

			float plain = 1f / power;
			float extra = interceptor.ZeroThrottleExtraGravity;
			float fade = interceptor.ZeroThrottleFallFade;
			if (extra <= 0f || plain >= fade)
			{
				return plain;
			}

			return (1f + extra) / (power + extra / fade);
		}

		private ArmingBlockReason CollectArmingBlocks()
		{
			ArmingBlockReason blocks = ArmingBlockReason.None;
			if ((_armingInhibits & ArmingInhibit.External) != 0)
			{
				blocks |= ArmingBlockReason.External;
			}

			if ((_armingInhibits & ArmingInhibit.PayloadArmed) != 0)
			{
				blocks |= ArmingBlockReason.PayloadArmed;
			}

			if ((_armingInhibits & ArmingInhibit.Crashed) != 0)
			{
				blocks |= ArmingBlockReason.Crashed;
			}

			if ((_armingInhibits & ArmingInhibit.Recovering) != 0)
			{
				blocks |= ArmingBlockReason.Recovering;
			}

			if (_battery && r_battery.StateOfCharge <= ARMING_MIN_STATE_OF_CHARGE)
			{
				blocks |= ArmingBlockReason.BatteryEmpty;
			}

			if (r_failsafe.Stage != FailsafeStage.None)
			{
				blocks |= ArmingBlockReason.Failsafe;
			}

			return blocks;
		}

		// Body axes: x pitch (nose down +), y yaw (nose right +), z roll (left +). rad/s.
		private Vector3 ComputeTargetRate(in RcCommand control, Quaternion rotation)
		{
			float yaw = _rates.YawRate(control.Yaw) * Mathf.Deg2Rad;
			float maxPitch = _rates.MaxPitchRate * Mathf.Deg2Rad;
			float maxRoll = _rates.MaxRollRate * Mathf.Deg2Rad;

			if (control.Mode == FlightMode.Angle)
			{
				Vector3 tilt = AngleController.ComputeTiltRate(rotation, control.Roll, control.Pitch, _profile.AngleLimit, _profile.AngleStrength);
				return new Vector3(Mathf.Clamp(tilt.x, -maxPitch, maxPitch), yaw, Mathf.Clamp(tilt.z, -maxRoll, maxRoll));
			}

			var acro = new Vector3(
				_rates.PitchRate(control.Pitch) * Mathf.Deg2Rad,
				yaw,
				-_rates.RollRate(control.Roll) * Mathf.Deg2Rad);

			if (control.Mode != FlightMode.Horizon)
			{
				return acro;
			}

			// Horizon: acro rates plus a pull towards level that fades out with stick deflection.
			float levelStrength = HorizonLevelFactor(control.Roll, control.Pitch, _profile.HorizonTransition);
			Vector3 level = levelStrength > 0f
				? AngleController.ComputeTiltRate(rotation, 0f, 0f, _profile.AngleLimit, _profile.HorizonLevelStrength) * levelStrength
				: Vector3.zero;
			return new Vector3(
				Mathf.Clamp(acro.x + level.x, -maxPitch, maxPitch),
				yaw,
				Mathf.Clamp(acro.z + level.z, -maxRoll, maxRoll));
		}

		// 1 at centre stick, 0 from the transition deflection on (Betaflight horizon_transition).
		public static float HorizonLevelFactor(float roll, float pitch, float transition)
		{
			float deflection = Mathf.Max(Mathf.Abs(roll), Mathf.Abs(pitch));
			return Mathf.Clamp01(1f - deflection / Mathf.Max(0.01f, transition));
		}

		private RateControllerSettings CreateRateSettings(float thrustScale)
		{
			// A heavier payload raises inertia, so the same motors turn the drone slower.
			float baseMass = _airframe.FrameMass + (_battery ? _battery.Mass : 0f);
			float massAuthority = Mathf.Clamp01(baseMass / Mathf.Max(1e-4f, TotalMass));
			Vector3 maxAcceleration = _airframe.MaxAngularAcceleration * (Mathf.Deg2Rad * thrustScale * massAuthority);
			Vector3 responseTime = new(_profile.RollPitchResponseTime, _profile.YawResponseTime, _profile.RollPitchResponseTime);
			return new RateControllerSettings(responseTime, maxAcceleration, maxAcceleration * _profile.IntegralLimit, _profile.IntegralTime, _airframe.AngularDrag);
		}

		private void UpdateTelemetry(
			in RcCommand control,
			bool isLinkHealthy,
			in RcLinkState linkState,
			float throttle,
			float thrust,
			float thrustScale,
			float gravity,
			Vector3 targetRate,
			Vector3 bodyRate,
			Quaternion rotation,
			Vector3 velocity,
			float airspeed)
		{
			float mass = TotalMass;

			_telemetry.IsArmed = r_arming.IsArmed;
			_telemetry.ArmingBlockReason = r_arming.BlockReason;
			_telemetry.Mode = control.Mode;
			_telemetry.FailsafeStage = r_failsafe.Stage;

			_telemetry.IsLinkHealthy = isLinkHealthy;
			_telemetry.LinkQuality = linkState.IsLost ? 0f : linkState.Quality;
			_telemetry.LinkLatency = r_link.Latency;
			_telemetry.TimeSinceLastFrame = r_link.TimeSinceLastFrame;

			_telemetry.Throttle = throttle;
			_telemetry.MotorOutput = r_motor.Output;
			_telemetry.Thrust = thrust;
			_telemetry.ThrustToWeight = AirframeMath.ThrustToWeight(_airframe, mass, gravity, thrustScale);
			_telemetry.HoverThrottleEstimate = AirframeMath.HoverThrottle(_airframe, _profile, mass, gravity, thrustScale);
			_telemetry.TotalMass = mass;

			_telemetry.BatteryVoltage = r_battery.Voltage;
			_telemetry.BatteryCellVoltage = _battery ? r_battery.Voltage / _battery.CellCount : 0f;
			_telemetry.BatteryCurrent = r_battery.Current;
			_telemetry.BatteryUsedMah = r_battery.UsedMah;
			_telemetry.BatteryStateOfCharge = r_battery.StateOfCharge;

			_telemetry.TargetRollRate = -targetRate.z * Mathf.Rad2Deg;
			_telemetry.TargetPitchRate = targetRate.x * Mathf.Rad2Deg;
			_telemetry.TargetYawRate = targetRate.y * Mathf.Rad2Deg;
			_telemetry.RollRate = -bodyRate.z * Mathf.Rad2Deg;
			_telemetry.PitchRate = bodyRate.x * Mathf.Rad2Deg;
			_telemetry.YawRate = bodyRate.y * Mathf.Rad2Deg;

			_telemetry.TiltAngle = AngleController.GetTiltAngle(rotation);
			_telemetry.Airspeed = airspeed;
			_telemetry.GroundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
			_telemetry.VerticalSpeed = velocity.y;
		}
	}
}
