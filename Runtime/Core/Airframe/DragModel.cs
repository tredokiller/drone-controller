using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	public static class DragModel
	{
		// Quadratic drag opposite to the air-relative velocity (body axes, m/s). The drag area is the box projection
		// along the flow: |dx|·CdA_side + |dy|·CdA_top + |dz|·CdA_front. Pure drag, no lift. Returns body-axis force, N.
		public static Vector3 BodyDrag(Vector3 bodyAirVelocity, Vector3 dragArea)
		{
			float speed = bodyAirVelocity.magnitude;
			if (speed < 1e-4f)
			{
				return Vector3.zero;
			}

			float area = ProjectedArea(bodyAirVelocity / speed, dragArea);
			return -0.5f * PhysicalConstants.AIR_DENSITY * area * speed * bodyAirVelocity;
		}

		public static float ProjectedArea(Vector3 bodyDirection, Vector3 dragArea) =>
			Mathf.Abs(bodyDirection.x) * dragArea.x + Mathf.Abs(bodyDirection.y) * dragArea.y + Mathf.Abs(bodyDirection.z) * dragArea.z;
	}
}
