using System.Collections.Generic;

namespace UForce.FpvFlight.Osd
{
	public enum FpvOsdElementGroup
	{
		// The interceptor OSD, shown by default.
		Main = 0,
		// Betaflight-style telemetry, hidden until the pilot turns it on.
		Extra = 1
	}

	public readonly struct FpvOsdElementInfo
	{
		public FpvOsdElementInfo(string id, FpvOsdElementGroup group, string label, bool isMovable)
		{
			Id = id;
			Group = group;
			Label = label;
			IsMovable = isMovable;
		}

		// The element's object name under MainOsdPanel.
		public string Id { get; }
		public FpvOsdElementGroup Group { get; }
		// Ukrainian, as in the OSD editor of FPV Defence Simulator.
		public string Label { get; }
		// The horizon and the reticle stay centred; they can only be hidden.
		public bool IsMovable { get; }
	}

	// The OSD elements a pilot can show, hide and move, in editor order.
	public static class FpvOsdElementCatalog
	{
		public const string MAIN_GROUP_LABEL = "ГОЛОВНЕ";
		public const string EXTRA_GROUP_LABEL = "ДОДАТКОВО";

		public const string VTX_SIGNAL = "VtxSignal";
		public const string ARM_STATUS = "ArmStatus";
		public const string BOMB_STATUS = "BombStatus";
		public const string FLIGHT_MODE = "FlightMode";
		public const string BATTERY_VOLTAGE = "BatteryVoltage";
		public const string ALTITUDE = "Altitude";
		public const string SPEED = "Speed";
		public const string HEADING = "Heading";
		public const string HORIZON = "Horizon";
		public const string RETICLE = "Reticle";
		public const string WARNING = "Warning";
		public const string THROTTLE = "Throttle";
		public const string FLIGHT_TIME = "FlightTime";
		public const string CURRENT = "Current";
		public const string USED_MAH = "UsedMah";
		public const string PACK_VOLTAGE = "PackVoltage";
		public const string BATTERY_PERCENT = "BatteryPercent";
		public const string LINK_QUALITY = "LinkQuality";
		public const string VERTICAL_SPEED = "VerticalSpeed";
		public const string PITCH_ANGLE = "PitchAngle";
		public const string ROLL_ANGLE = "RollAngle";

		private static readonly FpvOsdElementInfo[] s_elements =
		{
			new(VTX_SIGNAL, FpvOsdElementGroup.Main, "Сигнал VTX", true),
			new(ARM_STATUS, FpvOsdElementGroup.Main, "Стан ARM", true),
			new(BOMB_STATUS, FpvOsdElementGroup.Main, "Стан бомби", true),
			new(FLIGHT_MODE, FpvOsdElementGroup.Main, "Режим польоту", true),
			new(BATTERY_VOLTAGE, FpvOsdElementGroup.Main, "Напруга батареї", true),
			new(ALTITUDE, FpvOsdElementGroup.Main, "Висота", true),
			new(SPEED, FpvOsdElementGroup.Main, "Швидкість", true),
			new(HEADING, FpvOsdElementGroup.Main, "Компас", true),
			new(HORIZON, FpvOsdElementGroup.Main, "Авіагоризонт", false),
			new(RETICLE, FpvOsdElementGroup.Main, "Приціл", false),
			new(WARNING, FpvOsdElementGroup.Main, "Попередження (FAILSAFE)", true),
			new(THROTTLE, FpvOsdElementGroup.Extra, "Газ (THR)", true),
			new(FLIGHT_TIME, FpvOsdElementGroup.Extra, "Час польоту (FLY)", true),
			new(CURRENT, FpvOsdElementGroup.Extra, "Струм (A)", true),
			new(USED_MAH, FpvOsdElementGroup.Extra, "Витрачено ємності (MAH)", true),
			new(PACK_VOLTAGE, FpvOsdElementGroup.Extra, "Напруга збірки (V)", true),
			new(BATTERY_PERCENT, FpvOsdElementGroup.Extra, "Заряд батареї (BAT %)", true),
			new(LINK_QUALITY, FpvOsdElementGroup.Extra, "Якість зв'язку (LQ)", true),
			new(VERTICAL_SPEED, FpvOsdElementGroup.Extra, "Вертикальна швидкість (VS)", true),
			new(PITCH_ANGLE, FpvOsdElementGroup.Extra, "Тангаж (PIT)", true),
			new(ROLL_ANGLE, FpvOsdElementGroup.Extra, "Крен (ROL)", true)
		};

		public static IReadOnlyList<FpvOsdElementInfo> Elements => s_elements;

		public static bool TryGet(string id, out FpvOsdElementInfo info)
		{
			foreach (FpvOsdElementInfo element in s_elements)
			{
				if (element.Id == id)
				{
					info = element;
					return true;
				}
			}

			info = default;
			return false;
		}

		public static string GetGroupLabel(FpvOsdElementGroup group) => group == FpvOsdElementGroup.Main ? MAIN_GROUP_LABEL : EXTRA_GROUP_LABEL;
	}
}
