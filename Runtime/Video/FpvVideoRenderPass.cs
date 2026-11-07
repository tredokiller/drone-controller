using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Video
{
	public sealed class FpvVideoRenderPass : ScriptableRenderPass
	{
		private const int VIDEO_PASS = 0;
		private const int METER_PASS = 1;

		private FpvVideoCamera _video;

		public FpvVideoRenderPass()
		{
			requiresIntermediateTexture = true;
			profilingSampler = new ProfilingSampler("FPV Video");
		}

		public void Setup(FpvVideoCamera video)
		{
			_video = video;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			if (resourceData.isActiveTargetBackBuffer || _video == null || _video.Material == null)
			{
				return;
			}

			TextureHandle source = resourceData.activeColorTexture;
			TextureDesc description = renderGraph.GetTextureDesc(source);
			description.name = "_FpvVideo";
			description.clearBuffer = false;
			description.msaaSamples = MSAASamples.None;
			TextureHandle destination = renderGraph.CreateTexture(description);

			// Lens water, glow and the analog softness sample lower mips, and the camera colour has none.
			TextureDesc sourceDescription = description;
			sourceDescription.name = "_FpvVideoSource";
			sourceDescription.useMipMap = true;
			sourceDescription.autoGenerateMips = true;
			TextureHandle mippedSource = renderGraph.CreateTexture(sourceDescription);
			renderGraph.AddBlitPass(source, mippedSource, Vector2.one, Vector2.zero, passName: "FPV Video Source");

			_video.SetVideoSize(description.width, description.height);

			if (_video.MeterHandle != null)
			{
				TextureHandle meter = renderGraph.ImportTexture(_video.MeterHandle);
				renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(mippedSource, meter, _video.Material, METER_PASS), "FPV Video Meter");
			}

			renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(mippedSource, destination, _video.Material, VIDEO_PASS), "FPV Video");
			resourceData.cameraColor = destination;
		}
	}
}
