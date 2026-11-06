using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	public static class RcDevices
	{
		// A radio in USB joystick mode (Radiomaster TX12 and other EdgeTX/OpenTX radios) shows up as a HID joystick.
		public static bool IsRadio(InputDevice device) => device is Joystick;

		public static string GetDeviceKey(InputDevice device) =>
			device == null ? string.Empty : $"{device.description.interfaceName}|{device.description.manufacturer}|{device.description.product}";
	}
}
