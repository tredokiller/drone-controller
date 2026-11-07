using UnityEngine;

namespace UForce.FpvFlight.Video
{
	public struct FpvLensDrop
	{
		public Vector2 Position;
		public float Radius;
		public float Age;
		public float Life;
		public float Seed;
		public float Irregular;
		public float Veil;
		public float Blur;
		// Snow in the drop: 1 is a flake, 0 is water (melted or rain); it melts over Melt seconds.
		public float Frozen;
		public float Melt;
		public float Pattern;
	}
}
