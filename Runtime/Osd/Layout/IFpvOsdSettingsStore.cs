using System;

namespace UForce.FpvFlight.Osd
{
	public interface IFpvOsdSettingsStore
	{
		// Raised after Save with a copy of what was saved.
		public event Action<FpvOsdSettings> Changed;

		// What is saved, or the standard OSD when nothing is. Never null; the caller owns the returned copy.
		public FpvOsdSettings Load();
		public void Save(FpvOsdSettings settings);
	}
}
