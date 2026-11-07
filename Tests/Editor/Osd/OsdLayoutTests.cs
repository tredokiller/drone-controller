using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UForce.FpvFlight.Osd.Tests
{
	public sealed class OsdLayoutTests
	{
		private const string PREFAB_PATH = "Packages/com.uforce.fpv-flight/Runtime/Osd/Prefabs/FpvOsd.prefab";
		private const float TOLERANCE = 1e-3f;

		private GameObject _instance;
		private FpvOsdView _view;
		private MemoryStore _store;

		[SetUp]
		public void SetUp()
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			Assert.That(prefab, Is.Not.Null, "the OSD prefab is missing");
			_instance = UnityEngine.Object.Instantiate(prefab);
			((RectTransform)_instance.transform).sizeDelta = new Vector2(1920f, 1080f);
			_view = _instance.GetComponent<FpvOsdView>();
			_store = new MemoryStore();
		}

		[TearDown]
		public void TearDown()
		{
			UnityEngine.Object.DestroyImmediate(_instance);
		}

		[Test]
		public void SettingsSurviveJson()
		{
			var settings = new FpvOsdSettings { Mode = FpvOsdMode.Custom };
			settings.CustomLayout.SetElement(new FpvOsdElementLayout(FpvOsdElementCatalog.ALTITUDE, false, new Vector2(0.25f, 0.75f)));
			settings.CustomLayout.Opacity = 0.5f;
			settings.CustomLayout.BoldFont = true;

			Assert.That(FpvOsdSettings.TryFromJson(settings.ToJson(), out FpvOsdSettings copy), Is.True);
			Assert.That(copy.Mode, Is.EqualTo(FpvOsdMode.Custom));
			Assert.That(copy.CustomLayout.Opacity, Is.EqualTo(0.5f));
			Assert.That(copy.CustomLayout.BoldFont, Is.True);
			Assert.That(copy.CustomLayout.TryGetElement(FpvOsdElementCatalog.ALTITUDE, out FpvOsdElementLayout altitude), Is.True);
			Assert.That(altitude.IsVisible, Is.False);
			Assert.That(altitude.Anchor, Is.EqualTo(new Vector2(0.25f, 0.75f)));
			Assert.That(FpvOsdSettings.TryFromJson("not json", out _), Is.False);
		}

		[Test]
		public void OpacityStaysReadable()
		{
			var layout = new FpvOsdLayout { Opacity = 0.05f };
			Assert.That(layout.Opacity, Is.EqualTo(FpvOsdLayout.MIN_OPACITY));
			layout.Opacity = 3f;
			Assert.That(layout.Opacity, Is.EqualTo(1f));
		}

		[Test]
		public void PrefabHasEveryCatalogElement()
		{
			foreach (FpvOsdElementInfo info in FpvOsdElementCatalog.Elements)
			{
				Assert.That(_view.HasElement(info.Id), Is.True, info.Id);
			}
		}

		[Test]
		public void StandardLayoutShowsMainAndHidesExtras()
		{
			FpvOsdLayout layout = _view.DefaultLayout;
			foreach (FpvOsdElementInfo info in FpvOsdElementCatalog.Elements)
			{
				Assert.That(layout.TryGetElement(info.Id, out FpvOsdElementLayout element), Is.True, info.Id);
				Assert.That(element.IsVisible, Is.EqualTo(info.Group == FpvOsdElementGroup.Main), info.Id);
			}
		}

		[Test]
		public void MissingElementsFallBackToStandardWithExtrasHidden()
		{
			var source = new FpvOsdLayout();
			source.SetElement(new FpvOsdElementLayout(FpvOsdElementCatalog.VTX_SIGNAL, false, new Vector2(0f, 1f)));
			source.SetElement(new FpvOsdElementLayout(FpvOsdElementCatalog.THROTTLE, true, new Vector2(1f, 1f)));

			FpvOsdLayout layout = _view.CreateLayout(source);

			Assert.That(Visible(layout, FpvOsdElementCatalog.VTX_SIGNAL), Is.False);
			Assert.That(Visible(layout, FpvOsdElementCatalog.THROTTLE), Is.True);
			Assert.That(Visible(layout, FpvOsdElementCatalog.ALTITUDE), Is.True, "a main element the layout does not name keeps its standard state");
			Assert.That(Visible(layout, FpvOsdElementCatalog.CURRENT), Is.False, "an extra element the layout does not name stays hidden");
		}

		[Test]
		public void ModesApplyTheirLayout()
		{
			var settings = new FpvOsdSettings { Mode = FpvOsdMode.Custom };
			settings.CustomLayout.SetElement(new FpvOsdElementLayout(FpvOsdElementCatalog.ALTITUDE, true, new Vector2(0.5f, 0.5f)));
			settings.CustomLayout.SetElement(new FpvOsdElementLayout(FpvOsdElementCatalog.THROTTLE, true, new Vector2(1f, 1f)));
			settings.CustomLayout.Opacity = 0.4f;
			settings.CustomLayout.BoldFont = true;
			_view.SetSettings(settings);

			RectTransform altitude = Element(FpvOsdElementCatalog.ALTITUDE);
			Assert.That(altitude.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
			Assert.That(altitude.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
			Assert.That(Element(FpvOsdElementCatalog.THROTTLE).gameObject.activeSelf, Is.True);
			Assert.That(_instance.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.4f).Within(TOLERANCE));
			Assert.That((altitude.GetComponent<TMP_Text>().fontStyle & FontStyles.Bold) != 0, Is.True);

			_view.SetMode(FpvOsdMode.Main);
			Assert.That(altitude.anchorMin, Is.EqualTo(new Vector2(1f, 1f)), "the standard altitude sits top right");
			Assert.That(Element(FpvOsdElementCatalog.THROTTLE).gameObject.activeSelf, Is.False);
			Assert.That(_instance.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
			Assert.That((altitude.GetComponent<TMP_Text>().fontStyle & FontStyles.Bold) != 0, Is.False);

			_view.SetMode(FpvOsdMode.Off);
			Assert.That(_view.IsElementShown(FpvOsdElementCatalog.ALTITUDE), Is.False);
		}

		[Test]
		public void ViewFollowsItsStore()
		{
			_view.SetSettingsStore(_store);
			Assert.That(_view.Mode, Is.EqualTo(FpvOsdMode.Main));

			_store.Save(new FpvOsdSettings { Mode = FpvOsdMode.Off });
			Assert.That(_view.Mode, Is.EqualTo(FpvOsdMode.Off));
		}

		[Test]
		public void MovedElementLandsWhereItWasDropped()
		{
			_view.SetSettingsStore(_store);
			var session = new FpvOsdEditorSession(_view, _store);
			session.Begin();
			Assert.That(session.Mode, Is.EqualTo(FpvOsdMode.Main));

			Rect before = _view.GetElementBounds(FpvOsdElementCatalog.ALTITUDE);
			var target = new Vector2(0.3f, 0.4f);
			session.Move(FpvOsdElementCatalog.ALTITUDE, target);
			Rect after = _view.GetElementBounds(FpvOsdElementCatalog.ALTITUDE);

			Assert.That(session.Mode, Is.EqualTo(FpvOsdMode.Custom), "moving an element switches to the own layout");
			Assert.That(after.xMin, Is.EqualTo(target.x).Within(TOLERANCE));
			Assert.That(after.yMin, Is.EqualTo(target.y).Within(TOLERANCE));
			Assert.That(after.size.x, Is.EqualTo(before.size.x).Within(TOLERANCE));
			Assert.That(_view.GetDefaultBounds(FpvOsdElementCatalog.ALTITUDE).min.x, Is.EqualTo(before.xMin).Within(TOLERANCE));
		}

		[Test]
		public void SaveStoresTheDraftAndCancelRestores()
		{
			_view.SetSettingsStore(_store);
			var session = new FpvOsdEditorSession(_view, _store);

			session.Begin();
			session.SetVisible(FpvOsdElementCatalog.THROTTLE, true);
			session.SetOpacity(0.6f);
			Assert.That(_view.IsElementShown(FpvOsdElementCatalog.THROTTLE), Is.True, "the draft is shown at once");
			session.Cancel();
			Assert.That(_view.IsElementShown(FpvOsdElementCatalog.THROTTLE), Is.False);
			Assert.That(_store.SaveCount, Is.EqualTo(0));

			session.Begin();
			session.SetVisible(FpvOsdElementCatalog.THROTTLE, true);
			session.SetBoldFont(true);
			session.Save();
			FpvOsdSettings saved = _store.Load();
			Assert.That(saved.Mode, Is.EqualTo(FpvOsdMode.Custom));
			Assert.That(Visible(saved.CustomLayout, FpvOsdElementCatalog.THROTTLE), Is.True);
			Assert.That(saved.CustomLayout.BoldFont, Is.True);
			Assert.That(_view.Mode, Is.EqualTo(FpvOsdMode.Custom));

			session.Begin();
			Assert.That(session.Mode, Is.EqualTo(FpvOsdMode.Custom));
			session.ResetToDefault();
			Assert.That(Visible(session.GetDraft(), FpvOsdElementCatalog.THROTTLE), Is.False);
			Assert.That(session.BoldFont, Is.False);
			session.SetMode(FpvOsdMode.Off);
			session.Save();
			Assert.That(_store.Load().Mode, Is.EqualTo(FpvOsdMode.Off));
		}

		[Test]
		public void HorizonAndReticleCannotBeMoved()
		{
			var session = new FpvOsdEditorSession(_view, _store);
			session.Begin();
			Vector2 anchor = Element(FpvOsdElementCatalog.RETICLE).anchorMin;
			session.Move(FpvOsdElementCatalog.RETICLE, new Vector2(0.1f, 0.1f));
			Assert.That(Element(FpvOsdElementCatalog.RETICLE).anchorMin, Is.EqualTo(anchor));
		}

		[Test]
		public void SnappingPrefersHomeThenGuidesAndKeepsInsideTheFrame()
		{
			var home = new Rect(0.1f, 0.1f, 0.2f, 0.05f);
			var guides = new List<Rect> { new(0f, 0f, 1f, 1f), new(0.5f, 0.6f, 0.2f, 0.05f) };
			const float pixels = 1000f;

			Assert.That(FpvOsdSnapping.PlaceAxis(0.105f, 0.2f, 0, home, guides, pixels, true), Is.EqualTo(0.1f).Within(1e-6f), "within 10 px of home");
			Assert.That(FpvOsdSnapping.PlaceAxis(0.495f, 0.2f, 0, home, guides, pixels, true), Is.EqualTo(0.5f).Within(1e-6f), "left edges line up");
			Assert.That(FpvOsdSnapping.PlaceAxis(0.795f, 0.2f, 0, home, guides, pixels, true), Is.EqualTo(0.8f).Within(1e-6f), "frame right edge");
			Assert.That(FpvOsdSnapping.PlaceAxis(0.45f, 0.2f, 0, home, guides, pixels, true), Is.EqualTo(0.45f).Within(1e-6f), "nothing close");
			Assert.That(FpvOsdSnapping.PlaceAxis(0.495f, 0.2f, 0, home, guides, pixels, false), Is.EqualTo(0.495f).Within(1e-6f), "snapping off");
			Assert.That(FpvOsdSnapping.PlaceAxis(0.95f, 0.2f, 0, home, guides, pixels, false), Is.EqualTo(0.8f).Within(1e-6f), "clamped inside");
			Assert.That(FpvOsdSnapping.PlaceAxis(-0.2f, 0.2f, 1, home, guides, pixels, false), Is.EqualTo(0f).Within(1e-6f), "clamped inside");
		}

		private RectTransform Element(string id) => (RectTransform)_instance.transform.Find("MainOsdPanel/" + id);

		private static bool Visible(FpvOsdLayout layout, string id) => layout.TryGetElement(id, out FpvOsdElementLayout element) && element.IsVisible;

		private sealed class MemoryStore : IFpvOsdSettingsStore
		{
			private FpvOsdSettings _settings;

			public event Action<FpvOsdSettings> Changed;

			public int SaveCount { get; private set; }

			public FpvOsdSettings Load() => _settings != null ? _settings.Clone() : new FpvOsdSettings();

			public void Save(FpvOsdSettings settings)
			{
				SaveCount++;
				_settings = settings.Clone();
				Changed?.Invoke(settings.Clone());
			}
		}
	}
}
