using UnityEngine;

namespace UForce.FpvFlight.DroneTypes.Editor
{
	// How one drone prefab is put together from its model, as the Interceptor Drones prefabs are.
	internal sealed class FpvDronePrefabSpec
	{
		public string Name { get; set; }
		public string ModelPath { get; set; }
		public Material Material { get; set; }
		public Vector3 MountPosition { get; set; }
		public Vector3 ModelPosition { get; set; }
		public Vector3 ModelRotation { get; set; }
		public Vector3 CameraPosition { get; set; }
		// Interceptor P1-SUN: 0.15, so the camera does not see the inside of its own nose cone.
		public float CameraNearClip { get; set; } = 0.05f;
		public string[] PropellerNames { get; set; }
		// The propeller meshes sit at the model origin: give each a pivot at its own centre so it spins in place.
		public bool IsPropellerPivotNeeded { get; set; }
		public Vector3 PropellerAxis { get; set; }
		public FpvDroneType Type { get; set; }
	}
}
