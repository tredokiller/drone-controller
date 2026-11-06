using UnityEngine;

namespace UForce.FpvFlight.Rc
{
	public readonly struct RcLinkState
	{
		public readonly float Quality;
		public readonly float LatencySeconds;
		public readonly float PacketLossProbability;
		public readonly bool IsLost;

		public RcLinkState(float quality, float latencySeconds, float packetLossProbability, bool isLost)
		{
			Quality = Mathf.Clamp01(quality);
			LatencySeconds = Mathf.Max(0f, latencySeconds);
			PacketLossProbability = Mathf.Clamp01(packetLossProbability);
			IsLost = isLost;
		}

		public static RcLinkState Ideal => new(1f, 0f, 0f, false);

		public static RcLinkState Lost => new(0f, 0f, 1f, true);
	}
}
