using System.Collections.Generic;
using UnityEngine;

namespace UForce.FpvFlight.Osd
{
	// Editing of the pilot's OSD on a live FpvOsdView, without UI (OperatorDroneScreenBridge.OsdEditor of FPV Defence
	// Simulator). The draft starts from the saved own layout, every change is shown on the view at once, and any layout
	// change switches the mode to the own layout. Save writes the mode and the layout to the store; Cancel shows what is
	// saved again.
	public sealed class FpvOsdEditorSession
	{
		private readonly FpvOsdView r_view;
		private readonly IFpvOsdSettingsStore r_store;

		private FpvOsdSettings _saved;
		private FpvOsdLayout _draft;
		private FpvOsdMode _mode;

		public FpvOsdEditorSession(FpvOsdView view, IFpvOsdSettingsStore store = null)
		{
			r_view = view;
			r_store = store ?? view.SettingsStore ?? PlayerPrefsFpvOsdSettingsStore.Default;
		}

		public bool IsOpen { get; private set; }
		public FpvOsdMode Mode => _mode;
		public FpvOsdView View => r_view;
		public float Opacity => _draft?.Opacity ?? 1f;
		public bool BoldFont => _draft != null && _draft.BoldFont;

		public void Begin()
		{
			_saved = r_store.Load();
			_mode = _saved.Mode;
			_draft = r_view.CreateLayout(_saved.CustomLayout.IsEmpty ? null : _saved.CustomLayout);
			IsOpen = true;
			r_view.SetEditPreview(true);
			ShowDraft();
		}

		public FpvOsdLayout GetDraft() => _draft?.Clone();

		public List<FpvOsdEditorElement> GetElements()
		{
			var elements = new List<FpvOsdEditorElement>();
			if (_draft == null || !IsViewAlive)
			{
				return elements;
			}

			foreach (FpvOsdElementInfo info in FpvOsdElementCatalog.Elements)
			{
				if (!r_view.HasElement(info.Id))
				{
					continue;
				}

				_draft.TryGetElement(info.Id, out FpvOsdElementLayout element);
				elements.Add(new FpvOsdEditorElement(info, element.IsVisible, r_view.IsElementShown(info.Id), r_view.GetElementBounds(info.Id), r_view.GetDefaultBounds(info.Id)));
			}

			return elements;
		}

		public void SetMode(FpvOsdMode mode)
		{
			_mode = mode;
			ShowDraft();
		}

		public void SetVisible(string id, bool isVisible)
		{
			if (!TryGetDraftElement(id, out FpvOsdElementLayout element))
			{
				return;
			}

			element.IsVisible = isVisible;
			ChangeDraft(element);
		}

		public void SetAllVisible(bool isVisible)
		{
			if (_draft == null)
			{
				return;
			}

			foreach (FpvOsdElementLayout element in new List<FpvOsdElementLayout>(_draft.Elements))
			{
				FpvOsdElementLayout changed = element;
				changed.IsVisible = isVisible;
				_draft.SetElement(changed);
			}

			ChangeDraft();
		}

		// Moves the element so that its drawn bounds start at targetMin (normalized, origin bottom left).
		public void Move(string id, Vector2 targetMin)
		{
			if (!TryGetDraftElement(id, out FpvOsdElementLayout element) || !FpvOsdElementCatalog.TryGet(id, out FpvOsdElementInfo info) || !info.IsMovable)
			{
				return;
			}

			if (!IsViewAlive)
			{
				return;
			}

			if (_mode != FpvOsdMode.Custom)
			{
				SetMode(FpvOsdMode.Custom);
			}

			element.Anchor += targetMin - r_view.GetElementBounds(id).min;
			ChangeDraft(element);
		}

		public void SetOpacity(float opacity)
		{
			if (_draft == null)
			{
				return;
			}

			_draft.Opacity = opacity;
			ChangeDraft();
		}

		public void SetBoldFont(bool isBold)
		{
			if (_draft == null)
			{
				return;
			}

			_draft.BoldFont = isBold;
			ChangeDraft();
		}

		public void ResetToDefault()
		{
			if (_draft == null || !IsViewAlive)
			{
				return;
			}

			_draft = r_view.CreateLayout(r_view.DefaultLayout);
			ChangeDraft();
		}

		public void Save()
		{
			if (!IsOpen)
			{
				return;
			}

			var settings = new FpvOsdSettings { Mode = _mode, CustomLayout = _draft.Clone() };
			End();
			if (IsViewAlive)
			{
				r_view.SetSettings(settings);
			}

			r_store.Save(settings);
		}

		public void Cancel()
		{
			if (!IsOpen)
			{
				return;
			}

			End();
			if (IsViewAlive)
			{
				r_view.SetSettings(_saved);
			}
		}

		// The view may be destroyed under an open session (its scene unloaded, play mode left).
		private bool IsViewAlive => r_view != null;

		private void End()
		{
			IsOpen = false;
			if (IsViewAlive)
			{
				r_view.SetEditPreview(false);
			}
		}

		private bool TryGetDraftElement(string id, out FpvOsdElementLayout element)
		{
			element = default;
			return _draft != null && _draft.TryGetElement(id, out element);
		}

		// Any change to the layout means the pilot wants the own layout.
		private void ChangeDraft(FpvOsdElementLayout? element = null)
		{
			if (element.HasValue)
			{
				_draft.SetElement(element.Value);
			}

			_mode = FpvOsdMode.Custom;
			ShowDraft();
		}

		private void ShowDraft()
		{
			if (_draft != null && IsViewAlive)
			{
				r_view.SetSettings(new FpvOsdSettings { Mode = _mode, CustomLayout = _draft });
			}
		}
	}
}
