using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UForce.FpvFlight.Video.Editor
{
	// Ported from Interceptor Drones (HdrGradingBuildVariants). FpvVideoCamera switches URP colour grading to HDR at
	// runtime while a video profile is on. URP strips the HDR grading variant (_HDR_GRADING) from a build when no URP
	// asset is set to HDR, and then the digital picture turns into purple mush in the player. So during the build the
	// project's URP assets are set to HDR before URP collects its features (order 0), and restored afterwards:
	// on OnPostprocessBuild, or after a failed build on the first editor frame without a build.
	public sealed class FpvHdrGradingBuildVariants : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		private static readonly Dictionary<UniversalRenderPipelineAsset, ColorGradingMode> s_saved = new();

		public int callbackOrder => -1000;

		public void OnPreprocessBuild(BuildReport report)
		{
			Restore();

			foreach (UniversalRenderPipelineAsset asset in ProjectAssets())
			{
				if (asset.colorGradingMode == ColorGradingMode.HighDynamicRange || s_saved.ContainsKey(asset))
				{
					continue;
				}

				s_saved[asset] = asset.colorGradingMode;
				asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
			}

			if (s_saved.Count > 0)
			{
				Debug.Log($"[{nameof(FpvHdrGradingBuildVariants)}] HDR colour grading kept in the build ({s_saved.Count} URP asset(s) set to HDR for the build).");
				EditorApplication.update -= RestoreAfterBuild;
				EditorApplication.update += RestoreAfterBuild;
			}
		}

		public void OnPostprocessBuild(BuildReport report) => Restore();

		private static void RestoreAfterBuild()
		{
			if (BuildPipeline.isBuildingPlayer)
			{
				return;
			}

			Restore();
		}

		private static void Restore()
		{
			EditorApplication.update -= RestoreAfterBuild;

			foreach (KeyValuePair<UniversalRenderPipelineAsset, ColorGradingMode> pair in s_saved)
			{
				if (pair.Key == null)
				{
					continue;
				}

				pair.Key.colorGradingMode = pair.Value;
				EditorUtility.SetDirty(pair.Key);
				AssetDatabase.SaveAssetIfDirty(pair.Key);
			}

			s_saved.Clear();
		}

		// URP assets that go into the build: the default one and those of the quality levels.
		private static IEnumerable<UniversalRenderPipelineAsset> ProjectAssets()
		{
			HashSet<UniversalRenderPipelineAsset> assets = new();

			if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main)
			{
				assets.Add(main);
			}

			for (int index = 0; index < QualitySettings.names.Length; index++)
			{
				if (QualitySettings.GetRenderPipelineAssetAt(index) is UniversalRenderPipelineAsset level)
				{
					assets.Add(level);
				}
			}

			return assets;
		}
	}
}
