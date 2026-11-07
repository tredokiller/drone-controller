using System.Collections.Generic;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Where a dragged OSD element lands (OperatorOsdEditorView of FPV Defence Simulator). Positions are normalized to the
	// OSD canvas with the origin bottom left. With snapping, an edge or the centre of the element jumps to an edge or
	// centre of the frame, of the other shown elements or of any element's standard position when it is closer than the
	// snap distance, and the element's own standard position wins first. The element never leaves the frame.
	public static class FpvOsdSnapping
	{
		public const float SNAP_DISTANCE_PIXELS = 10f;

		private static readonly Rect s_frame = new(0f, 0f, 1f, 1f);

		// The frame, the standard bounds of every movable element and the bounds of the other shown elements.
		public static List<Rect> CollectGuides(IReadOnlyList<FpvOsdEditorElement> elements, string draggedId)
		{
			var guides = new List<Rect> { s_frame };
			foreach (FpvOsdEditorElement element in elements)
			{
				if (!element.Info.IsMovable)
				{
					continue;
				}

				guides.Add(element.DefaultBounds);
				if (element.Info.Id != draggedId && element.IsShown)
				{
					guides.Add(element.Bounds);
				}
			}

			return guides;
		}

		// The new bounds minimum for a drag that started at startBounds and moved by delta (normalized).
		public static Vector2 Place(Rect startBounds, Vector2 delta, Rect home, IReadOnlyList<Rect> guides, Vector2 framePixels, bool isSnapping)
		{
			return new Vector2(
				PlaceAxis(startBounds.xMin + delta.x, startBounds.width, 0, home, guides, framePixels.x, isSnapping),
				PlaceAxis(startBounds.yMin + delta.y, startBounds.height, 1, home, guides, framePixels.y, isSnapping));
		}

		public static float PlaceAxis(float min, float size, int axis, Rect home, IReadOnlyList<Rect> guides, float framePixels, bool isSnapping)
		{
			if (isSnapping && framePixels > 0f)
			{
				min = Snap(min, size, axis, home, guides, SNAP_DISTANCE_PIXELS / framePixels);
			}

			return Mathf.Clamp(min, 0f, Mathf.Max(0f, 1f - size));
		}

		private static float Snap(float min, float size, int axis, Rect home, IReadOnlyList<Rect> guides, float threshold)
		{
			float homeMin = home.min[axis];
			if (Mathf.Abs(min - homeMin) < threshold)
			{
				return homeMin;
			}

			float best = threshold;
			if (guides != null)
			{
				foreach (Rect guide in guides)
				{
					best = Closer(best, guide.min[axis] - min);
					best = Closer(best, guide.center[axis] - (min + size * 0.5f));
					best = Closer(best, guide.max[axis] - (min + size));
				}
			}

			return Mathf.Abs(best) < threshold ? min + best : min;
		}

		private static float Closer(float best, float offset) => Mathf.Abs(offset) < Mathf.Abs(best) ? offset : best;
	}
}
