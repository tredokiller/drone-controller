namespace UForce.FpvFlight.Osd
{
	public enum FpvOsdMode
	{
		// The interceptor's FPV OSD (MainOsdPanel of Interceptor Drones) as the prefab lays it out.
		Main = 0,
		Off = 1,
		// The pilot's own layout (FpvOsdSettings.CustomLayout), edited with FpvOsdEditorSession.
		Custom = 2
	}
}
