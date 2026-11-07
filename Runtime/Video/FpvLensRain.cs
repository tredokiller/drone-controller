using UnityEngine;

namespace UForce.FpvFlight.Video
{
	// Water and snow on the FPV lens, ported from Interceptor Drones (DroneLensRain). The camera is focused far away and
	// the water lies on the lens, so a drop is seen as a soft disc with a milky veil and a large drop or water film as an
	// irregular blur spot. Drops land at random when the rain flies into the lens relative to the camera (fall speed,
	// wind and the oncoming flow of the drone), hold, slowly fade and spread, and at speed drift to the frame edge.
	// In snow, flakes stick to the lens as soft white spots with blurred rays and melt into drops within seconds.
	// Against the sun the water glows; with the sun behind it is barely visible.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(FpvVideoCamera))]
	public sealed class FpvLensRain : MonoBehaviour
	{
		private const int MAX_DROPS = FpvVideoCamera.MAX_LENS_DROPS;

		private static readonly Vector4 r_noSun = new(-10f, -10f, 0f, 0f);

		[Tooltip("Optional. Must implement IFpvWeatherProvider. Without it there is no precipitation and the sun is RenderSettings.sun.")]
		[SerializeField] private MonoBehaviour _weatherProviderBehaviour;
		[Tooltip("Drops per second in heavy rain while hovering with the camera looking straight up.")]
		[SerializeField, Min(0f)] private float _dropsPerSecond = 0.08f;
		[Tooltip("Rain fall speed, m/s.")]
		[SerializeField, Min(0.1f)] private float _rainFallSpeed = 7f;
		[Tooltip("How many times more drops the oncoming flow in flight can give compared to hovering with the camera up.")]
		[SerializeField, Min(1f)] private float _maxFlowFactor = 4f;
		[SerializeField, Range(1, MAX_DROPS)] private int _maxVisible = MAX_DROPS;
		[Tooltip("Radius of a round drop (disc), frame heights.")]
		[SerializeField] private Vector2 _discRadius = new(0.06f, 0.13f);
		[Tooltip("Chance of an irregular blur spot (a large drop, a water film) instead of a disc, and its radius.")]
		[SerializeField, Range(0f, 1f)] private float _spotChance = 0.3f;
		[SerializeField] private Vector2 _spotRadius = new(0.12f, 0.22f);
		[Tooltip("Seconds water stays on the lens.")]
		[SerializeField] private Vector2 _lifetime = new(15f, 35f);
		[Tooltip("Part of the lifetime after which a drop starts to fade.")]
		[SerializeField, Range(0f, 0.95f)] private float _fadeStart = 0.3f;
		[Tooltip("How much a drop spreads over its lifetime.")]
		[SerializeField, Min(0f)] private float _spread = 0.25f;
		[Tooltip("Speed from which the flow blows water to the frame edge, km/h.")]
		[SerializeField, Min(1f)] private float _blowSpeedKmh = 90f;
		[Tooltip("Speed at which the flow blows water off fastest, km/h.")]
		[SerializeField, Min(1f)] private float _maxSpeedKmh = 150f;
		[Tooltip("Seconds of heavy rain until the whole lens is wet.")]
		[SerializeField, Min(0.1f)] private float _wetTime = 30f;
		[Tooltip("Seconds without precipitation until the lens dries.")]
		[SerializeField, Min(0.1f)] private float _dryTime = 40f;

		[Header("Snow")]
		[Tooltip("Flakes per second in heavy snow while hovering with the camera looking straight up.")]
		[SerializeField, Min(0f)] private float _flakesPerSecond = 0.25f;
		[Tooltip("Snow fall speed, m/s.")]
		[SerializeField, Min(0.1f)] private float _snowFallSpeed = 1.2f;
		[SerializeField, Min(1f)] private float _maxSnowFlowFactor = 8f;
		[Tooltip("Flake radius, frame heights.")]
		[SerializeField] private Vector2 _flakeRadius = new(0.025f, 0.06f);
		[Tooltip("Seconds until a flake melts into a drop.")]
		[SerializeField] private Vector2 _meltTime = new(2.5f, 6f);
		[Tooltip("Seconds a drop from a melted flake stays.")]
		[SerializeField] private Vector2 _meltedLifetime = new(6f, 14f);

		private readonly FpvLensDrop[] r_drops = new FpvLensDrop[MAX_DROPS];
		private readonly Vector4[] r_shaderDrops = new Vector4[MAX_DROPS];
		private readonly Vector4[] r_shaderShapes = new Vector4[MAX_DROPS];
		private readonly Vector4[] r_shaderSnow = new Vector4[MAX_DROPS];

