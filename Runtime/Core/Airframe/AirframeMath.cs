using UForce.FpvFlight.Control;
using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	// Values derived from thrust, mass and drag. Used by the inspector, telemetry and tests; never by the flight loop.
	public static class AirframeMath
	{
		private const int BISECTION_STEPS = 40;
		private const float USABLE_CAPACITY = 0.9f;

		public static float TotalMass(AirframeDefinition airframe, BatteryDefinition battery, float payloadMass) =>
			airframe.FrameMass + (battery != null ? battery.Mass : 0f) + Mathf.Max(0f, payloadMass);

		public static float ThrustAtThrottle(AirframeDefinition airframe, FlightControllerProfile profile, float throttle, float thrustScale)
		{
			float curved = profile != null ? profile.ApplyThrottleCurve(throttle) : Mathf.Clamp01(throttle);
			float output = ThrustModel.MotorCommand(curved, airframe.MotorIdle);
			return airframe.MaxThrust * ThrustModel.ThrustFraction(output, airframe.ThrustLinearity) * thrustScale;
		}

		public static float ThrustToWeight(AirframeDefinition airframe, float mass, float gravity, float thrustScale) =>
			airframe.MaxThrust * thrustScale / Mathf.Max(1e-4f, mass * gravity);

		// Throttle stick position (0..1) that holds altitude. +Infinity when full throttle cannot lift the drone.
		public static float HoverThrottle(AirframeDefinition airframe, FlightControllerProfile profile, float mass, float gravity, float thrustScale)
		{
			float weight = mass * gravity;
			if (ThrustAtThrottle(airframe, profile, 1f, thrustScale) < weight)
			{
				return float.PositiveInfinity;
			}

			if (ThrustAtThrottle(airframe, profile, 0f, thrustScale) >= weight)
			{
				return 0f;
			}

			float low = 0f;
			float high = 1f;
			for (int step = 0; step < BISECTION_STEPS; step++)
			{
				float middle = 0.5f * (low + high);
				if (ThrustAtThrottle(airframe, profile, middle, thrustScale) < weight)
				{
					low = middle;
				}
				else
				{
					high = middle;
				}
			}

			return 0.5f * (low + high);
		}

		// Steady climb speed at full throttle, m/s.
		public static float MaxClimbSpeed(AirframeDefinition airframe, float mass, float gravity, float thrustScale)
		{
			float excess = airframe.MaxThrust * thrustScale - mass * gravity;
			return excess <= 0f ? 0f : Mathf.Sqrt(excess / DragCoefficient(airframe.DragArea.y));
		}

		// Terminal speed of a level drone falling with motors stopped, m/s.
		public static float TerminalDescentSpeed(AirframeDefinition airframe, float mass, float gravity) =>
			Mathf.Sqrt(mass * gravity / DragCoefficient(airframe.DragArea.y));

		// Fastest level flight at full throttle, holding altitude, m/s. tiltDegrees is the pitch at that speed.
		// Thrust holds the weight (T·cosθ = m·g) and its horizontal part balances drag on the tilted body:
		//   T·sinθ = ½·ρ·v²·(CdA_top·sinθ + CdA_front·cosθ).
		public static float MaxLevelSpeed(AirframeDefinition airframe, float mass, float gravity, float thrustScale, out float tiltDegrees)
		{
			float weight = mass * gravity;
			float maxThrust = airframe.MaxThrust * thrustScale;
			if (maxThrust <= weight)
			{
				tiltDegrees = 0f;
				return 0f;
			}

			float tilt = Mathf.Acos(weight / maxThrust);
			tiltDegrees = tilt * Mathf.Rad2Deg;
			return LevelSpeedAtTilt(airframe, tilt, weight);
		}

		// Level flight speed at a fixed forward tilt with the thrust that holds altitude, m/s.
		public static float LevelSpeedAtTilt(AirframeDefinition airframe, float tiltRadians, float weight)
		{
			float cos = Mathf.Cos(tiltRadians);
			float sin = Mathf.Sin(tiltRadians);
			if (cos <= 1e-4f)
			{
				return float.PositiveInfinity;
			}

			float area = airframe.DragArea.y * sin + airframe.DragArea.z * cos;
			return Mathf.Sqrt(weight * sin / cos / (0.5f * PhysicalConstants.AIR_DENSITY * Mathf.Max(area, 1e-4f)));
		}

		// Current while hovering at nominal voltage, A.
		public static float HoverCurrent(AirframeDefinition airframe, BatteryDefinition battery, float mass, float gravity)
		{
			if (battery == null)
			{
				return 0f;
			}

			float power = BatteryModel.InducedPower(mass * gravity, airframe) / airframe.PropulsionEfficiency;
			return power / battery.NominalVoltage + airframe.AvionicsCurrent;
		}

		// Hover time on the usable capacity, minutes.
		public static float HoverFlightTimeMinutes(AirframeDefinition airframe, BatteryDefinition battery, float mass, float gravity)
		{
			float current = HoverCurrent(airframe, battery, mass, gravity);
			return current <= 0f ? 0f : battery.CapacityMah * 0.001f * USABLE_CAPACITY / current * 60f;
		}

		public static Vector3 BoxInertia(float mass, Vector3 size)
		{
			float x2 = size.x * size.x;
			float y2 = size.y * size.y;
			float z2 = size.z * size.z;
			float factor = mass / 12f;
			return new Vector3(factor * (y2 + z2), factor * (x2 + z2), factor * (x2 + y2));
		}

		private static float DragCoefficient(float dragArea) => 0.5f * PhysicalConstants.AIR_DENSITY * Mathf.Max(dragArea, 1e-4f);
	}
}
