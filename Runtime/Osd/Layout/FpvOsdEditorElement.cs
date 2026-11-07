using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// An element as an OSD editor shows it: its catalog entry, its state in the draft and where it is drawn
	// (normalized to the OSD canvas, origin bottom left).
	public readonly struct FpvOsdEditorElement
	{
		public FpvOsdEditorElement(FpvOsdElementInfo info, bool isVisible, bool isShown, Rect bounds, Rect defaultBounds)
		{
			Info = info;
			IsVisible = isVisible;
			IsShown = isShown;
			Bounds = bounds;
			DefaultBounds = defaultBounds;
		}

		public FpvOsdElementInfo Info { get; }
		// Turned on in the draft.
		public bool IsVisible { get; }
		// Drawn now: turned on and the OSD is not off.
		public bool IsShown { get; }
		public Rect Bounds { get; }
		// Where the element is drawn at its standard position.
		public Rect DefaultBounds { get; }
	}
}
