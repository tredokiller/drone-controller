using System.Collections.Generic;
using UForce.FpvFlight.Environment;
using UnityEngine;

namespace UForce.FpvFlight.RotorWash
{
	// Rotor downwash near the ground, ported from Interceptor Drones (DroneRotorWash). The propellers spin from arming, so
	// even at idle fine grit flies from under the drone; with throttle the wash grows and raises a dust cloud coloured by the
	// terrain layer below (grey over gravel, brown over dirt, little over grass, almost none in rain, white on snow).
	// Over water it raises mist instead. Grass shaders bend from the globals _RotorWashPosition (xyz, radius) and
	// _RotorWashStrength. Water, puddles, snow and rain come from an IFpvRotorWashEnvironment; leaves and water ripples
	// are IFpvRotorWashReceivers on the same GameObject (the GeoWorld bridge provides both).
	[DisallowMultipleComponent]
	public sealed class FpvRotorWash : MonoBehaviour
	{
		private const string DUST_MATERIAL_RESOURCE = "UForce/FpvFlight/RotorDust";
		private const int DUST_TILES = 4;
		private const int MAX_DUST_PER_FRAME = 30;
		private const int MAX_GRIT_PER_FRAME = 20;
		// Below this height part of the dust also curls up around the drone, and on take-off the camera flies through it.
		private const float BILLOW_HEIGHT = 1.2f;
		// Leaves are blown from higher up than dust is raised (GeoWorld's RotorWashLeaves.MaxWashHeight).
		private const float RECEIVER_REACH = 12f;
		// Share of gravity for grit and droplets.
		private const float GRIT_GRAVITY = 0.5f;
		// Jet radius at the surface without an environment: 0.4 m plus 0.8 of the height, as GeoWorld's RotorWashWater.
		private const float JET_RADIUS = 0.4f;
		private const float JET_SPREAD = 0.8f;

		private static readonly int r_washPositionId = Shader.PropertyToID("_RotorWashPosition");
		private static readonly int r_washStrengthId = Shader.PropertyToID("_RotorWashStrength");
		private static FpvRotorWash s_publisher;

		[Tooltip("Must implement IFpvMotorSource. Empty: the FpvFlightController in the parents.")]
		[SerializeField] private MonoBehaviour _motorSourceBehaviour;
		[Tooltip("Optional. Must implement IWindProvider; leaves drift with it.")]
		[SerializeField] private MonoBehaviour _windProviderBehaviour;
		[SerializeField] private FpvRotorWashSurfaceSet _surfaces;
		[Tooltip("Empty: Resources/" + DUST_MATERIAL_RESOURCE + ".")]
		[SerializeField] private Material _dustMaterial;

		[Header("Wash")]
		[Tooltip("Height above ground up to which the wash raises dust and bends grass, m.")]
		[SerializeField, Min(0.5f)] private float _reach = 5f;
		[Tooltip("Wash at idle: armed, zero throttle, propellers already turning (share of full).")]
		[SerializeField, Range(0f, 1f)] private float _idleWash = 0.3f;
		[Tooltip("Dust raised at idle (share of the take-off cloud); 0: grit only.")]
		[SerializeField, Range(0f, 1f)] private float _idleDust;
		[Tooltip("Radius in which grass bends, at the ground and at the reach height, m.")]
		[SerializeField] private Vector2 _washRadius = new(6f, 12f);
		[Tooltip("Motor output at which the wash is full.")]
		[SerializeField, Range(0.1f, 1f)] private float _fullThrottle = 0.6f;

		[Header("Dust")]
		[SerializeField, Min(0f)] private float _dustPerSecond = 340f;
		[SerializeField] private Vector2 _dustSpeed = new(3f, 7f);
		[SerializeField] private Vector2 _dustLifetime = new(1.5f, 3.2f);
		[SerializeField] private Vector2 _dustSize = new(1f, 2f);
		[SerializeField] private Vector2 _dustAlpha = new(0.4f, 0.65f);
		[Tooltip("Where dust and grit leave from under the drone, m from the point below it: about the propeller disc.")]
		[SerializeField] private Vector2 _dustStartRadius = new(0.3f, 1.5f);

		[Header("Grit")]
		[SerializeField, Min(0f)] private float _gritPerSecond = 220f;
		[SerializeField] private Vector2 _gritSpeed = new(4f, 12f);
		[SerializeField] private Vector2 _gritLifetime = new(0.5f, 1.2f);
		[SerializeField] private Vector2 _gritSize = new(0.03f, 0.07f);

		private readonly List<IFpvRotorWashReceiver> r_receivers = new();