		private FpvVideoCamera _video;
		private IFpvWeatherProvider _weatherProvider;
		private FpvBodyVelocityTracker _velocityTracker;
		private int _count;
		private float _wetness;

		public float Wetness => _wetness;

		private void Awake()
		{
			_video = GetComponent<FpvVideoCamera>();
			_weatherProvider ??= _weatherProviderBehaviour as IFpvWeatherProvider;
			_velocityTracker = new FpvBodyVelocityTracker(transform);
		}

		private void OnDisable()
		{
			_count = 0;
			_wetness = 0f;
			_velocityTracker.Reset();
			if (_video != null)
			{
				_video.ClearLensWater();
			}
		}

		public void SetWeatherProvider(IFpvWeatherProvider weatherProvider)
		{
			_weatherProvider = weatherProvider;
		}

		// Debug helpers: a drop or a flake at a random place on the lens.
		public void DropOnLens() => Spawn(new Vector2(Random.Range(0.12f, 0.88f), Random.Range(0.15f, 0.85f)));

		public void FlakeOnLens() => SpawnFlake(new Vector2(Random.Range(0.12f, 0.88f), Random.Range(0.15f, 0.85f)));

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			Camera camera = _video.Camera;
			if (camera == null || !camera.isActiveAndEnabled || deltaTime <= 0f)
			{
				return;
			}

			_velocityTracker.Tick(deltaTime);
			FpvWeatherSample weather = _weatherProvider != null ? _weatherProvider.Sample(transform.position) : FpvWeatherSample.Clear(RenderSettings.sun);

			float aspect = Mathf.Max(0.1f, camera.aspect);
			float speedKmh = _velocityTracker.SpeedKmh;
			float blow = Mathf.Clamp01((speedKmh - _blowSpeedKmh) / Mathf.Max(1f, _maxSpeedKmh - _blowSpeedKmh));

			Vector3 forward = camera.transform.forward;
			float flow = weather.Rain * FlowIntoLens(forward, weather.Wind, _rainFallSpeed, _maxFlowFactor);
			if (_count < _maxVisible && Random.value < _dropsPerSecond * flow * deltaTime)
			{
				Spawn(new Vector2(Random.Range(0.05f, 0.95f), Random.Range(0.08f, 0.92f)));
			}

			float snowFlow = weather.Snow * FlowIntoLens(forward, weather.Wind, _snowFallSpeed, _maxSnowFlowFactor);
			if (_count < _maxVisible && Random.value < _flakesPerSecond * snowFlow * deltaTime)
			{
				SpawnFlake(new Vector2(Random.Range(0.05f, 0.95f), Random.Range(0.08f, 0.92f)));
			}

			// Melted snow wets the lens less than rain.
			bool isDry = weather.Rain < 0.01f && weather.Snow < 0.01f;
			_wetness += (flow + snowFlow * 0.15f) * deltaTime / _wetTime - blow * deltaTime * 0.3f - (isDry ? deltaTime / _dryTime : 0f);
			_wetness = Mathf.Clamp01(_wetness);

			for (int index = _count - 1; index >= 0; index--)
			{
				if (!Step(ref r_drops[index], deltaTime, aspect, blow))
				{
					r_drops[index] = r_drops[--_count];
				}
			}

			Publish();
			PublishLight(camera, weather.Sun);
		}

		// One step of a drop; false when the water left the lens. A flake melts; dry snow is blown off faster than water.
		private static bool Step(ref FpvLensDrop drop, float deltaTime, float aspect, float blow)
		{
			if (drop.Frozen > 0f)
			{
				drop.Frozen = Mathf.Max(0f, drop.Frozen - deltaTime / Mathf.Max(0.1f, drop.Melt));
			}

			if (blow > 0f)
			{
				Vector2 fromCentre = new((drop.Position.x - 0.5f) * aspect, drop.Position.y - 0.5f);
				Vector2 away = fromCentre.sqrMagnitude > 0.0001f ? fromCentre.normalized : Vector2.up;
				Vector2 velocity = away * (blow * 0.03f * (1f + drop.Frozen * 2f));
				drop.Position += new Vector2(velocity.x / aspect, velocity.y) * deltaTime;
			}

			drop.Age += deltaTime * (1f + blow * 0.3f);

			float margin = drop.Radius;
			return drop.Age < drop.Life
				&& drop.Position.x > -margin && drop.Position.x < 1f + margin
				&& drop.Position.y > -margin && drop.Position.y < 1f + margin;
		}

