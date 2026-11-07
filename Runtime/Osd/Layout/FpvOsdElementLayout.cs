using System;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// One OSD element of a layout: whether it is shown and its anchor in the OSD canvas (0..1, origin bottom left). The
	// element keeps its pixel offset from the anchor, so moving the anchor moves the element (FPV Defence Simulator).
	[Serializable]
	public struct FpvOsdElementLayout
	{
		[SerializeField] private string _id;
		[SerializeField] private bool _isVisible;
		[SerializeField] private Vector2 _anchor;

		public FpvOsdElementLayout(string id, bool isVisible, Vector2 anchor)
		{
			_id = id;
			_isVisible = isVisible;
			_anchor = anchor;
		}

		public string Id => _id;

		public bool IsVisible
		{
			get => _isVisible;
			set => _isVisible = value;
		}

		public Vector2 Anchor
		{
			get => _anchor;
			set => _anchor = value;
		}
	}
}
