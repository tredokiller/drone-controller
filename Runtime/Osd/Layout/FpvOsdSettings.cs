using System;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// The pilot's OSD choice: the mode (standard, own layout or off) and the own layout.
	[Serializable]
	public sealed class FpvOsdSettings
	{
		[SerializeField] private FpvOsdMode _mode = FpvOsdMode.Main;
		[SerializeField] private FpvOsdLayout _customLayout = new();

		public FpvOsdMode Mode
		{
			get => _mode;
			set => _mode = value;
		}

		public FpvOsdLayout CustomLayout
		{
			get => _customLayout ??= new FpvOsdLayout();
			set => _customLayout = value ?? new FpvOsdLayout();
		}

		public FpvOsdSettings Clone() => new() { _mode = _mode, _customLayout = CustomLayout.Clone() };

		public string ToJson() => JsonUtility.ToJson(this);

		public static bool TryFromJson(string json, out FpvOsdSettings settings)
		{
			settings = null;
			if (string.IsNullOrEmpty(json))
			{
				return false;
			}

			try
			{
				settings = JsonUtility.FromJson<FpvOsdSettings>(json);
			}
			catch (ArgumentException)
			{
				settings = null;
			}

			return settings != null;
		}
	}
}
