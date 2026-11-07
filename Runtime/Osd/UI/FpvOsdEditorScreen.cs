using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace UForce.FpvFlight.Osd.UI
{
	// A ready OSD editor screen on UI Toolkit (OperatorOsdEditorView of FPV Defence Simulator): a sidebar with the OSD
	// mode, all elements, snapping, bold font, opacity and one switch per element, and the drone video with a frame
	// over every movable element that can be dragged. It edits through FpvOsdEditorSession, so the OSD itself shows
	// every change. Create it once, Open it on an OSD view and its video, and it closes on Save or Cancel.
	// Runtime UI Toolkit takes pointer input from an EventSystem; a scene without one (a project on the Input System gets
	// no pointer input at all then) gets a temporary one while the editor is open.
	[DisallowMultipleComponent]
	public sealed class FpvOsdEditorScreen : MonoBehaviour
	{
		private const string STYLE_PATH = "UForce/FpvFlight/FpvOsdEditor";
		private const string THEME_PATH = "UForce/FpvFlight/FpvOsdEditorTheme";
		private const string HIDDEN_CLASS = "hidden";
		private const string SELECTED_CLASS = "is-selected";
		private const string DRAGGING_CLASS = "is-dragging";
		private const float REFRESH_INTERVAL = 0.05f;

		private static readonly (FpvOsdMode Mode, string Label)[] s_modes =
		{
			(FpvOsdMode.Main, "Стандартний"),
			(FpvOsdMode.Custom, "Власний"),
			(FpvOsdMode.Off, "Вимкнено")
		};

		private readonly List<Button> r_modeButtons = new();
		private readonly Dictionary<string, Toggle> r_elementToggles = new();
		private readonly Dictionary<string, VisualElement> r_frames = new();

		private UIDocument _document;
		private PanelSettings _panelSettings;
		private VisualElement _overlay;
		private VisualElement _viewport;
		private Image _screen;
		private Toggle _allToggle;
		private Toggle _snapToggle;
		private Toggle _boldToggle;
		private Slider _opacitySlider;
		private Label _opacityValue;
		private ScrollView _elementList;

		private FpvOsdEditorSession _session;
		private List<FpvOsdEditorElement> _elements = new();
		private Camera _redirectedCamera;
		private RenderTexture _previousTarget;
		private RenderTexture _redirectTexture;
		private GameObject _eventSystem;
		private CursorLockMode _previousLockState;
		private bool _wasCursorVisible;
		private float _nextRefreshTime;

		private string _dragId;
		private Vector2 _dragStart;
		private Rect _dragStartBounds;
		private Rect _dragHome;
		private List<Rect> _snapGuides;

		public bool IsOpen => _session != null && _session.IsOpen;

		// True when the pilot saved, false when the edit was cancelled.
		public event Action<bool> Closed;

		public static FpvOsdEditorScreen Create(int sortingOrder = 1000)
		{
			var screen = new GameObject("[FPV OSD Editor]").AddComponent<FpvOsdEditorScreen>();
			screen.Build(sortingOrder);
			return screen;
		}

		// Shows the video of this camera in the editor: the camera renders into a texture while the editor is open.
		public void Open(FpvOsdView view, Camera videoCamera, IFpvOsdSettingsStore store = null)
		{
			if (view == null || IsOpen)
			{
				return;
			}

			if (videoCamera == null)
			{
				Open(view, (Texture)null, store);
				return;
			}

			_redirectedCamera = videoCamera;
			_previousTarget = videoCamera.targetTexture;
			_redirectTexture = new RenderTexture(Mathf.Max(16, Screen.width), Mathf.Max(16, Screen.height), 24, RenderTextureFormat.ARGB32)
			{
				name = "FPV OSD Editor Video",
				hideFlags = HideFlags.DontSave
			};

			_redirectTexture.Create();
			videoCamera.targetTexture = _redirectTexture;
			Open(view, _redirectTexture, store);
		}

		public void Open(FpvOsdView view, Texture videoTexture, IFpvOsdSettingsStore store = null)
		{
			if (view == null || IsOpen)
			{
				return;
			}

			_session = new FpvOsdEditorSession(view, store);
			_session.Begin();
			EnsureEventSystem();
			_screen.image = videoTexture;
			BuildElementList();
			SyncControls();

			_previousLockState = UnityEngine.Cursor.lockState;
			_wasCursorVisible = UnityEngine.Cursor.visible;
			UnityEngine.Cursor.lockState = CursorLockMode.None;
			UnityEngine.Cursor.visible = true;

			SetVisible(_overlay, true);
			FitScreen();
			RefreshFrames();
		}

		// Cancels an open edit.
		public void Close()
		{
			if (!IsOpen)
			{
				return;
			}

			_session.Cancel();
			Finish(false);
		}

		// Leaving play mode destroys the OSD before the editor; the session then only lets go of it.
		private void OnDestroy()
		{
			if (IsOpen)
			{
				_session.Cancel();
			}

			RestoreCamera();
			ReleaseEventSystem();
			if (_panelSettings != null)
			{
				Destroy(_panelSettings);
			}
		}

		private void Update()
		{
			if (IsOpen && Time.unscaledTime >= _nextRefreshTime)
			{
				RefreshFrames();
			}
		}

		private void Build(int sortingOrder)
		{
			_panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			_panelSettings.name = "FPV OSD Editor Panel";
			_panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(THEME_PATH);
			_panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			_panelSettings.referenceResolution = new Vector2Int(1920, 1080);
			_panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
			_panelSettings.match = 0.5f;
			_panelSettings.sortingOrder = sortingOrder;

			_document = gameObject.AddComponent<UIDocument>();
			_document.panelSettings = _panelSettings;

			VisualElement root = _document.rootVisualElement;
			var style = Resources.Load<StyleSheet>(STYLE_PATH);
			if (style != null)
			{
				root.styleSheets.Add(style);
			}

			BuildTree(root);
			SetVisible(_overlay, false);
		}

		private void BuildTree(VisualElement root)
		{
			_overlay = Element("fpv-osd-editor-overlay");
			root.Add(_overlay);

			VisualElement sidebar = Element("fpv-osd-editor-sidebar");
			_overlay.Add(sidebar);
			sidebar.Add(Text("НАЛАШТУВАННЯ OSD", "fpv-osd-editor-title"));

			VisualElement modes = Element("fpv-osd-editor-modes");
			foreach ((FpvOsdMode mode, string label) in s_modes)
			{
				var button = new Button(() => HandleModeClicked(mode)) { text = label };
				button.AddToClassList("fpv-osd-editor-mode");
				r_modeButtons.Add(button);
				modes.Add(button);
			}

			sidebar.Add(modes);

			_allToggle = AddToggleRow(sidebar, "Усі елементи", false);
			_allToggle.RegisterValueChangedCallback(evt => HandleAllChanged(evt.newValue));
			_snapToggle = AddToggleRow(sidebar, "Прив'язка до країв і елементів", true);
			_boldToggle = AddToggleRow(sidebar, "Жирний шрифт", false);
			_boldToggle.RegisterValueChangedCallback(evt => HandleBoldChanged(evt.newValue));

			VisualElement opacityRow = Element("fpv-osd-editor-row");
			Label opacityLabel = Text("Непрозорість", "fpv-osd-editor-row-label");
			opacityLabel.style.flexGrow = 0;
			opacityRow.Add(opacityLabel);
			_opacitySlider = new Slider(FpvOsdLayout.MIN_OPACITY * 100f, 100f) { value = 100f };
			_opacitySlider.AddToClassList("fpv-osd-editor-slider");
			_opacitySlider.RegisterValueChangedCallback(evt => HandleOpacityChanged(evt.newValue));
			opacityRow.Add(_opacitySlider);
			_opacityValue = Text("100%", "fpv-osd-editor-number");
			opacityRow.Add(_opacityValue);
			sidebar.Add(opacityRow);

			_elementList = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			_elementList.AddToClassList("fpv-osd-editor-elements");
			sidebar.Add(_elementList);

			VisualElement actions = Element("fpv-osd-editor-actions");
			Button save = ActionButton("ЗБЕРЕГТИ", HandleSaveClicked);
			save.AddToClassList("is-primary");
			actions.Add(save);
			actions.Add(ActionButton("ВІДМІНИТИ", Close));
			actions.Add(ActionButton("СКИНУТИ", HandleResetClicked));
			sidebar.Add(actions);

			_viewport = Element("fpv-osd-editor-viewport");
			_overlay.Add(_viewport);
			_screen = new Image { scaleMode = ScaleMode.StretchToFill };
			_screen.AddToClassList("fpv-osd-editor-screen");
			_viewport.Add(_screen);

			_viewport.RegisterCallback<GeometryChangedEvent>(_ => FitScreen());
			_screen.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
			_screen.RegisterCallback<PointerUpEvent>(HandlePointerUp);
			_screen.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
		}

		private void BuildElementList()
		{
			_elementList.Clear();
			r_elementToggles.Clear();
			foreach (VisualElement frame in r_frames.Values)
			{
				frame.RemoveFromHierarchy();
			}

			r_frames.Clear();
			_elements = _session.GetElements();
			FpvOsdElementGroup? group = null;
			foreach (FpvOsdEditorElement element in _elements)
			{
				if (group != element.Info.Group)
				{
					group = element.Info.Group;
					_elementList.Add(Text(FpvOsdElementCatalog.GetGroupLabel(element.Info.Group), "fpv-osd-editor-group"));
				}

				string id = element.Info.Id;
				Toggle toggle = AddToggleRow(_elementList, element.Info.Label, element.IsVisible);
				toggle.RegisterValueChangedCallback(evt => HandleElementChanged(id, evt.newValue));
				r_elementToggles[id] = toggle;

				if (!element.Info.IsMovable)
				{
					continue;
				}

				VisualElement frame = Element("fpv-osd-editor-frame");
				frame.RegisterCallback<PointerDownEvent>(evt => HandleFramePointerDown(id, evt));
				r_frames[id] = frame;
				_screen.Add(frame);
			}
		}

		private void SyncControls()
		{
			for (int index = 0; index < s_modes.Length; index++)
			{
				r_modeButtons[index].EnableInClassList(SELECTED_CLASS, s_modes[index].Mode == _session.Mode);
			}

			FpvOsdLayout draft = _session.GetDraft();
			bool isAllVisible = true;
			foreach (KeyValuePair<string, Toggle> pair in r_elementToggles)
			{
				bool isVisible = draft.TryGetElement(pair.Key, out FpvOsdElementLayout element) && element.IsVisible;
				pair.Value.SetValueWithoutNotify(isVisible);
				isAllVisible &= isVisible;
			}

			_allToggle.SetValueWithoutNotify(isAllVisible);
			_boldToggle.SetValueWithoutNotify(_session.BoldFont);
			_opacitySlider.SetValueWithoutNotify(_session.Opacity * 100f);
			_opacityValue.text = $"{_session.Opacity * 100f:F0}%";
		}

		private void RefreshFrames()
		{
			_nextRefreshTime = Time.unscaledTime + REFRESH_INTERVAL;
			_elements = _session.GetElements();
			foreach (FpvOsdEditorElement element in _elements)
			{
				if (!r_frames.TryGetValue(element.Info.Id, out VisualElement frame))
				{
					continue;
				}

				SetVisible(frame, element.IsShown);
				IStyle style = frame.style;
				style.left = Length.Percent(element.Bounds.xMin * 100f);
				style.top = Length.Percent((1f - element.Bounds.yMax) * 100f);
				style.width = Length.Percent(element.Bounds.width * 100f);
				style.height = Length.Percent(element.Bounds.height * 100f);
			}
		}

		private void FitScreen()
		{
			Texture texture = _screen.image;
			Rect area = _viewport.contentRect;
			if (float.IsNaN(area.width) || area.width <= 0f || area.height <= 0f)
			{
				return;
			}

			float aspect = texture != null ? (float)texture.width / texture.height : 16f / 9f;
			float width = Mathf.Min(area.width, area.height * aspect);
			float height = width / aspect;
			_screen.style.left = (area.width - width) * 0.5f;
			_screen.style.top = (area.height - height) * 0.5f;
			_screen.style.width = width;
			_screen.style.height = height;
		}

		private void HandleFramePointerDown(string id, PointerDownEvent evt)
		{
			if (evt.button != 0 || !TryGetElement(id, out FpvOsdEditorElement element))
			{
				return;
			}

			_dragId = id;
			_dragStart = evt.position;
			_dragStartBounds = element.Bounds;
			_dragHome = element.DefaultBounds;
			_snapGuides = FpvOsdSnapping.CollectGuides(_elements, id);
			r_frames[id].AddToClassList(DRAGGING_CLASS);
			_screen.CapturePointer(evt.pointerId);
			evt.StopImmediatePropagation();
		}

		private void HandlePointerMove(PointerMoveEvent evt)
		{
			if (_dragId == null || !_screen.HasPointerCapture(evt.pointerId))
			{
				return;
			}

			Vector2 size = _screen.worldBound.size;
			if (size.x <= 0f || size.y <= 0f)
			{
				return;
			}

			Vector2 delta = ((Vector2)evt.position - _dragStart) / size;
			Vector2 target = FpvOsdSnapping.Place(_dragStartBounds, new Vector2(delta.x, -delta.y), _dragHome, _snapGuides, size, _snapToggle.value);
			_session.Move(_dragId, target);
			RefreshFrames();
			SyncControls();
			evt.StopImmediatePropagation();
		}

		private void HandlePointerUp(PointerUpEvent evt)
		{
			if (_screen.HasPointerCapture(evt.pointerId))
			{
				_screen.ReleasePointer(evt.pointerId);
			}
		}

		private void EndDrag()
		{
			if (_dragId != null && r_frames.TryGetValue(_dragId, out VisualElement frame))
			{
				frame.RemoveFromClassList(DRAGGING_CLASS);
			}

			_dragId = null;
		}

		private void HandleModeClicked(FpvOsdMode mode)
		{
			_session.SetMode(mode);
			SyncControls();
			RefreshFrames();
		}

		private void HandleElementChanged(string id, bool isVisible)
		{
			_session.SetVisible(id, isVisible);
			SyncControls();
			RefreshFrames();
		}

		private void HandleAllChanged(bool isVisible)
		{
			_session.SetAllVisible(isVisible);
			SyncControls();
			RefreshFrames();
		}

		private void HandleBoldChanged(bool isBold)
		{
			_session.SetBoldFont(isBold);
			SyncControls();
			RefreshFrames();
		}

		private void HandleOpacityChanged(float percent)
		{
			_session.SetOpacity(percent / 100f);
			SyncControls();
		}

		private void HandleResetClicked()
		{
			_session.ResetToDefault();
			SyncControls();
			RefreshFrames();
		}

		private void HandleSaveClicked()
		{
			_session.Save();
			Finish(true);
		}

		private void Finish(bool isSaved)
		{
			EndDrag();
			SetVisible(_overlay, false);
			_screen.image = null;
			RestoreCamera();
			ReleaseEventSystem();
			UnityEngine.Cursor.lockState = _previousLockState;
			UnityEngine.Cursor.visible = _wasCursorVisible;
			Closed?.Invoke(isSaved);
		}

		private void EnsureEventSystem()
		{
			if (EventSystem.current != null || _eventSystem != null)
			{
				return;
			}

			_eventSystem = new GameObject("[FPV OSD Editor Input]", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
			_eventSystem.AddComponent<InputSystemUIInputModule>();
#else
			_eventSystem.AddComponent<StandaloneInputModule>();
#endif
		}

		private void ReleaseEventSystem()
		{
			if (_eventSystem != null)
			{
				Destroy(_eventSystem);
			}

			_eventSystem = null;
		}

		private void RestoreCamera()
		{
			if (_redirectedCamera != null)
			{
				_redirectedCamera.targetTexture = _previousTarget;
			}

			_redirectedCamera = null;
			_previousTarget = null;
			if (_redirectTexture != null)
			{
				_redirectTexture.Release();
				Destroy(_redirectTexture);
				_redirectTexture = null;
			}
		}

		private bool TryGetElement(string id, out FpvOsdEditorElement element)
		{
			foreach (FpvOsdEditorElement item in _elements)
			{
				if (item.Info.Id == id)
				{
					element = item;
					return true;
				}
			}

			element = default;
			return false;
		}

		private static Toggle AddToggleRow(VisualElement parent, string label, bool value)
		{
			VisualElement row = Element("fpv-osd-editor-row");
			row.Add(Text(label, "fpv-osd-editor-row-label"));
			var toggle = new Toggle();
			toggle.SetValueWithoutNotify(value);
			row.Add(toggle);
			// The whole row switches, not only the box; a click on the box is the toggle's own.
			row.RegisterCallback<ClickEvent>(evt =>
			{
				if (evt.target == row || evt.target is Label)
				{
					toggle.value = !toggle.value;
				}
			});
			parent.Add(row);
			return toggle;
		}

		private static Button ActionButton(string text, Action clicked)
		{
			var button = new Button(clicked) { text = text };
			button.AddToClassList("fpv-osd-editor-button");
			return button;
		}

		private static VisualElement Element(string className)
		{
			var element = new VisualElement();
			element.AddToClassList(className);
			return element;
		}

		private static Label Text(string text, string className)
		{
			var label = new Label(text);
			label.AddToClassList(className);
			return label;
		}

		private static void SetVisible(VisualElement element, bool isVisible) => element.EnableInClassList(HIDDEN_CLASS, !isVisible);
	}
}
