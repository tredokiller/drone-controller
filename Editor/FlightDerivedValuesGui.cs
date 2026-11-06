using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Editor
{
	// Read-only numbers that follow from thrust, mass and drag. They are shown, never typed in.
	public static class FlightDerivedValuesGui
	{
		private const float MS_TO_KMH = 3.6f;

		public static void Draw(AirframeDefinition airframe, BatteryDefinition battery, FlightControllerProfile profile, float payloadMass)
		{
			if (!airframe)
			{
				return;
			}

			float gravity = Physics.gravity.magnitude;
			float mass = AirframeMath.TotalMass(airframe, battery, payloadMass);

			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.FloatField("Total mass, kg", mass);
				EditorGUILayout.FloatField("Thrust to weight", AirframeMath.ThrustToWeight(airframe, mass, gravity, 1f));
				DrawThrottle("Hover throttle, full pack", AirframeMath.HoverThrottle(airframe, profile, mass, gravity, 1f));

				if (battery)
				{
					float nominalScale = Square(battery.NominalVoltage / battery.ReferenceVoltage);
					DrawThrottle("Hover throttle, nominal voltage", AirframeMath.HoverThrottle(airframe, profile, mass, gravity, nominalScale));
				}

				float topSpeed = AirframeMath.MaxLevelSpeed(airframe, mass, gravity, 1f, out float tilt);
				EditorGUILayout.TextField("Top level speed", $"{topSpeed * MS_TO_KMH:0} km/h at {tilt:0}° tilt");
				EditorGUILayout.TextField("Max climb speed", $"{AirframeMath.MaxClimbSpeed(airframe, mass, gravity, 1f):0.0} m/s");
				EditorGUILayout.TextField("Free-fall terminal speed", $"{AirframeMath.TerminalDescentSpeed(airframe, mass, gravity):0.0} m/s");

				if (battery)
				{
					float hoverCurrent = AirframeMath.HoverCurrent(airframe, battery, mass, gravity);
					EditorGUILayout.TextField("Hover current", $"{hoverCurrent:0.0} A");
					EditorGUILayout.TextField("Hover flight time", $"{AirframeMath.HoverFlightTimeMinutes(airframe, battery, mass, gravity):0.0} min");

					BatteryModel punch = new();
					punch.Reset(battery, 1f);
					float thrust = punch.Update(battery, airframe, airframe.MaxThrust, 0.001f);
					EditorGUILayout.TextField("Full throttle, full pack", $"{thrust:0} N at {punch.Voltage:0.0} V, {punch.Current:0} A");
				}
			}
		}

		private static void DrawThrottle(string label, float throttle)
		{
			string text = float.IsPositiveInfinity(throttle) ? "cannot hover" : $"{throttle * 100f:0.0} %";
			EditorGUILayout.TextField(label, text);
		}

		private static float Square(float value) => value * value;
	}
}
