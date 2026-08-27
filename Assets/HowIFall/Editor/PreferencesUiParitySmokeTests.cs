using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PreferencesUiParitySmokeTests
{
    [MenuItem("How I Fall/Tests/Run Preferences UI Parity Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Preferences UI parity smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VerifyBothContextsUseOneViewDefinition();
        VerifyTruthfulControlsAndConversions();
        VerifyMuteRestorationAndDialogueConsumers();
        VerifySaveIsolation();
    }

    private static void VerifyBothContextsUseOneViewDefinition()
    {
        GameObject canvasObject = new GameObject("PreferencesParityCanvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            GameObject mainLegacy = CreateUiObject(canvasObject.transform, "Main Menu Legacy Settings");
            SettingsPanelController mainAdapter = mainLegacy.AddComponent<SettingsPanelController>();
            mainAdapter.Show();
            SharedPreferencesView mainView = (SharedPreferencesView)typeof(SettingsPanelController)
                .GetField("sharedView", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(mainAdapter);

            GameObject gameplayOverlay = CreateUiObject(canvasObject.transform, "Gameplay Legacy Overlay");
            GameObject gameplayPanel = CreateUiObject(canvasObject.transform, "Gameplay Legacy Settings");
            var gameplayAdapter = new VNPreferencesAdapter(
                gameplayOverlay, gameplayPanel,
                null, null, null, null, null, null, null, null, null);
            var gameplayController = new PreferencesController(new FakePreferencesService(), gameplayAdapter);
            gameplayController.Initialize();
            SharedPreferencesView gameplayView = gameplayAdapter.SharedView;

            Require(mainView != null && gameplayView != null, "Both entry contexts must create a shared Preferences view.");
            Require(mainView.GetType() == typeof(SharedPreferencesView) && gameplayView.GetType() == typeof(SharedPreferencesView),
                "Main Menu and gameplay must use the same concrete view implementation.");
            Require(mainView.ContextId == "MainMenu" && gameplayView.ContextId == "Gameplay",
                "Only context identity may differ between shared view instances.");
            VerifySpaciousLayout(mainView);
            VerifySpaciousLayout(gameplayView);

            string[] expected =
            {
                SharedPreferencesView.ScreenModeId, SharedPreferencesView.ResolutionId,
                SharedPreferencesView.MasterVolumeId, SharedPreferencesView.MusicVolumeId,
                SharedPreferencesView.SfxVolumeId, SharedPreferencesView.TextSpeedId, SharedPreferencesView.AutoForwardDelayId,
                SharedPreferencesView.TextSizeId, SharedPreferencesView.TextboxOpacityId, SharedPreferencesView.SkipUnseenId,
                SharedPreferencesView.SkipAfterChoicesId, SharedPreferencesView.AutosaveId, SharedPreferencesView.ShowQuickMenuId
            };
            Require(SharedPreferencesView.VisibleControlIds.SequenceEqual(expected), "Shared Preferences visible control order changed unexpectedly.");
            Require(expected.All(id => mainView.HasControl(id) && gameplayView.HasControl(id)),
                "Both contexts must instantiate the identical visible control set.");

            string[] prohibited =
            {
                "refresh_rate", "game_look", "interface_style", "rewind_vhs_filter", "character_animations",
                "background_animations", "language", "font_size_mode", "show_hints", "auto_enabled",
                "ambient_volume", "music_during_pause", "text_outline", "textbox_width", "textbox_height",
                "run_in_background", "mute_all", "skip_speed"
            };
            Require(prohibited.All(id => !mainView.HasControl(id) && !gameplayView.HasControl(id)),
                "A fake, deferred, or B03 control leaked into the Phase 2 player-facing view.");
            Require(mainView.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.gameObject.name == "Preferences Columns") == 1
                && gameplayView.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.gameObject.name == "Preferences Columns") == 1,
                "Each shared Preferences instance must have exactly one two-column content context.");
            Require(!mainLegacy.activeSelf && !gameplayPanel.activeSelf && !gameplayOverlay.activeSelf,
                "Legacy settings surfaces must remain unreachable and hidden.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifySpaciousLayout(SharedPreferencesView view)
    {
        view.SetVisible(true);
        Canvas.ForceUpdateCanvases();
        RectTransform window = view.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.gameObject.name == "Preferences Window");
        Require(window != null
                && window.anchorMin == new Vector2(0f, 0f)
                && window.anchorMax == new Vector2(1f, 1f)
                && window.offsetMin.x >= 44f
                && window.offsetMax.x <= -44f,
            "Shared Preferences window must use a wide safe-area layout rather than a narrow fixed card.");

        Require(view.GetComponentsInChildren<ScrollRect>(true).Count(scroll => scroll.gameObject.name == "Single Scroll Viewport") == 0,
            "All Preferences controls fit at 1920x1080 and must not be hidden behind a scroll viewport mask.");
        RectTransform columns = view.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.gameObject.name == "Preferences Columns");
        Require(columns != null && columns.childCount == 2,
            "Shared Preferences must use two balanced columns at 1920x1080.");
        LayoutRebuilder.ForceRebuildLayoutImmediate(columns);
        RectTransform[] columnRects = columns.Cast<Transform>().Select(transform => transform as RectTransform).ToArray();
        Require(!Overlaps(columnRects[0], columnRects[1]), $"Preferences columns must not overlap. left={columnRects[0].rect}, right={columnRects[1].rect}");

        RectTransform viewport = window;
        foreach (TextMeshProUGUI heading in view.GetComponentsInChildren<TextMeshProUGUI>(true)
                     .Where(text => text.gameObject.name.StartsWith("Section ") || text.gameObject.name == "Label"))
        {
            Require(IsFullyInside(heading.rectTransform, viewport),
                $"Preferences heading or row label '{heading.text}' is clipped by its effective window.");
        }
        foreach (string id in SharedPreferencesView.VisibleControlIds)
        {
            RectTransform control = view.GetDropdown(id)?.GetComponent<RectTransform>()
                ?? view.GetSlider(id)?.GetComponent<RectTransform>()
                ?? view.GetToggle(id)?.GetComponent<RectTransform>();
            Require(control != null && IsFullyInside(control, viewport),
                $"Preferences control '{id}' leaves the effective window.");
        }
        Require(IsFullyInside(view.GetButton("reset").GetComponent<RectTransform>(), window)
                && IsFullyInside(view.GetButton("back").GetComponent<RectTransform>(), window),
            "Preferences footer controls must remain inside the window.");

        Require(SharedPreferencesView.VisibleControlIds
                .Select(id => view.GetDropdown(id) != null ? view.GetDropdown(id).GetComponent<LayoutElement>() : null)
                .Where(layout => layout != null)
                .All(layout => layout.preferredWidth >= 280f),
            "Preferences dropdown controls must retain a readable aligned width.");
        Require(view.GetComponentsInChildren<TMP_Dropdown>(true).Count() == 2,
            "Screen mode and resolution must remain real TMP dropdown controls.");
    }

    private static bool IsFullyInside(RectTransform child, RectTransform parent)
    {
        Vector3[] childCorners = new Vector3[4]; Vector3[] parentCorners = new Vector3[4];
        child.GetWorldCorners(childCorners); parent.GetWorldCorners(parentCorners);
        const float tolerance = 0.5f;
        return childCorners.All(corner => corner.x >= parentCorners[0].x - tolerance && corner.x <= parentCorners[2].x + tolerance
            && corner.y >= parentCorners[0].y - tolerance && corner.y <= parentCorners[2].y + tolerance);
    }

    private static bool Overlaps(RectTransform left, RectTransform right)
    {
        Vector3[] leftCorners = new Vector3[4]; Vector3[] rightCorners = new Vector3[4];
        left.GetWorldCorners(leftCorners); right.GetWorldCorners(rightCorners);
        return leftCorners[0].x < rightCorners[2].x && leftCorners[2].x > rightCorners[0].x
            && leftCorners[0].y < rightCorners[2].y && leftCorners[2].y > rightCorners[0].y;
    }

    private static void VerifyTruthfulControlsAndConversions()
    {
        GameObject host = new GameObject("PreferencesParityBindings", typeof(RectTransform));
        try
        {
            SharedPreferencesView view = SharedPreferencesView.Create(host.transform, "Test");
            var service = new FakePreferencesService();
            int closed = 0;
            var controller = new PreferencesController(service, view, onClosed: () => closed++);
            controller.Initialize();
            controller.Open();

            Require(controller.IsOpen && view.IsVisible, "Shared Preferences did not open.");
            Require(view.GetDisplayedValue(SharedPreferencesView.TextSpeedId) == "50 симв./сек.",
                "Text Speed must use player-facing character-per-second units.");
            Require(Mathf.Approximately(VNDialogueController.GetCharactersPerSecond(50f), 50f),
                "Text Speed label and VN typewriter consumer must use the same character-per-second unit.");
            string autoDelayText = view.GetDisplayedValue(SharedPreferencesView.AutoForwardDelayId);
            Require(autoDelayText.Contains("сек.") && !autoDelayText.Contains("%"),
                "Auto delay must display seconds instead of legacy percent.");

            TMP_Dropdown screenMode = view.GetDropdown(SharedPreferencesView.ScreenModeId);
            TMP_Dropdown resolution = view.GetDropdown(SharedPreferencesView.ResolutionId);
            Require(screenMode != null && resolution != null, "Screen Mode and Resolution must use real TMP dropdown controls.");
            Require(view.GetButton(SharedPreferencesView.ScreenModeId) == null && view.GetButton(SharedPreferencesView.ResolutionId) == null,
                "Display options must not fall back to cycle-on-click buttons.");
            screenMode.value = screenMode.options.FindIndex(option => option.text == SettingsOptionValues.Borderless);
            resolution.value = resolution.options.FindIndex(option => option.text == "1600x900");
            Require(service.Source.screenMode == SettingsOptionValues.Borderless && service.Source.resolution == "1600x900",
                "Selecting a dropdown option must apply the existing shared setting.");

            view.GetSlider(SharedPreferencesView.AutoForwardDelayId).value = 3.7f;
            Require(Mathf.Approximately(service.Source.autoForwardDelay, 370f), "Auto delay seconds did not roundtrip to legacy storage safely.");
            Require(Mathf.Approximately(PreferencesFormatting.AutoForwardDelaySeconds(service.Source.autoForwardDelay), 3.7f),
                "Auto delay legacy storage did not roundtrip back to seconds.");
            view.GetToggle(SharedPreferencesView.SkipUnseenId).isOn = true;
            Require(service.Source.skipMode == "Всё", "Skip unseen ON must map to the existing all-text runtime behavior.");
            view.GetToggle(SharedPreferencesView.SkipUnseenId).isOn = false;
            Require(service.Source.skipMode == "Виденное", "Skip unseen OFF must map to seen-only behavior.");

            view.GetSlider(SharedPreferencesView.MasterVolumeId).value = 0.23f;
            view.GetSlider(SharedPreferencesView.TextSizeId).value = 1.2f;
            view.GetSlider(SharedPreferencesView.TextboxOpacityId).value = 0.35f;
            view.GetToggle(SharedPreferencesView.ShowQuickMenuId).isOn = false;
            Require(!service.Source.showQuickMenu, "Show Quick Menu did not update the shared settings truth immediately.");
            Require(string.IsNullOrEmpty(view.GetDisplayedValue(SharedPreferencesView.MasterVolumeId))
                && string.IsNullOrEmpty(view.GetDisplayedValue(SharedPreferencesView.MusicVolumeId))
                && string.IsNullOrEmpty(view.GetDisplayedValue(SharedPreferencesView.SfxVolumeId)),
                "Volume sliders must not display percent labels.");
            view.GetButton("reset").onClick.Invoke();
            GameSettings defaults = new GameSettings();
            Require(service.ResetCount == 1, "Reset action did not use the shared service.");
            Require(Mathf.Approximately(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, defaults.masterVolume)
                && Mathf.Approximately(view.GetSlider(SharedPreferencesView.TextSizeId).value, defaults.dialogueTextScale)
                && Mathf.Approximately(view.GetSlider(SharedPreferencesView.TextboxOpacityId).value, defaults.textboxOpacity)
                && view.GetToggle(SharedPreferencesView.ShowQuickMenuId).isOn,
                "Reset did not refresh all newly visible controls to canonical defaults.");

            view.GetButton("back").onClick.Invoke();
            Require(!controller.IsOpen && !view.IsVisible && closed == 1, "Back did not close the shared view through its context callback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void VerifyMuteRestorationAndDialogueConsumers()
    {
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        float previousListenerVolume = AudioListener.volume;
        GameObject dialogueObject = new GameObject("PreferencesDialogueConsumer", typeof(RectTransform));
        GameObject textObject = CreateUiObject(dialogueObject.transform, "Dialogue Text");
        GameObject boxObject = CreateUiObject(dialogueObject.transform, "Dialogue Box");
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        Image box = boxObject.AddComponent<Image>();
        text.fontSize = 32f;
        text.color = Color.white;
        box.color = new Color(0.2f, 0.3f, 0.4f, 0.62f);

        try
        {
            manager.settings = new GameSettings
            {
                masterVolume = 0.63f,
                musicVolume = 0.41f,
                sfxVolume = 0.52f,
                dialogueTextScale = 1.2f,
                textboxOpacity = 0.35f
            };
            manager.SetMuteAll(true);
            Require(Mathf.Approximately(AudioListener.volume, 0f), "Mute All did not mute current player audio.");
            Require(Mathf.Approximately(manager.CurrentSettings.masterVolume, 0.63f)
                && Mathf.Approximately(manager.CurrentSettings.musicVolume, 0.41f)
                && Mathf.Approximately(manager.CurrentSettings.sfxVolume, 0.52f),
                "Mute All destructively changed useful slider values.");
            manager.SetMuteAll(false);
            Require(Mathf.Approximately(AudioListener.volume, 0.63f), "Unmute did not restore the previous useful Master Volume.");

            VNDialogueController.ApplyDialoguePresentation(text, box, 32f, manager.CurrentSettings);
            Require(Mathf.Approximately(text.fontSize, 38.4f), "Text Size does not alter actual VN dialogue TMP rendering.");
            Require(Mathf.Approximately(box.color.a, 0.35f), "Textbox Opacity does not alter the actual dialogue background.");
            Require(Mathf.Approximately(text.color.a, 1f), "Textbox Opacity incorrectly made dialogue text transparent.");
        }
        finally
        {
            manager.settings = previousSettings;
            manager.SaveSettings();
            AudioListener.volume = previousListenerVolume;
            UnityEngine.Object.DestroyImmediate(dialogueObject);
        }
    }

    private static void VerifySaveIsolation()
    {
        Require(SaveData.CurrentVersion == 3, "Preferences UI parity must preserve SaveData v3.");
        string json = JsonUtility.ToJson(new SaveData());
        Require(json.IndexOf("preferences", StringComparison.OrdinalIgnoreCase) < 0
            && json.IndexOf("textboxOpacity", StringComparison.OrdinalIgnoreCase) < 0
            && json.IndexOf("dialogueTextScale", StringComparison.OrdinalIgnoreCase) < 0,
            "Preferences state leaked into campaign SaveData JSON.");
        Require(!typeof(PreferencesService).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(GameSettings)), "PreferencesService gained a second GameSettings cache.");
    }

    private static SettingsManager GetSettingsManager()
    {
        if (SettingsManager.Instance != null)
        {
            return SettingsManager.Instance;
        }

        return new GameObject("Preferences UI Parity Settings Manager").AddComponent<SettingsManager>();
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        public GameSettings Source { get; private set; } = new GameSettings();
        public PreferencesState Current => new PreferencesState(Source);
        public bool IsAvailable => true;
        public int ResetCount { get; private set; }

        public void Reset() { ResetCount++; Source = new GameSettings(); }
        public void SetMasterVolume(float value) => Source.masterVolume = value;
        public void SetMusicVolume(float value) => Source.musicVolume = value;
        public void SetSfxVolume(float value) => Source.sfxVolume = value;
        public void SetMuteAll(bool value) => Source.muteAll = value;
        public void SetScreenMode(string value) => Source.screenMode = value;
        public void SetResolution(string value) => Source.resolution = value;
        public void SetRunInBackground(bool value) => Source.runInBackground = value;
        public void SetSkipMode(string value) => Source.skipMode = value;
        public void SetSkipBehavior(string value) => Source.skipBehavior = value;
        public void SetTextSpeed(float value) => Source.textSpeed = value;
        public void SetDialogueTextScale(float value) => Source.dialogueTextScale = value;
        public void SetTextboxOpacity(float value) => Source.textboxOpacity = value;
        public void SetAutoForwardDelay(float value) => Source.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => Source.skipAfterChoices = value;
        public void SetAutoForward(bool value) => Source.autoForward = value;
        public void SetAutoSave(bool value) => Source.autoSave = value;
        public void SetShowQuickMenu(bool value) => Source.showQuickMenu = value;
    }
}
