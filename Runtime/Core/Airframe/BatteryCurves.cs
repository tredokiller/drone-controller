using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	// Resting (open-circuit) cell voltage by state of charge, sampled every 10% from 0% to 100%.
	public static class BatteryCurves
	{
		private static readonly float[] s_liPoCellVoltage = { 3.30f, 3.60f, 3.70f, 3.75f, 3.79f, 3.82f, 3.87f, 3.92f, 4.00f, 4.08f, 4.20f };
		private static readonly float[] s_liIonCellVoltage = { 2.80f, 3.30f, 3.45f, 3.55f, 3.62f, 3.68f, 3.76f, 3.85f, 3.95f, 4.06f, 4.20f };

		public static float NominalCellVoltage(BatteryChemistry chemistry) => chemistry == BatteryChemistry.LiIon ? 3.6f : 3.7f;

		public static float OpenCircuitCellVoltage(BatteryChemistry chemistry, float stateOfCharge)
		{
			float[] table = chemistry == BatteryChemistry.LiIon ? s_liIonCellVoltage : s_liPoCellVoltage;
			float position = Mathf.Clamp01(stateOfCharge) * (table.Length - 1);
			int index = Mathf.Min((int)position, table.Length - 2);
			return Mathf.Lerp(table[index], table[index + 1], position - index);
		}
	}
}