		private IFpvMotorSource _motorSource;
		private IWindProvider _windProvider;
		private IFpvRotorWashEnvironment _environment;
		private FpvRotorWashSurfaceSet _fallbackSurfaces;
		private ParticleSystem _dust;
		private ParticleSystem _grit;
		private Terrain _terrain;
		private Vector3 _ground;
		private float _height;
		private float _radius;
		private float _strength;
		private float _spin;
		private float _dustDebt;
		private float _gritDebt;
		private bool _isOverWater;
		private bool _isOnIce;
		private float _groundWater;

		public float Strength => _strength;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetPublished()
		{
			s_publisher = null;
			Shader.SetGlobalFloat(r_washStrengthId, 0f);
		}

		private void Awake()
		{
			_motorSource = _motorSourceBehaviour as IFpvMotorSource ?? GetComponentInParent<FpvFlightController>();
			_windProvider = _windProviderBehaviour as IWindProvider;
			_environment = GetComponent<IFpvRotorWashEnvironment>();
			GetComponents(r_receivers);

			if (!_surfaces)
			{
				_fallbackSurfaces = ScriptableObject.CreateInstance<FpvRotorWashSurfaceSet>();
				_fallbackSurfaces.hideFlags = HideFlags.DontSave;
			}

			Material material = _dustMaterial ? _dustMaterial : Resources.Load<Material>(DUST_MATERIAL_RESOURCE);
			if (material == null)
			{
				Debug.LogWarning($"[{nameof(FpvRotorWash)}] Dust material is missing: Resources/{DUST_MATERIAL_RESOURCE}.", this);
				return;
			}

			_dust = CreateDust(material);
			_grit = CreateGrit(material);
		}

		public void SetMotorSource(IFpvMotorSource motorSource) => _motorSource = motorSource;

		public void SetEnvironment(IFpvRotorWashEnvironment environment) => _environment = environment;

		private void OnDisable()
		{
			_strength = 0f;
			_spin = 0f;
			_dustDebt = 0f;
			_gritDebt = 0f;

			if (s_publisher == this)
			{
				Publish(Vector3.zero, 0f);
			}
		}

		private void OnDestroy()
		{
			if (_dust != null)
			{
				Destroy(_dust.gameObject);
			}

			if (_grit != null)
			{
				Destroy(_grit.gameObject);
			}

			if (_fallbackSurfaces)
			{
				Destroy(_fallbackSurfaces);
			}
		}

		private void LateUpdate()
		{
			float deltaTime = Time.deltaTime;
			float spin = Spin();
			float target = 0f;
			Vector3 position = transform.position;
			bool hasGround = TryGetGround(position, out Vector3 ground, out Terrain terrain);

			// Over a river or lake the wash hits the water, not the riverbed below it; water above the drone does not count.
			_isOverWater = false;
			_isOnIce = false;
			if (hasGround && _environment != null && _environment.TryGetWaterHeight(position, out float waterHeight, out bool isFrozen)
				&& waterHeight > ground.y && waterHeight <= position.y)
			{
				ground.y = waterHeight;
				_isOnIce = isFrozen;
				_isOverWater = !isFrozen;
			}

			if (hasGround)
			{
				float height = Mathf.Max(0f, position.y - ground.y);
				float closeness = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_reach * 0.3f, _reach, height));
				target = spin * closeness;
				_ground = ground;
				_height = height;
				_radius = Mathf.Lerp(_washRadius.x, _washRadius.y, Mathf.Clamp01(height / _reach));
			}

			_groundWater = hasGround && !_isOverWater && !_isOnIce && _environment != null ? GroundWaterUnder(ground, _height) : 0f;

			// The wash builds up fast and settles slower, where the drone was last near the ground.
			_strength = Mathf.MoveTowards(_strength, target, deltaTime * (target > _strength ? 4f : 1.5f));
			_spin = Mathf.MoveTowards(_spin, spin, deltaTime * (spin > _spin ? 4f : 1.5f));

			if (_strength > 0.001f)
			{
				Publish(_ground, _radius);
			}
			else if (s_publisher == this)
			{
				Publish(Vector3.zero, 0f);
			}

			if (hasGround && _height <= _reach)
			{
				EmitDust(ground, terrain, deltaTime);
			}

