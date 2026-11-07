namespace UForce.FpvFlight.Osd
{
	public interface IFpvOsdDataSource
	{
		public bool TryGetData(out FpvOsdData data);
	}
}
