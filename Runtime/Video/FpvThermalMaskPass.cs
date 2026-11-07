using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Video
{
	// Draws FpvThermalSignature renderers into the camera's thermal mask before post-processing, while the camera
	// matrices and depth texture are current. Interceptor swapped target materials to a magenta "hot" shader for every
	// camera; a per-camera mask keeps other cameras untouched.
	public sealed class FpvThermalMaskPass : ScriptableRenderPass
	{
		private FpvVideoCamera _video;

		public FpvThermalMaskPass()
		{
			profilingSampler = new ProfilingSampler("FPV Thermal Mask");
		}

		public void Setup(FpvVideoCamera video)
		{
			_video = video;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (_video == null || _video.ThermalMaskHandle == null)
			{
				return;
			}

			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			TextureHandle mask = renderGraph.ImportTexture(_video.ThermalMaskHandle);

			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FPV Thermal Mask", out PassData passData, profilingSampler))
			{
				passData.Signatures = FpvThermalSignature.Active;
				builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
				if (resourceData.cameraDepthTexture.IsValid())
				{
					builder.UseTexture(resourceData.cameraDepthTexture);
				}

				builder.UseAllGlobalTextures(true);
				builder.AllowPassCulling(false);
				builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => Draw(data, context));
			}
		}

		private static void Draw(PassData data, RasterGraphContext context)
		{
			context.cmd.ClearRenderTarget(false, true, Color.clear);

			IReadOnlyList<FpvThermalSignature> signatures = data.Signatures;
			for (int index = 0; index < signatures.Count; index++)
			{
				signatures[index].Draw(context.cmd);
			}
		}

		private sealed class PassData
		{
			public IReadOnlyList<FpvThermalSignature> Signatures;
		}
	}
}
