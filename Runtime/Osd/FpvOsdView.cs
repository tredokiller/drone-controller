using System.Collections.Generic;
using TMPro;
using UForce.FpvFlight.Rc;
using UForce.FpvFlight.Video;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// The FPV OSD of Interceptor Drones (DroneOsdView, main panel): VTX signal, arm state, payload, battery, altitude,
	// speed, heading with compass tape, artificial horizon and reticle. Values come from an IFpvOsdDataSource instead
	// of the old fuel-mapped HUD, so the battery shows the real loaded cell voltage. Added for the flight core:
	// the flight mode, a centre warning for failsafe and rx loss, and hidden Betaflight-style extras.
	//
	// The pilot's layout (FPV Defence Simulator, FpvDroneOsdView): the elements of FpvOsdElementCatalog found under the
	// main panel can be shown, hidden and moved by their anchor, with an OSD opacity and a bold font. The view reads the
	// pilot's settings from a store (PlayerPrefsFpvOsdSettingsStore.Default unless told otherwise) and follows its changes.
	[DisallowMultipleComponent]
	public sealed class FpvOsdView : MonoBehaviour
	{
		private const string MESSAGE_PREVIEW = "FAILSAFE";
		private const float LOW_CHARGE = 0.2f;
		private const float LOW_LINK_QUALITY = 0.5f;

		[Header("Panels")]
		[SerializeField] private GameObject _mainPanel;
		[Tooltip("Load the pilot's settings from PlayerPrefsFpvOsdSettingsStore.Default and follow their changes. Off: start in the initial mode.")]
		[SerializeField] private bool _useSavedSettings = true;
		[SerializeField] private FpvOsdMode _initialMode = FpvOsdMode.Main;

		[Header("Telemetry")]
		[Tooltip("Must implement IFpvOsdDataSource. Empty: the first one found in the parents of the video camera.")]
		[SerializeField] private MonoBehaviour _dataSourceBehaviour;
		[SerializeField] private TMP_Text _batteryText;
		[SerializeField] private TMP_Text _vtxText;
		[SerializeField] private TMP_Text _altitudeText;
		[SerializeField] private TMP_Text _speedText;
		[SerializeField] private TMP_Text _headingText;
		[SerializeField] private TMP_Text _armText;
		[SerializeField] private TMP_Text _payloadText;
		[SerializeField] private TMP_Text _modeText;
		[SerializeField] private TMP_Text _warningText;
		[SerializeField] private FpvCompassTapeAnimator _compassTapeAnimator;

		[Header("Extras")]
		[SerializeField] private TMP_Text _throttleText;
		[SerializeField] private TMP_Text _flightTimeText;
		[SerializeField] private TMP_Text _currentText;
		[SerializeField] private TMP_Text _usedMahText;
		[SerializeField] private TMP_Text _packVoltageText;
		[SerializeField] private TMP_Text _batteryPercentText;
		[SerializeField] private TMP_Text _linkQualityText;
		[SerializeField] private TMP_Text _verticalSpeedText;
		[SerializeField] private TMP_Text _pitchText;
		[SerializeField] private TMP_Text _rollText;

		[Header("Horizon")]
		[SerializeField] private RectTransform _horizon;
		[Tooltip("OSD horizon roll per degree of camera roll (Interceptor: -1).")]
		[SerializeField] private float _rollAmplitude = -1f;
		[Tooltip("OSD pixels per degree of camera pitch (Interceptor: -8.4).")]
		[SerializeField] private float _pitchAmplitude = -8.4f;

		[Header("Thresholds")]
		[SerializeField] private float _lowCellVoltage = 3.5f;
		[SerializeField] private int _lowSignalPercent = 25;
		[SerializeField] private Color _normalColor = Color.white;
		[SerializeField] private Color _warningColor = new(1f, 0.35f, 0.2f, 1f);
		[SerializeField] private Color _alertColor = Color.red;

		private readonly List<Element> r_elements = new();

		private FpvOsdMode _mode;
		private IFpvOsdDataSource _dataSource;
		private Camera _viewCamera;
		private RectTransform _canvasRect;
		private CanvasGroup _canvasGroup;
		private TMP_Text[] _texts;
		private FontStyles[] _textStyles;
		private FpvOsdLayout _defaultLayout;
		private FpvOsdLayout _appliedLayout;
		private FpvOsdSettings _settings = new();
		private IFpvOsdSettingsStore _settingsStore;
		private bool _isEditPreview;
		private bool _isInitialized;

		public FpvOsdMode Mode => _mode;
		public FpvOsdSettings Settings => _settings.Clone();
		public IFpvOsdSettingsStore SettingsStore => _settingsStore;

		// The prefab's layout (FpvOsdMode.Main).
		public FpvOsdLayout DefaultLayout
		{
			get
			{
				Initialize();
				return _defaultLayout.Clone();
			}
		}

		private void Awake()
		{
			FpvVideoCamera video = GetComponentInParent<FpvVideoCamera>();
			_viewCamera = video != null ? video.Camera != null ? video.Camera : video.GetComponent<Camera>() : null;

			_dataSource = _dataSourceBehaviour as IFpvOsdDataSource;
			if (_dataSource == null)
			{
				_dataSource = FindDataSource(video != null ? video.transform : transform.parent);
			}

			Initialize();
			if (_useSavedSettings)
			{
				SetSettingsStore(PlayerPrefsFpvOsdSettingsStore.Default);
			}
			else
			{
				SetMode(_initialMode);
			}
		}

		private void OnDestroy()
		{
			if (_settingsStore != null)
			{
				_settingsStore.Changed -= HandleSettingsChanged;
			}
		}

		public void SetDataSource(IFpvOsdDataSource dataSource)
		{
			_dataSource = dataSource;
		}

		// Follows this store from now on and applies what it holds; null keeps the current settings.
		public void SetSettingsStore(IFpvOsdSettingsStore store)
		{
			if (_settingsStore != null)
			{
				_settingsStore.Changed -= HandleSettingsChanged;
			}

			_settingsStore = store;
			if (_settingsStore == null)
			{
				return;
			}

			_settingsStore.Changed += HandleSettingsChanged;
			SetSettings(_settingsStore.Load());
		}

		public void SetSettings(FpvOsdSettings settings)
		{
			_settings = settings != null ? settings.Clone() : new FpvOsdSettings();
			SetMode(_settings.Mode);
		}

		public void SetMode(FpvOsdMode mode)
		{
			Initialize();
			_mode = mode;
			if (_mainPanel != null)
			{
				_mainPanel.SetActive(mode != FpvOsdMode.Off);
			}

			ApplyLayout(mode == FpvOsdMode.Custom ? CreateLayout(_settings.CustomLayout) : _defaultLayout);
		}

		// Positions, visibility, opacity and font of a layout, as given. Elements it does not name keep the defaults.
		public void ApplyLayout(FpvOsdLayout layout)
		{
			Initialize();
			layout ??= _defaultLayout;
			foreach (Element element in r_elements)
			{
				FpvOsdElementLayout resolved = Resolve(layout, element);
				element.Rect.anchorMin = resolved.Anchor;
				element.Rect.anchorMax = resolved.Anchor;
				element.Rect.gameObject.SetActive(resolved.IsVisible);
			}

			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = layout.Opacity;
			}

			for (int index = 0; index < _texts.Length; index++)
			{
				if (_texts[index] != null)
				{
					_texts[index].fontStyle = layout.BoldFont ? _textStyles[index] | FontStyles.Bold : _textStyles[index];
				}
			}

			_appliedLayout = CreateLayout(layout);
		}

		// A complete layout for this OSD: every element, named by the source or from the defaults (extras hidden).
		public FpvOsdLayout CreateLayout(FpvOsdLayout source)
		{
			Initialize();
			var layout = new FpvOsdLayout
			{
				Opacity = source?.Opacity ?? 1f,
				BoldFont = source?.BoldFont ?? false
			};

			foreach (Element element in r_elements)
			{
				layout.SetElement(Resolve(source, element));
			}

			return layout;
		}

		public FpvOsdLayout GetAppliedLayout()
		{
			Initialize();
			return (_appliedLayout ?? _defaultLayout).Clone();
		}

		public bool HasElement(string id) => IndexOf(id) >= 0;

		public bool IsElementShown(string id)
		{
			int index = IndexOf(id);
			return index >= 0 && r_elements[index].Rect.gameObject.activeInHierarchy;
		}

		// Where the element is drawn, normalized to the OSD canvas (0..1, origin bottom left): the text it shows, or its
		// rectangle when it has no text.
		public Rect GetElementBounds(string id)
		{
			int index = IndexOf(id);
			if (index < 0)
			{
				return Rect.zero;
			}

			Element element = r_elements[index];
			Vector2 min = Vector2.positiveInfinity;
			Vector2 max = Vector2.negativeInfinity;
			foreach (TMP_Text text in element.Texts)
			{
				if (!text.isActiveAndEnabled)
				{
					continue;
				}

				text.ForceMeshUpdate();
				Bounds bounds = text.textBounds;
				if (bounds.size.x > 0f)
				{
					Encapsulate(text.transform, bounds.min, bounds.max, ref min, ref max);
				}
			}

			if (min.x > max.x)
			{
				Encapsulate(element.Rect, element.Rect.rect.min, element.Rect.rect.max, ref min, ref max);
			}

			Rect area = _canvasRect.rect;
			if (area.width <= 0f || area.height <= 0f)
			{
				return Rect.zero;
			}

			return Rect.MinMaxRect(
				(min.x - area.xMin) / area.width,
				(min.y - area.yMin) / area.height,
				(max.x - area.xMin) / area.width,
				(max.y - area.yMin) / area.height);
		}

		// Where the element would be drawn at its prefab anchor.
		public Rect GetDefaultBounds(string id)
		{
			int index = IndexOf(id);
			if (index < 0)
			{
				return Rect.zero;
			}

			Rect bounds = GetElementBounds(id);
			bounds.position += GetDefaultElement(index).Anchor - r_elements[index].Rect.anchorMin;
			return bounds;
		}

		// While editing, the payload and the warning are shown even when there is nothing to show, so they can be placed.
		public void SetEditPreview(bool isPreview)
		{
			_isEditPreview = isPreview;
			if (_payloadText != null)
			{
				_payloadText.enabled = isPreview || _payloadText.enabled;
			}

			if (_warningText != null)
			{
				_warningText.enabled = isPreview;
				if (isPreview)
				{
					_warningText.text = MESSAGE_PREVIEW;
					_warningText.color = _alertColor;
				}
			}
		}

		private void LateUpdate()
		{
			if (_mode == FpvOsdMode.Off || _dataSource == null || !_dataSource.TryGetData(out FpvOsdData data))
			{
				return;
			}

			UpdateHorizon(data);

			_batteryText.SetText("BAT {0:0.00}V", data.CellVoltage);
			_batteryText.color = data.CellVoltage <= _lowCellVoltage ? _warningColor : _normalColor;

			int signalPercent = Mathf.RoundToInt(Mathf.Clamp01(data.VideoSignal) * 100f);
			_vtxText.SetText("VTX {0:0}%", signalPercent);
			_vtxText.color = signalPercent <= _lowSignalPercent ? _warningColor : _normalColor;

			_altitudeText.SetText("ALT {0:0}M", data.Altitude);
			if (IsShown(_speedText))
			{
				_speedText.SetText("SPD {0:0}KM/H", data.SpeedKmh);
			}

			float heading = Mathf.Repeat(data.Heading, 360f);
			_headingText.SetText("{0:000}", heading);
			_compassTapeAnimator.SetHeading(heading);

			_armText.text = data.IsArmed ? "ARMED" : "DISARMED";
			_armText.color = data.IsArmed ? _normalColor : _alertColor;

			if (_payloadText != null)
			{
				_payloadText.enabled = _isEditPreview || data.HasPayload;
				_payloadText.color = data.IsPayloadArmed ? _alertColor : _normalColor;
			}

			if (_modeText != null)
			{
				_modeText.text = data.Mode switch
				{
					FlightMode.Angle => "ANGLE",
					FlightMode.Horizon => "HORIZON",
					_ => "ACRO"
				};
			}

			UpdateWarning(data);
			UpdateExtras(data);
		}

		private void UpdateExtras(in FpvOsdData data)
		{
			if (IsShown(_throttleText))
			{
				_throttleText.SetText("THR {0:0}%", Mathf.Clamp01(data.Throttle) * 100f);
			}

			if (IsShown(_flightTimeText))
			{
				int seconds = Mathf.Max(0, Mathf.FloorToInt(data.FlightTime));
				_flightTimeText.SetText("FLY {0:00}:{1:00}", seconds / 60, seconds % 60);
			}

			if (IsShown(_currentText))
			{
				_currentText.SetText("{0:0.0}A", data.Current);
			}

			if (IsShown(_usedMahText))
			{
				_usedMahText.SetText("{0:0}MAH", data.UsedMah);
			}

			if (IsShown(_packVoltageText))
			{
				_packVoltageText.SetText("{0:0.00}V", data.PackVoltage);
			}

			if (IsShown(_batteryPercentText))
			{
				_batteryPercentText.SetText("BAT {0:0}%", Mathf.Clamp01(data.BatteryCharge) * 100f);
				_batteryPercentText.color = data.BatteryCharge <= LOW_CHARGE ? _warningColor : _normalColor;
			}

			if (IsShown(_linkQualityText))
			{
				_linkQualityText.SetText("LQ {0:0}", Mathf.Clamp01(data.LinkQuality) * 100f);
				_linkQualityText.color = data.LinkQuality < LOW_LINK_QUALITY ? _warningColor : _normalColor;
			}

			if (IsShown(_verticalSpeedText))
			{
				if (data.VerticalSpeed >= 0.05f)
				{
					_verticalSpeedText.SetText("VS +{0:0.0}M/S", data.VerticalSpeed);
				}
				else
				{
					_verticalSpeedText.SetText("VS {0:0.0}M/S", data.VerticalSpeed);
				}
			}

			if (IsShown(_pitchText))
			{
				_pitchText.SetText("PIT {0:0}", data.Pitch);
			}

			if (IsShown(_rollText))
			{
				_rollText.SetText("ROL {0:0}", data.Roll);
			}
		}

		private void UpdateWarning(in FpvOsdData data)
		{
			if (_warningText == null || _isEditPreview)
			{
				return;
			}

			string warning = data.FailsafeStage switch
			{
				FailsafeStage.StageTwo => "FAILSAFE",
				FailsafeStage.StageOne => "RXLOSS",
				_ => !data.IsArmed && (data.ArmingBlockReason & ArmingBlockReason.RxLoss) != 0 ? "RXLOSS" : null
			};

			_warningText.enabled = warning != null;
			if (warning != null)
			{
				_warningText.text = warning;
				_warningText.color = _alertColor;
			}
		}

		// Gravity in camera space, so the camera uptilt and the body roll combine correctly. Without a camera (a preview)
		// the telemetry attitude stands in for the camera's.
		private void UpdateHorizon(in FpvOsdData data)
		{
			if (_horizon == null || !_horizon.gameObject.activeSelf)
			{
				return;
			}

			float pitch = data.Pitch;
			float roll = -data.Roll;
			if (_viewCamera != null)
			{
				Vector3 cameraUp = _viewCamera.transform.InverseTransformDirection(Vector3.up);
				float horizontalLength = new Vector2(cameraUp.x, cameraUp.y).magnitude;
				pitch = Mathf.Atan2(cameraUp.z, horizontalLength) * Mathf.Rad2Deg;
				roll = horizontalLength > 0.0001f ? Mathf.Atan2(cameraUp.x, cameraUp.y) * Mathf.Rad2Deg : 0f;
			}

			float horizonAngle = _rollAmplitude * roll;
			float pitchOffset = _pitchAmplitude * pitch;
			float angleRadians = horizonAngle * Mathf.Deg2Rad;

			_horizon.localRotation = Quaternion.Euler(0f, 0f, horizonAngle);
			_horizon.anchoredPosition = new Vector2(-pitchOffset * Mathf.Sin(angleRadians), pitchOffset * Mathf.Cos(angleRadians));
		}

		// Registers the catalog elements found directly under the main panel and their prefab layout, once.
		private void Initialize()
		{
			if (_isInitialized)
			{
				return;
			}

			_isInitialized = true;
			_canvasRect = (RectTransform)transform;
			_defaultLayout = new FpvOsdLayout();
			Transform panel = _mainPanel != null ? _mainPanel.transform : transform;
			foreach (FpvOsdElementInfo info in FpvOsdElementCatalog.Elements)
			{
				if (panel.Find(info.Id) is not RectTransform rect)
				{
					continue;
				}

				TMP_Text[] texts = rect.TryGetComponent(out TMP_Text _) ? rect.GetComponentsInChildren<TMP_Text>(true) : System.Array.Empty<TMP_Text>();
				r_elements.Add(new Element(info, rect, texts));
				_defaultLayout.SetElement(new FpvOsdElementLayout(info.Id, rect.gameObject.activeSelf, rect.anchorMin));
			}

			_texts = GetComponentsInChildren<TMP_Text>(true);
			_textStyles = new FontStyles[_texts.Length];
			for (int index = 0; index < _texts.Length; index++)
			{
				_textStyles[index] = _texts[index].fontStyle;
			}

			if (!TryGetComponent(out _canvasGroup))
			{
				_canvasGroup = gameObject.AddComponent<CanvasGroup>();
			}

			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}

		private FpvOsdElementLayout Resolve(FpvOsdLayout layout, Element element)
		{
			if (layout != null && layout.TryGetElement(element.Info.Id, out FpvOsdElementLayout saved))
			{
				return saved;
			}

			FpvOsdElementLayout fallback = GetDefaultElement(r_elements.IndexOf(element));
			fallback.IsVisible &= element.Info.Group == FpvOsdElementGroup.Main;
			return fallback;
		}

		private FpvOsdElementLayout GetDefaultElement(int index)
		{
			_defaultLayout.TryGetElement(r_elements[index].Info.Id, out FpvOsdElementLayout element);
			return element;
		}

		private int IndexOf(string id)
		{
			Initialize();
			return r_elements.FindIndex(element => element.Info.Id == id);
		}

		private void Encapsulate(Transform owner, Vector3 localMin, Vector3 localMax, ref Vector2 min, ref Vector2 max)
		{
			Vector2 first = _canvasRect.InverseTransformPoint(owner.TransformPoint(localMin));
			Vector2 second = _canvasRect.InverseTransformPoint(owner.TransformPoint(localMax));
			min = Vector2.Min(min, Vector2.Min(first, second));
			max = Vector2.Max(max, Vector2.Max(first, second));
		}

		private void HandleSettingsChanged(FpvOsdSettings settings) => SetSettings(settings);

		private static bool IsShown(TMP_Text text) => text != null && text.gameObject.activeSelf;

		private static IFpvOsdDataSource FindDataSource(Transform start)
		{
			for (Transform node = start; node != null; node = node.parent)
			{
				foreach (MonoBehaviour behaviour in node.GetComponents<MonoBehaviour>())
				{
					if (behaviour is IFpvOsdDataSource source)
					{
						return source;
					}
				}
			}

			return null;
		}

		private sealed class Element
		{
			public Element(FpvOsdElementInfo info, RectTransform rect, TMP_Text[] texts)
			{
				Info = info;
				Rect = rect;
				Texts = texts;
			}

			public FpvOsdElementInfo Info { get; }
			public RectTransform Rect { get; }
			public TMP_Text[] Texts { get; }
		}
	}
}
