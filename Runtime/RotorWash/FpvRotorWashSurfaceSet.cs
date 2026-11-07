using System;
using UnityEngine;

namespace UForce.FpvFlight.RotorWash
{
	// Dust per terrain layer for a world (Interceptor's DroneRotorWash ground dust). Layers not listed use the default.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Rotor Wash Surface Set", fileName = "RotorWashSurfaceSet")]
	public sealed class FpvRotorWashSurfaceSet : ScriptableObject
	{
		[SerializeField] private FpvRotorWashSurface[] _surfaces = Array.Empty<FpvRotorWashSurface>();
		[SerializeField] private FpvRotorWashSurface _default = new(null, new Color(0.55f, 0.5f, 0.42f), 0.6f);
		[SerializeField] private Color _snowDust = new(0.92f, 0.94f, 0.97f);
		[Tooltip("Water mist and spray over water: colour and amount.")]
		[SerializeField] private Color _waterSpray = new(0.86f, 0.89f, 0.92f);
		[SerializeField, Range(0f, 1f)] private float _waterSprayAmount = 0.45f;
		[Tooltip("Water mist is finer and more transparent than ground dust.")]
		[SerializeField, Range(0.1f, 1f)] private float _waterSpraySize = 0.7f;

		public FpvRotorWashSurface Default => _default;
		public Color SnowDust => _snowDust;
		public Color WaterSpray => _waterSpray;
		public float WaterSprayAmount => _waterSprayAmount;
		public float WaterSpraySize => _waterSpraySize;

		public FpvRotorWashSurface Find(TerrainLayer layer)
		{
			for (int index = 0; index < _surfaces.Length; index++)
			{
				if (_surfaces[index].Layer == layer)
				{
					return _surfaces[index];
				}
			}

			return _default;
		}

		public void SetSurfaces(FpvRotorWashSurface[] surfaces, FpvRotorWashSurface fallback)
		{
			_surfaces = surfaces ?? Array.Empty<FpvRotorWashSurface>();
			_default = fallback;
		}
	}
}
