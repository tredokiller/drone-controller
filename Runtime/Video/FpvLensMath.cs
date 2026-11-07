using UnityEngine;

namespace UForce.FpvFlight.Video
{
	// The fisheye of FpvCamera.hlsl on the CPU, for markers and the sun that must land where the lens puts them.
	public static class FpvLensMath
	{
		private const int BISECTION_STEPS = 24;

		// Camera frame point (viewport) -> screen point where the lens shows it.
		public static Vector2 ViewportToScreen(Vector2 viewport, Camera camera, float fisheye)
		{
			if (camera == null || fisheye <= 0.001f)
			{
				return viewport;
			}

			float aspect = Mathf.Max(0.1f, camera.aspect);
			float tanCorner = TanCorner(camera);
			float corner = 0.5f * Mathf.Sqrt(aspect * aspect + 1f);
			Vector2 point = new((viewport.x - 0.5f) * aspect, viewport.y - 0.5f);
			float rhoSource = point.magnitude / corner;

			if (rhoSource < 1e-5f)
			{
				return viewport;
			}

			// Inverse of FpvLensUV: the screen radius the lens takes from this frame radius (monotonic, so bisection).
			float low = 0f;
			float high = 3f;
			for (int step = 0; step < BISECTION_STEPS; step++)
			{
				float middle = 0.5f * (low + high);
				if (SourceRadius(middle, tanCorner, fisheye) < rhoSource)
				{
					low = middle;
				}
				else
				{
					high = middle;
				}
			}

			float rho = 0.5f * (low + high);
			point *= rho / rhoSource;
			return new Vector2(point.x / aspect + 0.5f, point.y + 0.5f);
		}

		// Tangent of half the camera frame diagonal.
		public static float TanCorner(Camera camera)
		{
			float aspect = Mathf.Max(0.1f, camera.aspect);
			return Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Sqrt(aspect * aspect + 1f);
		}

		// As FpvLensUV in the shader: share of the radius to the frame corner on screen -> in the camera frame.
		private static float SourceRadius(float rho, float tanCorner, float strength)
		{
			float cornerAngle = Mathf.Atan(tanCorner);
			float equidistant = rho * cornerAngle;
			float orthographic = Mathf.Asin(Mathf.Clamp01(rho * Mathf.Sin(cornerAngle)));
			float theta = Mathf.Min(Mathf.Lerp(equidistant, orthographic, 0.35f), 1.55f);
			return Mathf.Lerp(rho, Mathf.Tan(theta) / tanCorner, strength);
		}
	}
}
