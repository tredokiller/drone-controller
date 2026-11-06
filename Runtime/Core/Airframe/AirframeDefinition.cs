using UnityEngine;

namespace UForce.FpvFlight.Airframe
{
	// Physical description of a drone type. Hover throttle, top speed and flight time are derived from these
	// values (see AirframeMath and the inspector); they are never separate settings.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Airframe Definition", fileName = "AirframeDefinition")]
	public sealed class AirframeDefinition : ScriptableObject
	{
		[Header("Mass and shape")]
		[Tooltip("Frame with motors, props, electronics and camera. Battery and payload are added on top.")]
		[SerializeField, Min(0.05f)] private float _frameMass = 0.75f;
		[Tooltip("Box used for the inertia tensor (width, height, length), m.")]
		[SerializeField] private Vector3 _bodySize = new(0.32f, 0.1f, 0.32f);
		[SerializeField] private Vector3 _centerOfMass = Vector3.zero;

		[Header("Propulsion")]
		[Tooltip("Total static thrust of all motors at full output on a fully charged, unloaded battery, N. Voltage sag lowers it in flight.")]
		[SerializeField, Min(0.1f)] private float _maxThrust = 42f;
		[Tooltip("0 = thrust grows with outputÂ², 1 = linear.")]
		[SerializeField, Range(0f, 1f)] private float _thrustLinearity = 0.5f;
		[Tooltip("Motor output while armed at zero throttle (Betaflight dshot_idle_value 5.5%).")]
		[SerializeField, Range(0f, 0.2f)] private float _motorIdle = 0.055f;
		[Tooltip("First-order motor response time constant, s.")]
		[SerializeField, Min(0.001f)] private float _motorTimeConstant = 0.03f;
		[SerializeField, Min(1)] private int _motorCount = 4;
		[SerializeField, Min(0.02f)] private float _propDiameter = 0.178f;
		[Tooltip("Ideal induced power divided by electrical power (figure of merit Ã— motor and ESC efficiency).")]
		[SerializeField, Range(0.1f, 1f)] private float _propulsionEfficiency = 0.45f;
		[Tooltip("Flight controller, receiver, VTX and camera current, A.")]
		[SerializeField, Min(0f)] private float _avionicsCurrent = 0.8f;

		[Header("Aerodynamics")]
		[Tooltip("Drag coefficient Ã— area per body axis (side x, top y, front z), mÂ².")]
		[SerializeField] private Vector3 _dragArea = new(0.035f, 0.0345f, 0.03f);
		[Tooltip("Aerodynamic angular damping, 1/s.")]
		[SerializeField, Min(0f)] private float _angularDrag = 0.5f;

		[Header("Attitude authority")]
		[Tooltip("Maximum angular acceleration without payload (pitch x, yaw y, roll z), deg/sÂ².")]
		[SerializeField] private Vector3 _maxAngularAcceleration = new(6000f, 2000f, 7000f);

		public float FrameMass => _frameMass;
		public Vector3 BodySize => _bodySize;
		public Vector3 CenterOfMass => _centerOfMass;
		public float MaxThrust => _maxThrust;
		public float ThrustLinearity => _thrustLinearity;
		public float MotorIdle => _motorIdle;
		public float MotorTimeConstant => _motorTimeConstant;
		public int MotorCount => _motorCount;
		public float PropDiameter => _propDiameter;
		public float PropulsionEfficiency => _propulsionEfficiency;
		public float AvionicsCurrent => _avionicsCurrent;
		public Vector3 DragArea => _dragArea;
		public float AngularDrag => _angularDrag;
		public Vector3 MaxAngularAcceleration => _maxAngularAcceleration;

		public float DiskArea => _motorCount * Mathf.PI * 0.25f * _propDiameter * _propDiameter;

		public void ConfigureMass(float frameMass, Vector3 bodySize, Vector3 centerOfMass)
		{
			_frameMass = Mathf.Max(0.05f, frameMass);
			_bodySize = bodySize;
			_centerOfMass = centerOfMass;
		}

		public void ConfigurePropulsion(float maxThrust, float thrustLinearity, float motorIdle, float motorTimeConstant, int motorCount, float propDiameter, float propulsionEfficiency, float avionicsCurrent)
		{
			_maxThrust = Mathf.Max(0.1f, maxThrust);
			_thrustLinearity = Mathf.Clamp01(thrustLinearity);
			_motorIdle = Mathf.Clamp(motorIdle, 0f, 0.2f);
			_motorTimeConstant = Mathf.Max(0.001f, motorTimeConstant);
			_motorCount = Mathf.Max(1, motorCount);
			_propDiameter = Mathf.Max(0.02f, propDiameter);
			_propulsionEfficiency = Mathf.Clamp(propulsionEfficiency, 0.1f, 1f);
			_avionicsCurrent = Mathf.Max(0f, avionicsCurrent);
		}

		public void ConfigureAerodynamics(Vector3 dragArea, float angularDrag, Vector3 maxAngularAcceleration)
		{
			_dragArea = Vector3.Max(dragArea, Vector3.one * 1e-4f);
			_angularDrag = Mathf.Max(0f, angularDrag);
			_maxAngularAcceleration = Vector3.Max(maxAngularAcceleration, Vector3.one);
		}
	}
}
