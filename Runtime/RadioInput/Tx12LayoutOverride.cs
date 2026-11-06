using UnityEngine;
using UnityEngine.InputSystem;

namespace UForce.FpvFlight.RadioInput
{
	// Ported from Interceptor. The TX12 declares its last two axes with the same HID usage (Slider), and Unity gives
	// both the report offset of channel 8, so channel 7 (bytes 16-17, switch B) ends up without a control.
	public static class Tx12LayoutOverride
	{
		private const string LAYOUT_JSON = @"{
			""name"": ""Tx12SliderOffsetOverride"",
			""extend"": ""HID::OpenTX Radiomaster TX12 Joystick"",
			""controls"": [{ ""name"": ""slider1"", ""offset"": 16 }]
		}";

		private static bool s_isRegistered;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#endif
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Register()
		{
			if (s_isRegistered)
			{
				return;
			}

			s_isRegistered = true;
			InputSystem.RegisterLayoutOverride(LAYOUT_JSON);
		}
	}
}
