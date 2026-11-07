using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// The package's OSD prefab and the picture an OSD preview is drawn over, reachable at runtime without references
	// (Resources/UForce/FpvFlight/FpvOsdDefaults). Rebuilt by Tools/UForce/FPV Flight/Build OSD Prefab.
	[CreateAssetMenu(menuName = "UForce/FPV Flight/OSD Defaults", fileName = "FpvOsdDefaults")]
	public sealed class FpvOsdDefaults : ScriptableObject
	{
		public const string RESOURCE_PATH = "UForce/FpvFlight/FpvOsdDefaults";

		[SerializeField] private Canvas _osdPrefab;
		[Tooltip("An FPV camera frame without OSD.")]
		[SerializeField] private Texture2D _previewBackground;

		public Canvas OsdPrefab => _osdPrefab;
		public Texture2D PreviewBackground => _previewBackground;

		public static FpvOsdDefaults Load() => Resources.Load<FpvOsdDefaults>(RESOURCE_PATH);

		public void Configure(Canvas osdPrefab, Texture2D previewBackground)
		{
			_osdPrefab = osdPrefab;
			_previewBackground = previewBackground;
		}
	}
}
