using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	// Collective motor output with first-order response lag.
	public sealed class MotorModel
	{
		public float Output { get; private set; }
		public float Command { get; private set; }

		public void Reset()
		{
			Output = 0f;
			Command = 0f;
		}

		public void Update(float command, float deltaTime, float timeConstant)
		{
			Command = Mathf.Clamp01(command);
			float blend = 1f - Mathf.Exp(-deltaTime / Mathf.Max(timeConstant, 1e-4f));
			Output += (Command - Output) * blend;
		}
	}
}
