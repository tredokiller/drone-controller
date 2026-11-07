using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UForce.FpvFlight.Video
{
	// Marks an object as hot for FPV thermal cameras (people, engines, drones). Its renderers glow white in the
	// white-hot picture and yellow in the colour ramp, as targets did in Interceptor's thermal mode.
	[DisallowMultipleComponent]
	public sealed class FpvThermalSignature : MonoBehaviour
	{
		private const string SHADER_NAME = "UForce/FPV Flight/Thermal Signature";

		private static readonly List<FpvThermalSignature> s_active = new();
		private static readonly int r_heatId = Shader.PropertyToID("_FpvHeat");

		[Tooltip("0 is the background, 1 is as hot as a running engine or a person.")]
		[SerializeField, Range(0f, 1f)] private float _heat = 1f;
		[Tooltip("Empty: every mesh renderer under this object.")]
		[SerializeField] private Renderer[] _renderers;
		[SerializeField] private Shader _shader;

		private readonly List<Renderer> r_drawnRenderers = new();
		private readonly List<int> r_subMeshCounts = new();
		private Material _material;

		public static IReadOnlyList<FpvThermalSignature> Active => s_active;
		public float Heat => _heat;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			s_active.Clear();
		}

		private void OnEnable()
		{
			if (_material == null)
			{
				Shader shader = _shader != null ? _shader : Shader.Find(SHADER_NAME);
				if (shader == null)
				{
					Debug.LogError($"[{nameof(FpvThermalSignature)}] Shader '{SHADER_NAME}' is missing.", this);
					return;
				}

				_material = new Material(shader)
				{
					name = "FPV Thermal Signature",
					hideFlags = HideFlags.DontSave
				};
			}

			_material.SetFloat(r_heatId, _heat);
			CollectRenderers();
			s_active.Add(this);
		}

		private void OnDisable()
		{
			s_active.Remove(this);
		}

		private void OnDestroy()
		{
			if (_material != null)
			{
				Destroy(_material);
			}
		}

		public void SetHeat(float heat)
		{
			_heat = Mathf.Clamp01(heat);
			if (_material != null)
			{
				_material.SetFloat(r_heatId, _heat);
			}
		}

		// Call after renderers are added or removed at runtime.
		public void CollectRenderers()
		{
			r_drawnRenderers.Clear();
			r_subMeshCounts.Clear();

			Renderer[] renderers = _renderers != null && _renderers.Length > 0 ? _renderers : GetComponentsInChildren<Renderer>(true);
			foreach (Renderer candidate in renderers)
			{
				int subMeshCount = GetSubMeshCount(candidate);
				if (subMeshCount <= 0)
				{
					continue;
				}

				r_drawnRenderers.Add(candidate);
				r_subMeshCounts.Add(subMeshCount);
			}
		}

		internal void Draw(RasterCommandBuffer commandBuffer)
		{
			if (_material == null || _heat <= 0f)
			{
				return;
			}

			for (int index = 0; index < r_drawnRenderers.Count; index++)
			{
				Renderer drawn = r_drawnRenderers[index];
				if (drawn == null || !drawn.enabled || !drawn.gameObject.activeInHierarchy)
				{
					continue;
				}

				for (int subMesh = 0; subMesh < r_subMeshCounts[index]; subMesh++)
				{
					commandBuffer.DrawRenderer(drawn, _material, subMesh, 0);
				}
			}
		}

		private static int GetSubMeshCount(Renderer candidate)
		{
			switch (candidate)
			{
				case SkinnedMeshRenderer skinned:
					return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 0;
				case MeshRenderer:
					return candidate.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 0;
				default:
					return 0;
			}
		}
	}
}
