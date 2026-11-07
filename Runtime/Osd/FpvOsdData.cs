using UForce.FpvFlight.Rc;

namespace UForce.FpvFlight.Osd
{
	// What the FPV OSD shows. Filled by an IFpvOsdDataSource every frame.
	public struct FpvOsdData
	{
		public bool IsArmed;
		public FlightMode Mode;
		public FailsafeStage FailsafeStage;
		public ArmingBlockReason ArmingBlockReason;

		public float CellVoltage;
		public float PackVoltage;
		public float Current;
		public float UsedMah;
		public float BatteryCharge;

		// 0..1 video link quality (VTX) and control link quality (RSSI / LQ).
		public float VideoSignal;
		public float LinkQuality;

		public float Altitude;
		public float SpeedKmh;
		public float VerticalSpeed;
		// Degrees clockwise from world +Z (north), 0..360.
		public float Heading;
		public float Throttle;
		// Seconds armed since the last arming.
		public float FlightTime;
		// Body attitude in degrees: pitch positive nose up, roll positive right side down.
		public float Pitch;
		public float Roll;

		// Project payload (warhead, bomb): shown when HasPayload is true.
		public bool HasPayload;
		public bool IsPayloadArmed;
	}
}
