using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    public sealed class PreferencesInteractionPlayModeTests : InputTestFixture
    {
        [TestCase("MainMenu")]
        [TestCase("Gameplay")]
        public void Draft_ApplyBackResetReopen_PreservesCommittedState(string context)
        {
            var host = new GameObject("DraftTest", typeof(RectTransform), typeof(Canvas));
            var service = new FakePreferencesService();
            var view = SharedPreferencesView.Create(host.transform, context);
            // This test owns the draft/apply lifecycle semantics; a large display keeps every
            // supported resolution selectable so screen-mode switching never rewrites the draft.
            var controller = new PreferencesController(service, view,
                displayResolutionProvider: () => new Vector2Int(7680, 4320));
            try
            {
                service.Source.masterVolume = 0.31f;
                controller.Initialize();
                controller.Open();
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(0.31f));
                Assert.That(view.GetButton("apply").interactable, Is.False);
                view.GetSlider(SharedPreferencesView.MasterVolumeId).value = 0.55f;
                view.GetDropdown(SharedPreferencesView.ScreenModeId).value = 1;
                Assert.That(service.Source.masterVolume, Is.EqualTo(0.31f));
                Assert.That(service.Source.screenMode, Is.EqualTo(SettingsOptionValues.Fullscreen));
                Assert.That(controller.IsDirty, Is.True);
                controller.Close();
                controller.Open();
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(0.31f));
                Assert.That(view.GetDropdown(SharedPreferencesView.ScreenModeId).value, Is.EqualTo(0));
                view.GetSlider(SharedPreferencesView.MasterVolumeId).value = 0.55f;
                view.GetDropdown(SharedPreferencesView.ScreenModeId).value = 1;
                controller.Apply();
                Assert.That(controller.IsOpen && view.IsVisible, Is.True);
                Assert.That(service.Source.masterVolume, Is.EqualTo(0.55f));
                Assert.That(service.Source.screenMode, Is.EqualTo(SettingsOptionValues.Windowed));
                Assert.That(service.WriteCount, Is.EqualTo(2), "Apply must call only changed setters.");
                controller.Apply();
                Assert.That(service.WriteCount, Is.EqualTo(2), "Clean Apply must not persist again.");
                Assert.That(controller.IsDirty, Is.False);
                Assert.That(view.GetButton("apply").interactable, Is.False);
                view.GetSlider(SharedPreferencesView.MasterVolumeId).value = 0.91f;
                controller.Close(); controller.Open();
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(0.55f));
                controller.Reset();
                Assert.That(service.ResetCount, Is.Zero);
                Assert.That(service.Source.masterVolume, Is.EqualTo(0.55f));
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(new GameSettings().masterVolume));
                controller.Close(); controller.Open();
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(0.55f));
                controller.Reset(); controller.Apply();
                Assert.That(service.Source.masterVolume, Is.EqualTo(new GameSettings().masterVolume));
                Assert.That(service.Source.screenMode, Is.EqualTo(new GameSettings().screenMode));
                controller.SetTextSpeed(999f); controller.SetAutoForwardDelay(-1f);
                controller.SetDialogueTextScale(9f); controller.SetTextboxOpacity(-3f);
                controller.Apply();
                Assert.That(service.Source.textSpeed, Is.EqualTo(100f));
                Assert.That(service.Source.autoForwardDelay, Is.EqualTo(50f));
                Assert.That(service.Source.dialogueTextScale, Is.EqualTo(1.25f));
                Assert.That(service.Source.textboxOpacity, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void WindowedResolution_Ux_PersistsExactlyWhatItDisplays()
        {
            bool hadScreenMode = PlayerPrefs.HasKey("hif_screen_mode");
            bool hadResolution = PlayerPrefs.HasKey("hif_resolution");
            bool hadFullscreen = PlayerPrefs.HasKey("hif_fullscreen");
            string savedScreenMode = PlayerPrefs.GetString("hif_screen_mode", string.Empty);
            string savedResolution = PlayerPrefs.GetString("hif_resolution", string.Empty);
            int savedFullscreen = PlayerPrefs.GetInt("hif_fullscreen", -1);
            PlayerPrefs.DeleteKey("hif_screen_mode");
            PlayerPrefs.DeleteKey("hif_resolution");
            PlayerPrefs.DeleteKey("hif_fullscreen");
            PlayerPrefs.Save();
            GameObject managerObject = new GameObject("WindowedUxSettingsManager", typeof(SettingsManager));
            GameObject host = new GameObject("WindowedUxTest", typeof(RectTransform), typeof(Canvas));
            try
            {
                SettingsManager manager = SettingsManager.Instance;
                Assert.That(manager, Is.Not.Null);
                Assert.That(manager.CurrentSettings.screenMode, Is.EqualTo(SettingsOptionValues.Fullscreen));
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo("1920x1080"));
                var view = SharedPreferencesView.Create(host.transform, "WindowedUx");
                var controller = new PreferencesController(
                    new PreferencesService(manager), view,
                    displayResolutionProvider: () => new Vector2Int(1920, 1080));
                controller.Initialize();
                controller.Open();

                // A. Fullscreen presents the full list; staging the native-size value is valid there.
                Assert.That(view.GetDropdown(SharedPreferencesView.ResolutionId).options.Count,
                    Is.EqualTo(PreferencesOptions.Resolutions.Count));
                view.GetDropdown(SharedPreferencesView.ResolutionId).value = 2;
                // A. Switching to Windowed must visibly normalize the draft before Apply.
                view.GetDropdown(SharedPreferencesView.ScreenModeId).value = 1;
                Assert.That(view.GetDisplayedValue(SharedPreferencesView.ResolutionId), Is.EqualTo("1600x900"),
                    "Switching to Windowed must visibly normalize the native-size draft before Apply.");
                Assert.That(DropdownOptionTexts(view.GetDropdown(SharedPreferencesView.ResolutionId)),
                    Is.EqualTo(new[] { "1280x720", "1600x900" }),
                    "Windowed must present only resolutions that fit the current display.");
                controller.Apply();
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo(view.GetDisplayedValue(SharedPreferencesView.ResolutionId)),
                    "Apply must persist exactly the displayed Windowed resolution.");
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo("1600x900"));

                // B. A fitting Windowed value stays exactly as displayed.
                controller.SetResolution("1280x720");
                controller.Apply();
                Assert.That(view.GetDisplayedValue(SharedPreferencesView.ResolutionId), Is.EqualTo("1280x720"));
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo("1280x720"));

                // C. Fullscreen applies the selected resolution exactly, with the full list.
                controller.SetScreenMode(SettingsOptionValues.Fullscreen);
                controller.SetResolution("1920x1080");
                Assert.That(view.GetDropdown(SharedPreferencesView.ResolutionId).options.Count,
                    Is.EqualTo(PreferencesOptions.Resolutions.Count));
                controller.Apply();
                Assert.That(manager.CurrentSettings.screenMode, Is.EqualTo(SettingsOptionValues.Fullscreen));
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo("1920x1080"));

                // D. Borderless keeps the full list and applies the exact selected value.
                controller.SetScreenMode(SettingsOptionValues.Borderless);
                controller.SetResolution("2560x1440");
                Assert.That(view.GetDropdown(SharedPreferencesView.ResolutionId).options.Count,
                    Is.EqualTo(PreferencesOptions.Resolutions.Count));
                controller.Apply();
                Assert.That(manager.CurrentSettings.resolution, Is.EqualTo("2560x1440"));

                // Reopening shows the same persisted resolution that was actually applied.
                controller.SetScreenMode(SettingsOptionValues.Windowed);
                controller.Apply();
                string appliedResolution = manager.CurrentSettings.resolution;
                Assert.That(PlayerPrefs.GetString("hif_resolution", string.Empty), Is.EqualTo(appliedResolution));
                controller.Close();
                controller.Open();
                Assert.That(view.GetDisplayedValue(SharedPreferencesView.ResolutionId), Is.EqualTo(appliedResolution));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(managerObject);
                RestorePlayerPrefsKey("hif_screen_mode", hadScreenMode, savedScreenMode);
                RestorePlayerPrefsKey("hif_resolution", hadResolution, savedResolution);
                if (hadFullscreen) PlayerPrefs.SetInt("hif_fullscreen", savedFullscreen); else PlayerPrefs.DeleteKey("hif_fullscreen");
                PlayerPrefs.Save();
            }
        }

        private static void RestorePlayerPrefsKey(string key, bool hadValue, string value)
        {
            if (hadValue) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key);
        }

        private static List<string> DropdownOptionTexts(TMPro.TMP_Dropdown dropdown)
        {
            List<string> texts = new List<string>();
            foreach (TMPro.TMP_Dropdown.OptionData option in dropdown.options) texts.Add(option.text);
            return texts;
        }

        [UnityTest]
        public IEnumerator CategoryNavigation_EntersControlsAndFooterWithoutStaleFocus()
        {
            var host = new GameObject("CategoryTest", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("CategoryEvents", typeof(EventSystem));
            var view = SharedPreferencesView.Create(host.transform, "Navigation");
            var controller = new PreferencesController(new FakePreferencesService(), view);
            try
            {
                controller.Initialize(); controller.Open();
                yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton("category_0").gameObject));
                ExecuteEvents.Execute<IMoveHandler>(view.GetButton("category_0").gameObject,
                    new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                ExecuteEvents.Execute<ISubmitHandler>(EventSystem.current.currentSelectedGameObject,
                    new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(view.ActiveCategory, Is.EqualTo(1));
                Assert.That(view.GetDropdown(SharedPreferencesView.ScreenModeId).isActiveAndEnabled, Is.False);
                ExecuteEvents.Execute<IMoveHandler>(view.GetButton("category_1").gameObject,
                    new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetSlider(SharedPreferencesView.MasterVolumeId).gameObject));
                for (int i = 0; i < 3; i++)
                    ExecuteEvents.Execute<IMoveHandler>(EventSystem.current.currentSelectedGameObject,
                        new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton("back").gameObject));
                controller.SetMasterVolume(0.11f);
                view.GetButton("apply").Select();
                controller.Apply();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton("back").gameObject));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(events); }
        }

        [UnityTest]
        public IEnumerator FocusedControl_UpdatesContextualHintWithoutChangingDraft()
        {
            var host = new GameObject("HintTest", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("HintEvents", typeof(EventSystem));
            var service = new FakePreferencesService();
            var view = SharedPreferencesView.Create(host.transform, "Hint");
            var controller = new PreferencesController(service, view);
            try
            {
                controller.Initialize(); controller.Open();
                view.SelectCategory(1);
                EventSystem.current.SetSelectedGameObject(view.GetSlider(SharedPreferencesView.MasterVolumeId).gameObject);
                yield return null;
                Assert.That(view.CurrentContextHint, Does.Contain("Общий уровень звука"));
                Assert.That(service.Source.masterVolume, Is.EqualTo(new GameSettings().masterVolume));
                Assert.That(controller.IsDirty, Is.False);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(events); }
        }

        [UnityTest]
        public IEnumerator DropdownCancel_ReturnsFocusAndKeepsParentOpen()
        {
            var host = new GameObject("CancelTest", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var events = new GameObject("CancelEvents", typeof(EventSystem));
            var view = SharedPreferencesView.Create(host.transform, "CancelTest");
            var controller = new PreferencesController(new FakePreferencesService(), view);
            try
            {
                controller.Initialize(); controller.Open();
                yield return null;
                foreach (string id in new[] { SharedPreferencesView.ScreenModeId, SharedPreferencesView.ResolutionId })
                {
                    var dropdown = view.GetDropdown(id);
                    dropdown.Show();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    RectTransform popup = dropdown.transform.Find("Dropdown List") as RectTransform;
                    Assert.That(popup, Is.Not.Null);
                    Assert.That(popup.rect.width, Is.InRange(370f, 390f));
                    foreach (TMPro.TextMeshProUGUI label in popup.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                    {
                        var corners = new Vector3[4];
                        label.rectTransform.GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                            Assert.That(popup.rect.Contains(popup.InverseTransformPoint(corner)), Is.True, "Clipped dropdown option: " + label.text);
                    }
                    Assert.That(view.IsHandlingDropdownCancel, Is.True);
                    dropdown.OnCancel(new BaseEventData(EventSystem.current));
                    Assert.That(controller.IsOpen, Is.True);
                    yield return new WaitForSecondsRealtime(0.25f);
                    Assert.That(dropdown.IsExpanded, Is.False);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));
                }
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(events); }
        }

        [UnityTest]
        public IEnumerator MasterVolume_RealSliderPointerDragAndMoveEvents_ApplyAndClamp()
        {
            var canvasObject = new GameObject("PreferencesInteractionCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var eventObject = new GameObject("PreferencesInteractionEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var service = new FakePreferencesService();
            GameObject host = new GameObject("PreferencesInteractionHost", typeof(RectTransform));
            host.transform.SetParent(canvasObject.transform, false);
            SharedPreferencesView view = SharedPreferencesView.Create(host.transform, "PlayModeInteraction");
            var controller = new PreferencesController(service, view);
            controller.Initialize();
            service.Source.masterVolume = 0.2f;
            controller.Open();
            view.SelectCategory(1);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Slider slider = view.GetSlider(SharedPreferencesView.MasterVolumeId);
            Assert.That(slider, Is.Not.Null);
            float initial = slider.value;
            Vector3[] corners = new Vector3[4];
            slider.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 dragPoint = RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(corners[0], corners[3], 0.82f));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = dragPoint };
            ExecuteEvents.Execute<IPointerDownHandler>(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute<IDragHandler>(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IEndDragHandler>(slider.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(slider.value, Is.GreaterThan(initial));
            Assert.That(service.Source.masterVolume, Is.EqualTo(0.2f));

            EventSystem.current.SetSelectedGameObject(slider.gameObject);
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.dpad.right);
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slider.gameObject));
            Assert.That(service.Source.masterVolume, Is.EqualTo(0.2f));

            slider.value = slider.maxValue;
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
            Assert.That(slider.value, Is.EqualTo(slider.maxValue));
            slider.value = slider.minValue;
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left }, ExecuteEvents.moveHandler);
            Assert.That(slider.value, Is.EqualTo(slider.minValue));

            controller.Apply();
            Assert.That(service.Source.masterVolume, Is.EqualTo(slider.value));
            Assert.That(controller.IsOpen, Is.True);
            Object.Destroy(view.gameObject); Object.Destroy(host); Object.Destroy(canvasObject); Object.Destroy(eventObject);

        }
        private sealed class FakePreferencesService : IPreferencesService
        {
            public GameSettings Source { get; private set; } = new GameSettings();
            public PreferencesState Current => new PreferencesState(Source);
            public bool IsAvailable => true;
            public int ResetCount { get; private set; }
            public int WriteCount { get; private set; }

            public void Reset() { ResetCount++; Source = new GameSettings(); }
            public void SetMasterVolume(float value) { WriteCount++; Source.masterVolume = value; }
            public void SetMusicVolume(float value) { WriteCount++; Source.musicVolume = value; }
            public void SetSfxVolume(float value) { WriteCount++; Source.sfxVolume = value; }
            public void SetMuteAll(bool value) { WriteCount++; Source.muteAll = value; }
            public void SetScreenMode(string value) { WriteCount++; Source.screenMode = value; }
            public void SetResolution(string value) { WriteCount++; Source.resolution = value; }
            public void SetRunInBackground(bool value) { WriteCount++; Source.runInBackground = value; }
            public void SetSkipMode(string value) { WriteCount++; Source.skipMode = value; }
            public void SetSkipBehavior(string value) { WriteCount++; Source.skipBehavior = value; }
            public void SetTextSpeed(float value) { WriteCount++; Source.textSpeed = value; }
            public void SetDialogueTextScale(float value) { WriteCount++; Source.dialogueTextScale = value; }
            public void SetTextboxOpacity(float value) { WriteCount++; Source.textboxOpacity = value; }
            public void SetAutoForwardDelay(float value) { WriteCount++; Source.autoForwardDelay = value; }
            public void SetSkipAfterChoices(bool value) { WriteCount++; Source.skipAfterChoices = value; }
            public void SetAutoForward(bool value) { WriteCount++; Source.autoForward = value; }
            public void SetAutoSave(bool value) { WriteCount++; Source.autoSave = value; }
            public void SetShowQuickMenu(bool value) { WriteCount++; Source.showQuickMenu = value; }
        }
    }
}
