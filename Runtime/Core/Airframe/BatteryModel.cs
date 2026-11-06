using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	// Pack voltage under load, current draw and state of charge. Thrust scales with (loaded voltage / full-pack voltage)²,
	// so a draining battery and hard throttle punches both make the drone weaker.
	public sealed class BatteryModel
	{
		// Below this state of charge the ESCs brown out and thrust fades to zero.
		public const float DEPLETED_STATE_OF_CHARGE = 0.02f;

		private const int OPERATING_POINT_STEPS = 24;

		public float StateOfCharge { get; private set; } = 1f;
		public float Voltage { get; private set; }
		public float Current { get; private set; }
		public float UsedMah { get; private set; }
		public float ThrustScale { get; private set; } = 1f;
		public bool IsDepleted => StateOfCharge <= 0f;

		public void Reset(BatteryDefinition definition, float stateOfCharge)
		{
			StateOfCharge = Mathf.Clamp01(stateOfCharge);
			Current = 0f;
			UsedMah = 0f;

			if (definition == null)
			{
				Voltage = 0f;
				ThrustScale = 1f;
				return;
			}

			Voltage = definition.OpenCircuitVoltage(StateOfCharge);
			ThrustScale = ComputeThrustScale(definition, Voltage, StateOfCharge);
		}

		// referenceThrust is what the motors would produce on a full, unloaded pack. Solves the operating point
		// (loaded voltage and thrust that agree with each other), drains the pack and returns the thrust produced.
		public float Update(BatteryDefinition definition, AirframeDefinition airframe, float referenceThrust, float deltaTime)
		{
			if (definition == null)
			{
				ThrustScale = 1f;
				return referenceThrust;
			}

			float openCircuit = definition.OpenCircuitVoltage(StateOfCharge);
			float resistance = definition.PackResistance;
			float voltage = resistance < 1e-6f ? openCircuit : SolveLoadedVoltage(definition, airframe, referenceThrust, openCircuit, resistance);

			ThrustScale = ComputeThrustScale(definition, voltage, StateOfCharge);
			float thrust = referenceThrust * ThrustScale;
			float power = InducedPower(thrust, airframe) / airframe.PropulsionEfficiency;
			float current = resistance < 1e-6f
				? (voltage > 0f ? power / voltage : 0f) + airframe.AvionicsCurrent
				: (openCircuit - voltage) / resistance;

			Voltage = Mathf.Max(0f, voltage);
			Current = Mathf.Max(0f, current);

			float usedAmpSeconds = Current * deltaTime;
			UsedMah += usedAmpSeconds / 3.6f;
			StateOfCharge = Mathf.Max(0f, StateOfCharge - usedAmpSeconds / (definition.CapacityMah * 3.6f));
			return thrust;
		}

		// Ideal induced power of a hovering rotor set (momentum theory), W.
		public static float InducedPower(float thrust, AirframeDefinition airframe)
		{
			if (thrust <= 0f)
			{
				return 0f;
			}

			return thrust * Mathf.Sqrt(thrust / (2f * PhysicalConstants.AIR_DENSITY * airframe.DiskArea));
		}

		// Thrust grows with voltage and power demand grows with thrust, so the loaded voltage is the fixed point
		// V = Voc − I(T(V))·R. The residual V − f(V) rises monotonically, so bisection always converges.
		private float SolveLoadedVoltage(BatteryDefinition definition, AirframeDefinition airframe, float referenceThrust, float openCircuit, float resistance)
		{
			float low = 0f;
			float high = openCircuit;
			for (int step = 0; step < OPERATING_POINT_STEPS; step++)
			{
				float middle = 0.5f * (low + high);
				if (middle < LoadedVoltage(definition, airframe, referenceThrust, middle, openCircuit, resistance))
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

		private float LoadedVoltage(BatteryDefinition definition, AirframeDefinition airframe, float referenceThrust, float voltage, float openCircuit, float resistance)
		{
			float thrust = referenceThrust * ComputeThrustScale(definition, voltage, StateOfCharge);
			float power = InducedPower(thrust, airframe) / airframe.PropulsionEfficiency;

			// V = Voc − I·R with I = P / V + I_avionics  →  V² − (Voc − I_avionics·R)·V + P·R = 0.
			float b = openCircuit - airframe.AvionicsCurrent * resistance;
			float discriminant = b * b - 4f * power * resistance;
			return discriminant > 0f ? 0.5f * (b + Mathf.Sqrt(discriminant)) : 0.5f * b;
		}

		private static float ComputeThrustScale(BatteryDefinition definition, float voltage, float stateOfCharge)
		{
			float ratio = voltage / definition.ReferenceVoltage;
			float availability = Mathf.Clamp01(stateOfCharge / DEPLETED_STATE_OF_CHARGE);
			return ratio * ratio * availability;
		}
	}
}
