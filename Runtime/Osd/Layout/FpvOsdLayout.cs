using System;
using System.Collections.Generic;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// A pilot's OSD layout (DroneOsdLayout of FPV Defence Simulator): the elements it names, the OSD opacity and the
	// bold font. Elements it does not name fall back to the prefab's layout when applied; extra elements stay hidden.
	[Serializable]
	public sealed class FpvOsdLayout
	{
		public const float MIN_OPACITY = 0.2f;

		[SerializeField] private List<FpvOsdElementLayout> _elements = new();
		[SerializeField] private float _opacity = 1f;
		[SerializeField] private bool _boldFont;

		public IReadOnlyList<FpvOsdElementLayout> Elements => _elements;
		public bool IsEmpty => _elements.Count == 0;

		public float Opacity
		{
			get => Mathf.Clamp(_opacity, MIN_OPACITY, 1f);
			set => _opacity = Mathf.Clamp(value, MIN_OPACITY, 1f);
		}

		public bool BoldFont
		{
			get => _boldFont;
			set => _boldFont = value;
		}

		public bool TryGetElement(string id, out FpvOsdElementLayout element)
		{
			int index = IndexOf(id);
			element = index >= 0 ? _elements[index] : default;
			return index >= 0;
		}

		// Replaces the element with the same id or adds it.
		public void SetElement(FpvOsdElementLayout element)
		{
			if (string.IsNullOrEmpty(element.Id))
			{
				return;
			}

			int index = IndexOf(element.Id);
			if (index >= 0)
			{
				_elements[index] = element;
			}
			else
			{
				_elements.Add(element);
			}
		}

		public void Clear()
		{
			_elements.Clear();
			_opacity = 1f;
			_boldFont = false;
		}

		public FpvOsdLayout Clone()
		{
			var copy = new FpvOsdLayout { _opacity = _opacity, _boldFont = _boldFont };
			copy._elements.AddRange(_elements);
			return copy;
		}

		private int IndexOf(string id) => _elements.FindIndex(element => element.Id == id);
	}
}
