using TMPro;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Ported from Interceptor Drones (CompassTapeAnimator): slides the compass tape to the heading and names the nearest
	// cardinal directions on both sides.
	[RequireComponent(typeof(RectTransform))]
	public sealed class FpvCompassTapeAnimator : MonoBehaviour
	{
		private const float TOTAL_DEGREES = 720f;

		[SerializeField] private float _pixelsPerDegree = 5f;
		[SerializeField] private float _moveSpeed = 10f;

		[Header("Side labels (optional)")]
		[SerializeField] private TMP_Text _leftSideLabel;
		[SerializeField] private TMP_Text _rightSideLabel;
		[SerializeField] private float _sideUpdateSpeed = 10f;

		private RectTransform _compassScale;
		private float _targetHeading;
		private float _currentHeading;
		private float _displayedHeading;

		private void Awake()
		{
			_compassScale = GetComponent<RectTransform>();
		}

		public void SetHeading(float heading)
		{
			_targetHeading = Mathf.Repeat(heading, 360f);
		}

		private void Update()
		{
			_currentHeading = Mathf.LerpAngle(_currentHeading, _targetHeading, Time.deltaTime * _moveSpeed);

			float offsetX = -_currentHeading * _pixelsPerDegree;
			float tapeLength = TOTAL_DEGREES * _pixelsPerDegree;
			float halfWidth = tapeLength * 0.5f;
			float normalized = Mathf.Repeat(offsetX + halfWidth, tapeLength) - halfWidth;

			_compassScale.anchoredPosition = new Vector2(normalized, _compassScale.anchoredPosition.y);
			_displayedHeading = Mathf.LerpAngle(_displayedHeading, _currentHeading, Time.deltaTime * _sideUpdateSpeed);

			UpdateSideLabels(_displayedHeading);
		}

		private void UpdateSideLabels(float heading)
		{
			int leftDeg = RoundToNearest90(NormalizeDeg(heading - 45f));
			int rightDeg = RoundToNearest90(NormalizeDeg(heading + 45f));

			if (_leftSideLabel != null)
			{
				_leftSideLabel.text = CardinalName(leftDeg);
			}

			if (_rightSideLabel != null)
			{
				_rightSideLabel.text = CardinalName(rightDeg);
			}
		}

		private static float NormalizeDeg(float degrees) => (degrees % 360f + 360f) % 360f;

		private static int RoundToNearest90(float degrees)
		{
			int rounded = Mathf.RoundToInt(degrees / 90f) * 90;
			return ((rounded % 360) + 360) % 360;
		}

		private static string CardinalName(int degrees)
		{
			switch (degrees)
			{
				case 0:
					return "N";
				case 90:
					return "E";
				case 180:
					return "S";
				case 270:
					return "W";
				default:
					return degrees.ToString();
			}
		}
	}
}
