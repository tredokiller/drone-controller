using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Editor
{
	[CustomEditor(typeof(AirframeDefinition))]
	public sealed class AirframeDefinitionEditor : UnityEditor.Editor
	{
		private BatteryDefinition _previewBattery;
		private FlightControllerProfile _previewProfile;
		private float _previewPayload;

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Derived values", EditorStyles.boldLabel);
			_previewBattery = (BatteryDefinition)EditorGUILayout.ObjectField("Preview battery", _previewBattery, typeof(BatteryDefinition), false);
			_previewProfile = (FlightControllerProfile)EditorGUILayout.ObjectField("Preview FC profile", _previewProfile, typeof(FlightControllerProfile), false);
			_previewPayload = Mathf.Max(0f, EditorGUILayout.FloatField("Preview payload, kg", _previewPayload));

			FlightDerivedValuesGui.Draw((AirframeDefinition)target, _previewBattery, _previewProfile, _previewPayload);
		}
	}
}
