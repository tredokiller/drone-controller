namespace UForce.FpvFlight.RadioInput
{
	// Radio switches and buttons. Default Radiomaster TX12 layout as in Interceptor Drones: Arm SE, PayloadArm SF,
	// PayloadDetonate SD (button), CameraTilt SC (3 positions), FlightMode SB (3 positions), Thermal SA (button).
	public enum RcSwitchChannel
	{
		Arm = 0,
		FlightMode = 1,
		PayloadArm = 2,
		PayloadDetonate = 3,
		CameraTilt = 4,
		Thermal = 5
	}
}
