using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Video
{
	// Add to the URP renderer used by FPV drone cameras. Cameras with an enabled FpvVideoCamera get the FPV video
	// (after URP post-processing) and, in thermal mode, the heat mask of FpvThermalSignature objects.
	public sealed class FpvVideoRendererFeature : ScriptableRendererFeature
	{
		private FpvVideoRenderPass _videoPass;
		private FpvThermalMaskPass _thermalPass;

		public override void Create()
		{
			_videoPass = new FpvVideoRenderPass
			{
				renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
			};
			_thermalPass = new FpvThermalMaskPass
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
			};
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (renderingData.cameraData.cameraType != CameraType.Game)
			{
				return;
			}

			if (!renderingData.cameraData.camera.TryGetComponent(out FpvVideoCamera video) || !video.isActiveAndEnabled || video.Material == null)
			{
				return;
			}

			if (video.IsThermal && video.ThermalMaskHandle != null && FpvThermalSignature.Active.Count > 0)
			{
				_thermalPass.Setup(video);
				renderer.EnqueuePass(_thermalPass);
			}

			_videoPass.Setup(video);
			renderer.EnqueuePass(_videoPass);
		}
	}
}
