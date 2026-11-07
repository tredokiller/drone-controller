using TMPro;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Ported from Interceptor Drones (CompassTapeGenerator): builds the compass tape ticks and degree labels at start,
	// two full turns plus a buffer on each side so the tape can wrap seamlessly.
	[RequireComponent(typeof(RectTransform))]
	public sealed class FpvCompassTapeGenerator : MonoBehaviour
	{
		private const float TOTAL_DEGREES = 720f;

		[Tooltip("Tick template. Can be an inactive child of the OSD prefab.")]
		[SerializeField] private GameObject _linePrefab;
		[Tooltip("Label template. Can be an inactive child of the OSD prefab.")]
		[SerializeField] private GameObject _labelPrefab;
		[SerializeField] private RectTransform _viewport;
		[SerializeField] private float _pixelsPerDegree = 5f;
		[SerializeField] private int _stepDegrees = 10;

		private RectTransform _tapeParent;

		private void Awake()
		{
			_tapeParent = GetComponent<RectTransform>();
		}

		private void Start()
		{
			_tapeParent.sizeDelta = new Vector2(TOTAL_DEGREES * _pixelsPerDegree, _tapeParent.sizeDelta.y);
			GenerateTapeWithBuffer();
		}

		private void GenerateTapeWithBuffer()
		{
			float halfWidth = _tapeParent.sizeDelta.x * 0.5f;
			float tapeHeight = _tapeParent.sizeDelta.y / 4;

			float viewportPixels = _viewport != null ? _viewport.rect.width : 120f * _pixelsPerDegree;
			float viewportDegrees = Mathf.Ceil(viewportPixels / _pixelsPerDegree);
			int bufferDegrees = Mathf.CeilToInt(viewportDegrees) + _stepDegrees;

			int startDeg = -bufferDegrees;
			int endDeg = Mathf.CeilToInt(TOTAL_DEGREES) + bufferDegrees;

			for (int deg = startDeg; deg <= endDeg; deg += _stepDegrees)
			{
				float posX = deg * _pixelsPerDegree - halfWidth;

				GameObject tick = Instantiate(_linePrefab, _tapeParent, false);
				tick.SetActive(true);
				var tickRect = tick.GetComponent<RectTransform>();
				tickRect.anchorMin = tickRect.anchorMax = new Vector2(0.5f, 0f);
				tickRect.pivot = new Vector2(0.5f, 0f);

				bool hasLabel = deg % 30 == 0;
				float tickHeight = hasLabel ? tapeHeight * 0.6f : tapeHeight;
				tickRect.sizeDelta = new Vector2(4f, tickHeight);
				tickRect.anchoredPosition = new Vector2(posX, 0f);

				if (!hasLabel)
				{
					continue;
				}

				GameObject label = Instantiate(_labelPrefab, _tapeParent, false);
				label.SetActive(true);
				var labelRect = label.GetComponent<RectTransform>();
				labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
				labelRect.pivot = new Vector2(0.5f, 1f);
				labelRect.anchoredPosition = new Vector2(posX, tickHeight + labelRect.sizeDelta.y);

				if (label.TryGetComponent(out TMP_Text text))
				{
					text.text = ((deg % 360 + 360) % 360).ToString();
				}
			}
		}
	}
}
