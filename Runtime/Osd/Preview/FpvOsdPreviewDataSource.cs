using UForce.FpvFlight.Rc;

namespace UForce.FpvFlight.Osd
{
	// Fixed, plausible telemetry for an OSD preview: every element shows a typical value, and the values do not change,
	// so the elements keep their size while a pilot arranges them.
	public sealed class FpvOsdPreviewDataSource : IFpvOsdDataSource
	{
		public bool TryGetData(out FpvOsdData data)
		{
			data = new FpvOsdData
			{
				IsArmed = true,
				Mode = FlightMode.Acro,
				FailsafeStage = FailsafeStage.None,
				CellVoltage = 3.92f,
				PackVoltage = 15.68f,
				Current = 18.4f,
				UsedMah = 420f,
				BatteryCharge = 0.72f,
				VideoSignal = 0.86f,
				LinkQuality = 0.98f,
				Altitude = 42f,
				SpeedKmh = 86f,
				VerticalSpeed = 1.2f,
				Heading = 137f,
				Throttle = 0.46f,
				FlightTime = 83f,
				// The preview has no camera: this attitude puts the horizon below the reticle, as with a camera uptilt.
				Pitch = 9f,
				Roll = 6f,
				HasPayload = true,
				IsPayloadArmed = false
			};

			return true;
		}
	}
}
