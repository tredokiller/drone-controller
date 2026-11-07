using UnityEngine;

namespace UForce.FpvFlight.Video
{
	// What FPV goggles show without a signal: full static, as Interceptor Drones' video at total loss. Call Render every frame
	// into a video output (web page, second monitor) while no drone camera renders into it.
	public static class FpvVideoNoSignal
	{
		private const string SHADER_RESOURCE = "UForce/FpvFlight/FpvNoSignal";

		private static readonly int r_intensityId = Shader.PropertyToID("_Intensity");

		private static Material s_material;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			s_material = null;
		}

		// intensity 0..1: 1 full static, lower values darker.
		public static void Render(RenderTexture target, float intensity = 1f)
		{
			if (target == null)
			{
				return;
			}

			if (s_material == null)
			{
				var shader = Resources.Load<Shader>(SHADER_RESOURCE);
				if (shader == null)
				{
					Debug.LogError($"[{nameof(FpvVideoNoSignal)}] Shader Resources/{SHADER_RESOURCE} is missing.");
					return;
				}

				s_material = new Material(shader)
				{
					name = "FPV No Signal",
					hideFlags = HideFlags.DontSave
				};
			}

			s_material.SetFloat(r_intensityId, Mathf.Clamp01(intensity));
			Graphics.Blit(null, target, s_material);
		}
	}
}
