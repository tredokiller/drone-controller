using UForce.FpvFlight.Rates;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Editor
{
	// Shows the rate curves the way Betaflight Configurator does: stick deflection to deg/s.
	[CustomEditor(typeof(RatesProfile))]
	public sealed class RatesProfileEditor : UnityEditor.Editor
	{
		private const int SAMPLES = 48;
		private const float GRAPH_HEIGHT = 140f;

		private static readonly Color s_rollColor = new(0.95f, 0.35f, 0.3f);
		private static readonly Color s_pitchColor = new(0.3f, 0.8f, 0.35f);
		private static readonly Color s_yawColor = new(0.35f, 0.55f, 0.95f);

		private readonly Vector3[] r_points = new Vector3[SAMPLES + 1];

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			var profile = (RatesProfile)target;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Max rate at full stick", EditorStyles.boldLabel);
			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.TextField("Roll", $"{profile.MaxRollRate:0} deg/s");
				EditorGUILayout.TextField("Pitch", $"{profile.MaxPitchRate:0} deg/s");
				EditorGUILayout.TextField("Yaw", $"{profile.MaxYawRate:0} deg/s");
			}

			Rect rect = GUILayoutUtility.GetRect(10f, GRAPH_HEIGHT, GUILayout.ExpandWidth(true));
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
			float maxRate = Mathf.Max(100f, Mathf.Max(profile.MaxRollRate, Mathf.Max(profile.MaxPitchRate, profile.MaxYawRate)));
			DrawCurve(rect, maxRate, profile.RollRate, s_rollColor);
			DrawCurve(rect, maxRate, profile.PitchRate, s_pitchColor);
			DrawCurve(rect, maxRate, profile.YawRate, s_yawColor);
			GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 200f, 16f), $"{maxRate:0} deg/s", EditorStyles.miniLabel);
		}

		private void DrawCurve(Rect rect, float maxRate, System.Func<float, float> evaluate, Color color)
		{
			for (int index = 0; index <= SAMPLES; index++)
			{
				float stick = index / (float)SAMPLES;
				float rate = evaluate(stick) / maxRate;
				r_points[index] = new Vector3(rect.x + stick * rect.width, rect.yMax - rate * rect.height, 0f);
			}

			Handles.color = color;
			Handles.DrawAAPolyLine(2f, r_points);
		}
	}
}
