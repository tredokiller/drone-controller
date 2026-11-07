using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Ported from Interceptor Drones (HudTextAtlasGuard). Without its atlas the TMP shader samples the built-in white
	// texture and every glyph turns into a solid box. Detects that on OSD texts, logs the state needed to find the cause
	// and rebinds the font material.
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(1001)]
	public sealed class FpvHudTextAtlasGuard : MonoBehaviour
	{
		private static readonly int r_mainTexId = Shader.PropertyToID("_MainTex");

		[SerializeField, Min(0f)] private float _checkInterval = 0.2f;

		private readonly List<TMP_Text> r_texts = new();
		private readonly HashSet<TMP_Text> r_broken = new();
		private float _nextCheckTime;

		private void LateUpdate()
		{
			if (Time.unscaledTime < _nextCheckTime)
			{
				return;
			}

			_nextCheckTime = Time.unscaledTime + _checkInterval;
			GetComponentsInChildren(false, r_texts);

			foreach (TMP_Text text in r_texts)
			{
				string problem = FindProblem(text);
				if (problem == null)
				{
					r_broken.Remove(text);
					continue;
				}

				// Log once per incident, keep repairing while it lasts.
				if (r_broken.Add(text))
				{
					Debug.LogError(Describe(text, problem), text);
				}

				Repair(text);
			}
		}

		private static string FindProblem(TMP_Text text)
		{
			TMP_FontAsset font = text.font;
			if (font == null)
			{
				return "font asset is missing";
			}

			// Nothing is drawn yet, and a dynamic atlas may legitimately still be empty.
			if (text.textInfo == null || text.textInfo.characterCount == 0)
			{
				return null;
			}

			Texture2D atlas = font.atlasTexture;
			if (atlas == null)
			{
				return "font atlas texture is missing";
			}

			if (atlas.width <= 1 || atlas.height <= 1)
			{
				return "font atlas texture is empty";
			}

			Material shared = text.fontSharedMaterial;
			if (shared == null)
			{
				return "shared material is missing";
			}

			if (shared.GetTexture(r_mainTexId) != atlas)
			{
				return "shared material does not use the font atlas";
			}

			CanvasRenderer renderer = text.canvasRenderer;
			if (renderer.materialCount == 0)
			{
				return null;
			}

			Material rendered = renderer.GetMaterial(0);
			if (rendered == null)
			{
				return "canvas renderer material is missing";
			}

			return rendered.GetTexture(r_mainTexId) != atlas ? "canvas renderer material does not use the font atlas" : null;
		}

		private static void Repair(TMP_Text text)
		{
			TMP_FontAsset font = text.font;
			if (font == null || font.atlasTexture == null || font.material == null)
			{
				return;
			}

			if (font.material.GetTexture(r_mainTexId) != font.atlasTexture)
			{
				font.material.SetTexture(r_mainTexId, font.atlasTexture);
			}

			Material shared = text.fontSharedMaterial;
			if (shared == null || shared.GetTexture(r_mainTexId) != font.atlasTexture)
			{
				text.fontSharedMaterial = font.material;
			}

			text.SetMaterialDirty();
			text.ForceMeshUpdate(true, true);
		}

		private static string Describe(TMP_Text text, string problem)
		{
			TMP_FontAsset font = text.font;
			Texture2D atlas = font != null ? font.atlasTexture : null;
			Material shared = text.fontSharedMaterial;
			Material rendered = text.canvasRenderer.materialCount > 0 ? text.canvasRenderer.GetMaterial(0) : null;
			Canvas canvas = text.canvas;

			var builder = new StringBuilder();
			builder.Append($"[{nameof(FpvHudTextAtlasGuard)}] ").Append(problem).Append(" on '").Append(GetPath(text.transform)).Append('\'');
			builder.Append("\n  frame=").Append(Time.frameCount).Append(" time=").Append(Time.realtimeSinceStartup.ToString("F2"));
			builder.Append("\n  text='").Append(text.text).Append("' characters=").Append(text.textInfo != null ? text.textInfo.characterCount : -1);
			builder.Append("\n  font=").Append(Name(font)).Append(" population=").Append(font != null ? font.atlasPopulationMode.ToString() : "-");
			builder.Append("\n  atlas=").Append(Name(atlas)).Append(atlas != null ? $" {atlas.width}x{atlas.height} {atlas.format}" : string.Empty);
			builder.Append("\n  font material=").Append(Name(font != null ? font.material : null))
				.Append(" tex=").Append(Name(font != null && font.material != null ? font.material.GetTexture(r_mainTexId) : null));
			builder.Append("\n  shared material=").Append(Name(shared)).Append(" tex=").Append(Name(shared != null ? shared.GetTexture(r_mainTexId) : null));
			builder.Append("\n  rendered material=").Append(Name(rendered)).Append(" tex=").Append(Name(rendered != null ? rendered.GetTexture(r_mainTexId) : null));
			builder.Append("\n  canvas=").Append(canvas != null ? canvas.name : "-")
				.Append(" camera=").Append(canvas != null && canvas.worldCamera != null ? canvas.worldCamera.name : "-")
				.Append(" scale=").Append(text.transform.lossyScale.y.ToString("E3"));
			return builder.ToString();
		}

		private static string Name(Object target) => target == null ? "null" : $"{target.name}#{target.GetInstanceID()}";

		private static string GetPath(Transform node)
		{
			string path = node.name;
			for (Transform parent = node.parent; parent != null; parent = parent.parent)
			{
				path = parent.name + "/" + path;
			}

			return path;
		}
	}
}
