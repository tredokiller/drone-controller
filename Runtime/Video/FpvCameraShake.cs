using UnityEngine;
using UnityEngine.Rendering;

namespace UForce.FpvFlight.Video
{
	// Ported from Interceptor Drones (DroneCameraShake): vibration and buffeting that grow with the square of the speed.
	// The rotation is applied only while this camera renders and restored right after, so the drone's orientation,
	// flight and OSD attitude are not affected.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	public sealed class FpvCameraShake : MonoBehaviour
	{
		private const float MIN_VISIBLE_SHAKE = 0.001f;

		[Tooltip("The camera does not shake below this speed, km/h.")]
		[SerializeField, Min(0f)] private float _startSpeedKmh = 30f;
		[SerializeField, Min(1f)] private float _maxSpeedKmh = 150f;
		[Tooltip("Full shake at the maximum speed multiplied by this value.")]
		[SerializeField, Range(0.2f, 2f)] private float _fullSpeedFactor = 1f;
		[SerializeField, Range(0f, 3f)] private float _strength = 0.5f;

		[Header("Vibration")]
		[SerializeField, Range(0f, 2f)] private float _vibrationAngle = 0.25f;
		[SerializeField, Min(0.1f)] private float _vibrationFrequency = 19f;

		[Header("Buffeting")]
		[SerializeField, Range(0f, 5f)] private float _buffetAngle = 0.9f;
		[SerializeField, Min(0.1f)] private float _buffetFrequency = 2.6f;

		[Tooltip("How fast the shake follows the speed, 1/s.")]
		[SerializeField, Min(0.1f)] private float _response = 4f;

		private Camera _camera;
		private FpvBodyVelocityTracker _velocityTracker;
		private float _intensity;
		private float _seed;
		private bool _isShaken;
		private Quaternion _restoreRotation;

		private void Awake()
		{
			_camera = GetComponent<Camera>();
			_velocityTracker = new FpvBodyVelocityTracker(transform);
			_seed = Random.Range(0f, 100f);
		}

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			if (deltaTime <= 0f)
			{
				return;
			}

			_velocityTracker.Tick(deltaTime);
			_intensity = Mathf.Lerp(_intensity, GetTargetIntensity(), 1f - Mathf.Exp(-_response * deltaTime));
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
			RestoreRotation();
			_velocityTracker.Reset();
			_intensity = 0f;
		}

		private float GetTargetIntensity()
		{
			float fullSpeedKmh = Mathf.Max(_startSpeedKmh + 1f, _maxSpeedKmh * _fullSpeedFactor);
			float speed = Mathf.Clamp01((_velocityTracker.SpeedKmh - _startSpeedKmh) / (fullSpeedKmh - _startSpeedKmh));
			// Air pressure grows with the square of the speed.
			return speed * speed;
		}

		private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (camera != _camera || _isShaken || _intensity * _strength < MIN_VISIBLE_SHAKE)
			{
				return;
			}

			float time = Time.time;
			float vibration = _vibrationAngle * _intensity * _strength;
			float buffet = _buffetAngle * _intensity * _strength;
			float pitch = GetNoise(time * _vibrationFrequency, 0f) * vibration + GetNoise(time * _buffetFrequency, 10f) * buffet;
			float yaw = GetNoise(time * _vibrationFrequency, 20f) * vibration * 0.6f + GetNoise(time * _buffetFrequency, 30f) * buffet * 0.5f;
			float roll = GetNoise(time * _vibrationFrequency, 40f) * vibration * 0.8f + GetNoise(time * _buffetFrequency, 50f) * buffet * 0.7f;

			_restoreRotation = transform.localRotation;
			transform.localRotation = _restoreRotation * Quaternion.Euler(pitch, yaw, roll);
			_isShaken = true;
		}

		private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
		{
			if (camera == _camera)
			{
				RestoreRotation();
			}
		}

		private void RestoreRotation()
		{
			if (!_isShaken)
			{
				return;
			}

			transform.localRotation = _restoreRotation;
			_isShaken = false;
		}

		private float GetNoise(float x, float offset) => Mathf.PerlinNoise(x, _seed + offset) * 2f - 1f;
	}
}
