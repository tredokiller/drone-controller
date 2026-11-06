using UnityEngine;
using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	// Radio calibration and debug-device settings as config. A per-device calibration saved by the calibration
	// wizard overrides the default calibration here.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/RC Calibration Profile", fileName = "RcCalibrationProfile")]
	public sealed class RcCalibrationProfile : ScriptableObject
	{
		[SerializeField] private RcCalibration _calibration = new();
		[SerializeField] private RcDebugInputSettings _debugInput = new();

		public RcCalibration Calibration => _calibration;
		public RcDebugInputSettings DebugInput => _debugInput;

		public RcCalibration Resolve(InputDevice device, IRcCalibrationStore store)
		{
			if (store != null && device != null && store.TryLoad(RcDevices.GetDeviceKey(device), out RcCalibration saved))
			{
				return saved;
			}

			return _calibration.Clone();
		}

		public void SetCalibration(RcCalibration calibration)
		{
			_calibration = calibration != null ? calibration.Clone() : new RcCalibration();
		}
	}
}
