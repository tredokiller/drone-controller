namespace UForce.FpvFlight.RadioInput
{
	public interface IRcCalibrationStore
	{
		public bool TryLoad(string deviceKey, out RcCalibration calibration);
		public void Save(RcCalibration calibration);
		public void Delete(string deviceKey);
	}
}
