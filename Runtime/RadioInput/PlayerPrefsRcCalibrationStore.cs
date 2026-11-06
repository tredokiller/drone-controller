using UnityEngine;

namespace UForce.FpvFlight.RadioInput
{
	// Stores one calibration per radio as JSON in PlayerPrefs.
	public sealed class PlayerPrefsRcCalibrationStore : IRcCalibrationStore
	{
		private const string DEFAULT_KEY_PREFIX = "UForce.FpvFlight.RcCalibration.";

		private readonly string r_keyPrefix;

		public PlayerPrefsRcCalibrationStore(string keyPrefix = DEFAULT_KEY_PREFIX)
		{
			r_keyPrefix = string.IsNullOrEmpty(keyPrefix) ? DEFAULT_KEY_PREFIX : keyPrefix;
		}

		public bool TryLoad(string deviceKey, out RcCalibration calibration)
		{
			calibration = null;
			if (string.IsNullOrEmpty(deviceKey))
			{
				return false;
			}

			string json = PlayerPrefs.GetString(r_keyPrefix + deviceKey, string.Empty);
			if (string.IsNullOrEmpty(json))
			{
				return false;
			}

			calibration = JsonUtility.FromJson<RcCalibration>(json);
			return calibration != null;
		}

		public void Save(RcCalibration calibration)
		{
			if (calibration == null || string.IsNullOrEmpty(calibration.DeviceKey))
			{
				Debug.LogWarning($"[{nameof(PlayerPrefsRcCalibrationStore)}] A calibration without a device key cannot be saved.");
				return;
			}

			PlayerPrefs.SetString(r_keyPrefix + calibration.DeviceKey, JsonUtility.ToJson(calibration));
			PlayerPrefs.Save();
		}

		public void Delete(string deviceKey)
		{
			if (string.IsNullOrEmpty(deviceKey))
			{
				return;
			}

			PlayerPrefs.DeleteKey(r_keyPrefix + deviceKey);
			PlayerPrefs.Save();
		}
	}
}