			if (r_receivers.Count > 0)
			{
				Vector3 velocity = _motorSource != null ? _motorSource.Velocity : Vector3.zero;
				var state = new FpvRotorWashState(
					position,
					_ground,
					hasGround,
					hasGround ? _height : FpvRotorWashState.NO_GROUND_HEIGHT,
					_spin,
					_strength,
					Wind(position),
					velocity,
					_terrain,
					_isOverWater);

				for (int index = 0; index < r_receivers.Count; index++)
				{
					r_receivers[index].Blow(state);
				}
			}
		}

		// Propeller wash as a share of full: idle wash from arming, full at the full-throttle motor output; 0 disarmed.
		private float Spin()
		{
			if (_motorSource == null || !_motorSource.IsArmed)
			{
				return 0f;
			}

			return Mathf.Lerp(_idleWash, 1f, Mathf.Clamp01(_motorSource.MotorOutput / _fullThrottle));
		}

		private Vector3 Wind(Vector3 position)
		{
			if (_windProvider == null)
			{
				return Vector3.zero;
			}

			Vector3 wind = _windProvider.Sample(position, Time.time).Velocity;
			return Vector3.ProjectOnPlane(wind, Vector3.up);
		}

		private float JetRadiusAt(float height) =>
			_environment != null ? _environment.JetRadiusAt(height) : JET_RADIUS + JET_SPREAD * Mathf.Min(height, RECEIVER_REACH);

		private float GroundWaterUnder(Vector3 ground, float height)
		{
			float water = _environment.GroundWaterAt(ground);
			float jet = JetRadiusAt(height);

			for (int index = 0; index < 4 && water < 1f; index++)
			{
				float angle = index * Mathf.PI * 0.5f;
				water = Mathf.Max(water, _environment.GroundWaterAt(ground + new Vector3(Mathf.Cos(angle) * jet, 0f, Mathf.Sin(angle) * jet)));
			}

			return water;
		}

		private bool TryGetGround(Vector3 position, out Vector3 ground, out Terrain terrain)
		{
			terrain = Contains(_terrain, position) ? _terrain : null;

			if (terrain == null)
			{
				foreach (Terrain candidate in Terrain.activeTerrains)
				{
					if (Contains(candidate, position))
					{
						terrain = candidate;
						break;
					}
				}

				_terrain = terrain;
			}

			if (terrain != null)
			{
				ground = new Vector3(position.x, terrain.SampleHeight(position) + terrain.transform.position.y, position.z);
			}
			else if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, Mathf.Max(_reach, RECEIVER_REACH) + 1f, ~0, QueryTriggerInteraction.Ignore))
			{
				// Not over a terrain (a plane, a mesh world): the first surface below.
				ground = hit.point;
			}
			else
			{
				ground = default;
				return false;
			}

			return position.y - ground.y < Mathf.Max(_reach, RECEIVER_REACH) + 1f;
		}

		private static bool Contains(Terrain terrain, Vector3 position)
		{
			if (terrain == null || terrain.terrainData == null)
			{
				return false;
			}

			Vector3 local = position - terrain.transform.position;
			Vector3 size = terrain.terrainData.size;
			return local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z;
		}

		private void EmitDust(Vector3 ground, Terrain terrain, float deltaTime)
		{
			FpvRotorWashSurfaceSet surfaces = _surfaces ? _surfaces : _fallbackSurfaces;
			bool isWatery = _isOverWater || _groundWater > 0.5f;
			float strength = isWatery && _environment != null
				? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 9f, _environment.JetAir(_height, _spin)))
				: _strength;

			if (_dust == null || _grit == null || strength < 0.02f)
			{
				_dustDebt = 0f;
				_gritDebt = 0f;
				return;
			}

			Color color;
			float amount;
			if (isWatery)
			{
				color = surfaces.WaterSpray;
				amount = surfaces.WaterSprayAmount;
			}
			else
			{
				SampleGround(surfaces, terrain, ground, out color, out amount);

				float snowfall = _environment != null ? _environment.SnowfallIntensity : 0f;
				float snowCover = _environment != null ? _environment.SnowCover : 0f;
				if (snowfall > 0.2f || _isOnIce || snowCover > 0.3f)
				{
					color = surfaces.SnowDust;
					amount = Mathf.Max(amount, 0.7f);
				}

				// Wet ground does not dust.
				float rain = _environment != null ? _environment.RainIntensity : 0f;
				amount *= 1f - rain * 0.85f;
			}

			color *= _environment != null ? _environment.DustLight : DefaultDustLight();

			float cloud = isWatery
				? strength
				: Mathf.Lerp(_idleDust, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_idleWash, 1f, _strength))) * Mathf.Clamp01(_strength / Mathf.Max(0.01f, _idleWash));
			int clouds = Take(ref _dustDebt, _dustPerSecond * cloud * amount * deltaTime, MAX_DUST_PER_FRAME);
			int grit = isWatery ? 0 : Take(ref _gritDebt, _gritPerSecond * strength * amount * deltaTime, MAX_GRIT_PER_FRAME);
			if (isWatery)
			{
				_gritDebt = 0f;
			}

			bool isBillowing = _height < BILLOW_HEIGHT;
			Vector2 startRadius = isWatery ? new Vector2(0.5f, 1.2f) * JetRadiusAt(_height) : _dustStartRadius;
			float spraySize = surfaces.WaterSpraySize;
			var particle = new ParticleSystem.EmitParams();

			for (int index = 0; index < clouds; index++)
			{
				Vector2 direction = Random.insideUnitCircle.normalized;
				float start = Random.Range(startRadius.x, startRadius.y);
				if (!IsWet(isWatery, ground, direction, start))
				{
					start = Random.Range(_dustStartRadius.x, _dustStartRadius.y);
					if (!IsWet(isWatery, ground, direction, start))
					{
						continue;
					}
				}

				float speed = Random.Range(_dustSpeed.x, _dustSpeed.y) * Mathf.Lerp(0.5f, 1f, strength);
				float rise = isWatery ? Random.Range(0.05f, 0.35f) : isBillowing && Random.value < 0.35f ? Random.Range(1.5f, 3f) : Random.Range(0.4f, 1.6f);
				particle.position = ground + new Vector3(direction.x * start, isWatery ? 0.1f : 0.2f, direction.y * start);
				particle.velocity = new Vector3(direction.x * speed, rise * strength, direction.y * speed);
				particle.startLifetime = Random.Range(_dustLifetime.x, _dustLifetime.y) * (isWatery ? 0.7f : 1f);
				particle.startSize = Random.Range(_dustSize.x, _dustSize.y) * Mathf.Lerp(0.6f, 1f, cloud) * (isWatery ? spraySize * 1.4f : 1f);
				particle.rotation = Random.Range(0f, 360f);
				particle.startColor = new Color(color.r, color.g, color.b, Random.Range(_dustAlpha.x, _dustAlpha.y) * (isWatery ? spraySize * 0.35f : 1f));
				_dust.Emit(particle, 1);
			}

			// Grit is a little lighter than the ground below it: dry crumbs catch the light.
			Color gritColor = color * 1.1f;
			for (int index = 0; index < grit; index++)
			{
				Vector2 direction = Random.insideUnitCircle.normalized;
				float start = Random.Range(startRadius.x * 0.5f, startRadius.y * 0.6f);
				float speed = Random.Range(_gritSpeed.x, _gritSpeed.y) * Mathf.Lerp(0.6f, 1f, strength);
				particle.position = ground + new Vector3(direction.x * start, 0.05f, direction.y * start);
				particle.velocity = new Vector3(direction.x * speed, Random.Range(1.5f, 3.5f) * Mathf.Lerp(0.6f, 1f, strength), direction.y * speed);
				particle.startLifetime = Random.Range(_gritLifetime.x, _gritLifetime.y);
				particle.startSize = Random.Range(_gritSize.x, _gritSize.y);
				particle.rotation = Random.Range(0f, 360f);
				particle.startColor = new Color(gritColor.r, gritColor.g, gritColor.b, 1f);
				_grit.Emit(particle, 1);
			}
		}

		// Spray only from water: over a narrow stream, at a shore or over a puddle the edge of the jet reaches dry land.
		private bool IsWet(bool isWatery, Vector3 ground, Vector2 direction, float radius)
		{
			if (!isWatery || _environment == null)
			{
				return true;
			}

			Vector3 at = ground + new Vector3(direction.x * radius, 0f, direction.y * radius);
			if (_isOverWater)
			{
				return _environment.TryGetWaterHeight(at + Vector3.up * 0.5f, out float surface, out _) && surface > ground.y - 0.5f;
			}

			return _environment.GroundWaterAt(at) > 0.5f;
		}

		private static int Take(ref float debt, float amount, int max)
		{
			debt += amount;
			int count = Mathf.Min((int)debt, max);
			debt -= (int)debt;
			return count;
		}

		// Dust colour and amount from the terrain layer weights below the drone.
		private static void SampleGround(FpvRotorWashSurfaceSet surfaces, Terrain terrain, Vector3 ground, out Color color, out float amount)
		{
			if (terrain == null || terrain.terrainData == null)
			{
				color = surfaces.Default.Color;
				amount = surfaces.Default.Amount;
				return;
			}

			TerrainData data = terrain.terrainData;
			Vector3 local = ground - terrain.transform.position;
			int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
			int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
			float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
			TerrainLayer[] layers = data.terrainLayers;
			color = Color.black;
			amount = 0f;
			float total = 0f;

			for (int index = 0; index < layers.Length && index < data.alphamapLayers; index++)
			{
				float weight = weights[0, 0, index];
				if (weight <= 0f)
				{
					continue;
				}

				FpvRotorWashSurface surface = surfaces.Find(layers[index]);
				color += surface.Color * weight;
				amount += surface.Amount * weight;
				total += weight;
			}

			if (total <= 0.0001f)
			{
				color = surfaces.Default.Color;
				amount = surfaces.Default.Amount;
				return;
			}

			color /= total;
			amount /= total;
		}

		// Dust is not lit like a surface (against the sun the puffs came out dark): light it by sky and sun ourselves.
		private static Color DefaultDustLight()
		{
			Light sun = RenderSettings.sun;
			float sunlight = 0f;
			if (sun != null && sun.isActiveAndEnabled)
			{
				float elevation = Mathf.Clamp01(Vector3.Dot(-sun.transform.forward, Vector3.up));
				sunlight = sun.intensity * Mathf.Sqrt(elevation);
			}

			Color ambient = RenderSettings.ambientLight;
			float light = Mathf.Clamp(0.35f + 0.65f * Mathf.Min(1f, sunlight) + ambient.grayscale * 0.3f, 0.15f, 1.2f);
			return new Color(light, light, light, 1f);
		}

		private void Publish(Vector3 ground, float radius)
		{
			Shader.SetGlobalVector(r_washPositionId, new Vector4(ground.x, ground.y, ground.z, radius));
			Shader.SetGlobalFloat(r_washStrengthId, _strength);
			s_publisher = _strength > 0.001f ? this : null;
		}

		// In world space, not under the drone: dust stays where it is when the drone climbs away. Emit spawns the particles.
		private static ParticleSystem CreateParticles(string name, Material material, int maxParticles, float gravity)
		{
			var particlesObject = new GameObject(name);
			var particles = particlesObject.AddComponent<ParticleSystem>();
			particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			ParticleSystem.MainModule main = particles.main;
			main.loop = true;
			main.playOnAwake = false;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.maxParticles = maxParticles;
			main.startSpeed = 0f;
			main.gravityModifier = gravity;

			ParticleSystem.EmissionModule emission = particles.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = particles.shape;
			shape.enabled = false;

			ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
			sheet.enabled = true;
			sheet.numTilesX = DUST_TILES;
			sheet.numTilesY = DUST_TILES;
			sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
			sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, DUST_TILES * DUST_TILES - 1);

			var renderer = particlesObject.GetComponent<ParticleSystemRenderer>();
			renderer.sharedMaterial = material;
			renderer.sortMode = ParticleSystemSortMode.Distance;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			return particles;
		}

		private static ParticleSystem CreateDust(Material material)
		{
			ParticleSystem dust = CreateParticles("Rotor dust", material, 1400, -0.01f);

			ParticleSystem.LimitVelocityOverLifetimeModule drag = dust.limitVelocityOverLifetime;
			drag.enabled = true;
			drag.limit = 100f;
			drag.drag = 0.7f;
			drag.multiplyDragByParticleSize = false;
			drag.multiplyDragByParticleVelocity = false;

			ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f)));

			ParticleSystem.ColorOverLifetimeModule colour = dust.colorOverLifetime;
			colour.enabled = true;
			var fade = new Gradient();
			fade.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
			colour.color = fade;

			ParticleSystem.RotationOverLifetimeModule rotation = dust.rotationOverLifetime;
			rotation.enabled = true;
			rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

			var renderer = dust.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Billboard;
			// Dust right in front of the camera must not shrink.
			renderer.maxParticleSize = 4f;
			dust.Play();
			return dust;
		}

		// Grit: small crumbs of earth the wash throws low over the ground; they fall and stretch along their speed.
		private static ParticleSystem CreateGrit(Material material)
		{
			ParticleSystem grit = CreateParticles("Rotor grit", material, 600, GRIT_GRAVITY);

			ParticleSystem.LimitVelocityOverLifetimeModule drag = grit.limitVelocityOverLifetime;
			drag.enabled = true;
			drag.limit = 100f;
			drag.drag = 0.4f;
			drag.multiplyDragByParticleSize = false;
			drag.multiplyDragByParticleVelocity = false;

			ParticleSystem.ColorOverLifetimeModule colour = grit.colorOverLifetime;
			colour.enabled = true;
			var fade = new Gradient();
			fade.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
			colour.color = fade;

			var renderer = grit.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Stretch;
			renderer.velocityScale = 0.05f;
			renderer.lengthScale = 1f;
			grit.Play();
			return grit;
		}
	}
}
