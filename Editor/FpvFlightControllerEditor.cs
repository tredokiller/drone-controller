using UnityEditor;

namespace UForce.FpvFlight.Editor
{
	[CustomEditor(typeof(FpvFlightController))]
	public sealed class FpvFlightControllerEditor : UnityEditor.Editor
	{
		public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			var controller = (FpvFlightController)target;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Derived values", EditorStyles.boldLabel);
			FlightDerivedValuesGui.Draw(controller.Airframe, controller.Battery, controller.Profile, controller.PayloadMass);

			if (EditorApplication.isPlaying && controller.IsInitialized)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Telemetry", EditorStyles.boldLabel);
				FlightTelemetryGui.Draw(controller.Telemetry);
			}
		}
	}
}
