using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Rates;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Editor
{
	public static class FpvDroneMenu
	{
		[MenuItem("GameObject/UForce/FPV Drone", false, 10)]
		private static void CreateDrone(MenuCommand command)
		{
			var drone = new GameObject("FPV Drone");
			GameObjectUtility.SetParentAndAlign(drone, command.context as GameObject);
			Undo.RegisterCreatedObjectUndo(drone, "Create FPV Drone");

			var collider = drone.AddComponent<BoxCollider>();
			collider.size = new Vector3(0.3f, 0.1f, 0.3f);

			var body = drone.AddComponent<Rigidbody>();
			body.interpolation = RigidbodyInterpolation.Interpolate;
			body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

			var controller = drone.AddComponent<FpvFlightController>();
			var serialized = new SerializedObject(controller);
			serialized.FindProperty("_airframe").objectReferenceValue = FindFirst<AirframeDefinition>();
			serialized.FindProperty("_battery").objectReferenceValue = FindFirst<BatteryDefinition>();
			serialized.FindProperty("_rates").objectReferenceValue = FindFirst<RatesProfile>();
			serialized.FindProperty("_profile").objectReferenceValue = FindFirst<FlightControllerProfile>();
			serialized.ApplyModifiedPropertiesWithoutUndo();

			Selection.activeObject = drone;
		}

		private static T FindFirst<T>() where T : Object
		{
			string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
			return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}
	}
}
