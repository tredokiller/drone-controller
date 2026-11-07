using UForce.FpvFlight.Airframe;
using UForce.FpvFlight.Control;
using UForce.FpvFlight.Interceptor;
using UForce.FpvFlight.Rates;
using UForce.FpvFlight.Visuals;
using UnityEngine;

namespace UForce.FpvFlight
{
	// One kind of drone: its flight assets, camera rig and prefab. Interceptor Drones' P1-SUN, Zorelit v5, Sting and SPYS
	// share the interceptor prefab and differ in physics and battery; the strike FPV has its own model and a fixed uptilt.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/Drone Type", fileName = "DroneType")]
	public sealed class FpvDroneType : ScriptableObject
	{
		[SerializeField] private string _id = string.Empty;
		[SerializeField] private string _displayName = string.Empty;
		[SerializeField, TextArea] private string _description = string.Empty;
		[SerializeField] private Sprite _preview;

		[Header("Flight")]
		[SerializeField] private AirframeDefinition _airframe;
		[SerializeField] private BatteryDefinition _battery;
		[SerializeField] private RatesProfile _rates;
		[SerializeField] private FlightControllerProfile _profile;
		[Tooltip("Optional. Flies with Interceptor Drones' flight model and this preset instead of the Betaflight physics.")]
		[SerializeField] private InterceptorFlightProfile _interceptorFlight;

		[Header("Camera")]
		[Tooltip("Uptilt per camera switch position, degrees above the body's forward axis. One value: fixed uptilt.")]
		[SerializeField] private float[] _cameraUptilts = { 25f };
		[SerializeField, Range(0, 2)] private int _cameraStartPosition;
		[SerializeField, Range(20f, 150f)] private float _cameraFieldOfView = 90f;

		[Header("Prefab")]
		[Tooltip("Playable drone with model, camera, video, OSD, sound and rotor wash.")]
		[SerializeField] private GameObject _prefab;

		public string Id => _id;
		public string DisplayName => _displayName;
		public string Description => _description;
		public Sprite Preview => _preview;
		public AirframeDefinition Airframe => _airframe;
		public BatteryDefinition Battery => _battery;
		public RatesProfile Rates => _rates;
		public FlightControllerProfile Profile => _profile;
		public InterceptorFlightProfile InterceptorFlight => _interceptorFlight;
		public float[] CameraUptilts => _cameraUptilts;
		public int CameraStartPosition => _cameraStartPosition;
		public float CameraFieldOfView => _cameraFieldOfView;
		public bool HasCameraSwitch => _cameraUptilts != null && _cameraUptilts.Length > 1;
		public GameObject Prefab => _prefab;

		// Gives a drone this type's flight assets and camera rig, for prefabs shared by several types.
		public void Apply(FpvFlightController controller)
		{
			if (controller)
			{
				controller.Configure(_airframe, _battery, _rates, _profile, _interceptorFlight);
			}

			FpvCameraTilt tilt = controller ? controller.GetComponentInChildren<FpvCameraTilt>(true) : null;
			if (tilt)
			{
				tilt.SetAngles(_cameraUptilts, _cameraStartPosition);
				if (tilt.TryGetComponent(out Camera camera))
				{
					camera.fieldOfView = _cameraFieldOfView;
				}
			}
		}

		public void Configure(string id, string displayName, string description, AirframeDefinition airframe, BatteryDefinition battery, RatesProfile rates,
			FlightControllerProfile profile, float[] cameraUptilts, int cameraStartPosition, float cameraFieldOfView, GameObject prefab)
		{
			_id = id;
			_displayName = displayName;
			_description = description;
			_airframe = airframe;
			_battery = battery;
			_rates = rates;
			_profile = profile;
			_cameraUptilts = cameraUptilts;
			_cameraStartPosition = cameraStartPosition;
			_cameraFieldOfView = cameraFieldOfView;
			_prefab = prefab;
		}

		public void SetInterceptorFlight(InterceptorFlightProfile interceptorFlight) => _interceptorFlight = interceptorFlight;
	}
}
