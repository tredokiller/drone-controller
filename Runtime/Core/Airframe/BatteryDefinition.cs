using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Battery Definition", fileName = "BatteryDefinition")]
	public sealed class BatteryDefinition : ScriptableObject
	{
		[SerializeField] private BatteryChemistry _chemistry = BatteryChemistry.LiPo;
		[SerializeField, Range(1, 12)] private int _cellCount = 6;
		[SerializeField, Min(50f)] private float _capacityMah = 2200f;
		[SerializeField, Min(0.01f)] private float _mass = 0.33f;
		[Tooltip("Internal resistance of one series cell group, Ω. LiPo packs ≈ 0.003–0.008, Li-ion 1P ≈ 0.012–0.02.")]
		[SerializeField, Min(0f)] private float _cellInternalResistance = 0.005f;

		public BatteryChemistry Chemistry => _chemistry;
		public int CellCount => _cellCount;
		public float CapacityMah => _capacityMah;
		public float Mass => _mass;
		public float CellInternalResistance => _cellInternalResistance;

		public float NominalVoltage => _cellCount * BatteryCurves.NominalCellVoltage(_chemistry);
		// AirframeDefinition.MaxThrust is measured at this voltage: a full pack with no load.
		public float ReferenceVoltage => OpenCircuitVoltage(1f);
		public float PackResistance => _cellCount * _cellInternalResistance;

		public float OpenCircuitVoltage(float stateOfCharge) => _cellCount * BatteryCurves.OpenCircuitCellVoltage(_chemistry, stateOfCharge);

		public void Configure(BatteryChemistry chemistry, int cellCount, float capacityMah, float mass, float cellInternalResistance)
		{
			_chemistry = chemistry;
			_cellCount = Mathf.Clamp(cellCount, 1, 12);
			_capacityMah = Mathf.Max(50f, capacityMah);
			_mass = Mathf.Max(0.01f, mass);
			_cellInternalResistance = Mathf.Max(0f, cellInternalResistance);
		}
	}
}
