namespace UForce.FpvFlight.RadioInput
{
	public readonly struct RcCalibrationStep
	{
		private RcCalibrationStep(RcCalibrationStepKind kind, RcAxisChannel axis, RcSwitchChannel switchChannel, bool isOptional)
		{
			Kind = kind;
			Axis = axis;
			Switch = switchChannel;
			IsOptional = isOptional;
		}

		public RcCalibrationStepKind Kind { get; }
		public RcAxisChannel Axis { get; }
		public RcSwitchChannel Switch { get; }
		public bool IsOptional { get; }

		public static RcCalibrationStep ForAxis(RcAxisChannel axis) => new(RcCalibrationStepKind.Axis, axis, default, false);

		public static RcCalibrationStep ForSwitch(RcSwitchChannel switchChannel, bool isOptional) =>
			new(RcCalibrationStepKind.Switch, default, switchChannel, isOptional);

		public static RcCalibrationStep Of(RcCalibrationStepKind kind) => new(kind, default, default, false);
	}
}
