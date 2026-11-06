namespace UForce.FpvFlight.Rc
{
	public enum FailsafeStage
	{
		None = 0,
		// Rx loss: roll, pitch and yaw are centered and throttle follows the stage 1 setting.
		StageOne = 1,
		// Drop: the drone is disarmed and falls. Re-arming needs the arm switch cycled.
		StageTwo = 2
	}
}
