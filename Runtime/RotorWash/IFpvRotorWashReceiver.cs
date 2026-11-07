namespace UForce.FpvFlight.RotorWash
{
	// Something the rotor wash blows on every frame (GeoWorld's leaves and water ripples). Put it on the drone next to FpvRotorWash.
	public interface IFpvRotorWashReceiver
	{
		public void Blow(in FpvRotorWashState state);
	}
}
