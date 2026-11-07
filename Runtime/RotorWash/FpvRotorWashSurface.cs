using System;
using UnityEngine;

namespace UForce.FpvFlight.RotorWash
{
	// Dust raised from one terrain layer: its colour and how much of it there is (0 none, 1 bare dirt).
	[Serializable]
	public struct FpvRotorWashSurface
	{
		[SerializeField] private TerrainLayer _layer;
		[SerializeField] private Color _color;
		[SerializeField, Range(0f, 1f)] private float _amount;

		public FpvRotorWashSurface(TerrainLayer layer, Color color, float amount)
		{
			_layer = layer;
			_color = color;
			_amount = amount;
		}

		public TerrainLayer Layer => _layer;
		public Color Color => _color;
		public float Amount => _amount;
	}
}
