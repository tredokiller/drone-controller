using UForce.FpvFlight.Rc;

namespace UForce.FpvFlight
{
	// Snapshot of the flight controller for OSD, sound and debugging. Rates use the radio convention
	// (roll right +, pitch nose down +, yaw nose right +), in deg/s.
	public struct FlightTelemetry
	{
		public bool IsArmed { get; internal set; }
		public ArmingBlockReason ArmingBlockReason { get; internal set; }
		public FlightMode Mode { get; internal set; }
		public FailsafeStage FailsafeStage { get; internal set; }

		public bool IsLinkHealthy { get; internal set; }
		public float LinkQuality { get; internal set; }
		public float LinkLatency { get; internal set; }
		public float TimeSinceLastFrame { get; internal set; }

		public float Throttle { get; internal set; }
		public float MotorOutput { get; internal set; }
		public float Thrust { get; internal set; }
		public float ThrustToWeight { get; internal set; }
		public float HoverThrottleEstimate { get; internal set; }
		public float TotalMass { get; internal set; }

		public float BatteryVoltage { get; internal set; }
		public float BatteryCellVoltage { get; internal set; }
		public float BatteryCurrent { get; internal set; }
		public float BatteryUsedMah { get; internal set; }
		public float BatteryStateOfCharge { get; internal set; }

		public float TargetRollRate { get; internal set; }
		public float TargetPitchRate { get; internal set; }
		public float TargetYawRate { get; internal set; }
		public float RollRate { get; internal set; }
		public float PitchRate { get; internal set; }
		public float YawRate { get; internal set; }

		public float TiltAngle { get; internal set; }
		public float Airspeed { get; internal set; }
		public float GroundSpeed { get; internal set; }
		public float VerticalSpeed { get; internal set; }
	}
}
