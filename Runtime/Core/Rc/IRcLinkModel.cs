namespace UForce.FpvFlight.Rc
{
	// Implemented by the project's radio model. The flight core samples it once per physics step.
	public interface IRcLinkModel
	{
		public RcLinkState Sample();
	}
}
