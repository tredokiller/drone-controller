using UnityEngine;

namespace UForce.FpvFlight
{
	// What motor sound, propellers and rotor wash need from a drone. FpvFlightController implements it on the pilot's
	// machine; a network drone on another machine can implement it from synced motor power.
	public interface IFpvMotorSource
	{
		public bool IsArmed { get; }
		// Lagged motor output 0..1, idle included.
		public float MotorOutput { get; }
		public Vector3 Velocity { get; }
	}
}