		private void Spawn(Vector2 position)
		{
			if (_count >= MAX_DROPS)
			{
				return;
			}

			bool isSpot = Random.value < _spotChance;
			r_drops[_count++] = new FpvLensDrop
			{
				Position = position,
				Radius = isSpot ? Random.Range(_spotRadius.x, _spotRadius.y) : Random.Range(_discRadius.x, _discRadius.y),
				Life = Random.Range(_lifetime.x, _lifetime.y),
				Seed = Random.Range(0f, 100f),
				Irregular = isSpot ? Random.Range(0.7f, 1f) : 0f,
				Veil = isSpot ? Random.Range(0.05f, 0.15f) : Random.Range(0.3f, 0.5f),
				Blur = isSpot ? Random.Range(0.9f, 1.3f) : Random.Range(0.5f, 0.8f)
			};
		}

		// A flake: smaller than a drop and irregular; once melted it is a round drop.
		private void SpawnFlake(Vector2 position)
		{
			if (_count >= MAX_DROPS)
			{
				return;
			}

			float melt = Random.Range(_meltTime.x, _meltTime.y);
			r_drops[_count++] = new FpvLensDrop
			{
				Position = position,
				Radius = Random.Range(_flakeRadius.x, _flakeRadius.y),
				Life = melt + Random.Range(_meltedLifetime.x, _meltedLifetime.y),
				Seed = Random.Range(0f, 100f),
				Irregular = Random.Range(0.5f, 0.9f),
				Veil = Random.Range(0.3f, 0.45f),
				Blur = Random.Range(0.5f, 0.75f),
				Frozen = 1f,
				Melt = melt,
				Pattern = Random.value
			};
		}

		private void Publish()
		{
			for (int index = 0; index < MAX_DROPS; index++)
			{
				if (index >= _count)
				{
					r_shaderDrops[index] = Vector4.zero;
					r_shaderShapes[index] = Vector4.zero;
					r_shaderSnow[index] = Vector4.zero;
					continue;
				}

				FpvLensDrop drop = r_drops[index];
				float life = drop.Age / drop.Life;
				// A drop appears within a fraction of a second, holds, then fades slowly and spreads.
				float visibility = Mathf.SmoothStep(0f, 1f, drop.Age / 0.35f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_fadeStart, 1f, life)));
				// A melted flake is a drop smaller than the flake.
				float size = drop.Radius * (1f + _spread * life) * Mathf.Lerp(0.7f, 1f, drop.Frozen);
				r_shaderDrops[index] = new Vector4(drop.Position.x, drop.Position.y, size, visibility);
				r_shaderShapes[index] = new Vector4(drop.Seed, drop.Irregular, drop.Veil, drop.Blur);
				r_shaderSnow[index] = new Vector4(drop.Frozen, drop.Pattern, 0f, 0f);
			}

			_video.SetLensWater(r_shaderDrops, r_shaderShapes, r_shaderSnow, _count, _wetness);
		}

		// The sun for the lens: where it is on screen (through the fisheye), how much the camera faces it, how bright it is.
		private void PublishLight(Camera camera, Light sun)
		{
			if (sun == null || !sun.isActiveAndEnabled)
			{
				_video.SetLensLight(r_noSun, Color.white);
				return;
			}

			Vector3 toSun = -sun.transform.forward;
			float facing = Mathf.Clamp01(Vector3.Dot(camera.transform.forward, toSun) * 0.5f + 0.5f);
			float daylight = Mathf.Clamp01(sun.intensity) * Mathf.Clamp01(toSun.y * 4f + 0.3f);

			Vector3 viewport = camera.WorldToViewportPoint(camera.transform.position + toSun * 1000f);
			Vector2 sunOnScreen = viewport.z > 0f ? _video.ViewportToScreen(new Vector2(viewport.x, viewport.y)) : new Vector2(-10f, -10f);
			_video.SetLensLight(new Vector4(sunOnScreen.x, sunOnScreen.y, facing, daylight), Color.Lerp(Color.white, sun.color, 0.5f));
		}

		// Precipitation flow into the lens relative to "camera up, drone hovering": it falls at its own speed, drifts with
		// the wind, and the drone flies into it; what hits the lens flies against the camera direction.
		private float FlowIntoLens(Vector3 cameraForward, Vector3 wind, float fallSpeed, float maxFactor)
		{
			Vector3 fallVelocity = Vector3.down * fallSpeed + wind;
			Vector3 towardsLens = _velocityTracker.Velocity - fallVelocity;
			float intoLens = Mathf.Max(0f, Vector3.Dot(towardsLens, cameraForward));
			return Mathf.Min(intoLens / fallSpeed, maxFactor);
		}
	}
}
