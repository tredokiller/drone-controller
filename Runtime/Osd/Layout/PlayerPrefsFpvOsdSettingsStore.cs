using System;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Stores the OSD settings as JSON in PlayerPrefs. FpvOsdView uses Default unless it is given another store, so a
	// layout saved through Default reaches every OSD that is already shown.
	public sealed class PlayerPrefsFpvOsdSettingsStore : IFpvOsdSettingsStore
	{
		public const string DEFAULT_KEY = "UForce.FpvFlight.OsdSettings";

		private readonly string r_key;

		public static PlayerPrefsFpvOsdSettingsStore Default { get; } = new();

		public event Action<FpvOsdSettings> Changed;

		public PlayerPrefsFpvOsdSettingsStore(string key = DEFAULT_KEY)
		{
			r_key = string.IsNullOrEmpty(key) ? DEFAULT_KEY : key;
		}

		public FpvOsdSettings Load()
		{
			return FpvOsdSettings.TryFromJson(PlayerPrefs.GetString(r_key, string.Empty), out FpvOsdSettings settings)
				? settings
				: new FpvOsdSettings();
		}

		public void Save(FpvOsdSettings settings)
		{
			if (settings == null)
			{
				return;
			}

			PlayerPrefs.SetString(r_key, settings.ToJson());
			PlayerPrefs.Save();
			Changed?.Invoke(settings.Clone());
		}
	}
}
