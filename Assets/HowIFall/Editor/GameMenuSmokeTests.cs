using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameMenuSmokeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("How I Fall/Tests/Run Game Menu Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Game Menu smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        VerifyActionSetsAndResponsiveStructure();
        VerifyEscapeBlockingAndQuickMenuOwnership();
        VerifyExistingPanelRoutingAndConfirmations();
        VerifySaveAndPersistentStateIsolation();
    }

    private static void VerifyActionSetsAndResponsiveStructure()
    {
        GameObject canvasObject = new GameObject("Game Menu View Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            VNGameMenuView view = VNGameMenuView.Create(canvasObject.transform);
            Require(view != null, "Runtime Game Menu view was not created.");
            Require(view.GetComponentsInChildren<Canvas>(true).Length == 0, "Game Menu must reuse the VN Canvas instead of creating another Canvas.");
            Require(view.GetComponent<Image>() != null && view.GetComponent<Image>().raycastTarget,
                "Full-screen Game Menu root must block clicks to dialogue, choices, and Quick Menu underneath.");

            view.SetReplayMode(false);
            AssertVisibleActions(view, new[]
            {
                VNGameMenuAction.Save, VNGameMenuAction.Load, VNGameMenuAction.Preferences,
                VNGameMenuAction.MainMenu, VNGameMenuAction.Quit, VNGameMenuAction.Return
            });
            AssertLabel(view, VNGameMenuAction.Save, "Сохранить");
            AssertLabel(view, VNGameMenuAction.Load, "Загрузить");
            AssertLabel(view, VNGameMenuAction.Preferences, "Настройки");
            AssertLabel(view, VNGameMenuAction.MainMenu, "Главное меню");
            AssertLabel(view, VNGameMenuAction.Quit, "Выйти");
            AssertLabel(view, VNGameMenuAction.Return, "Вернуться");
            Require(!view.IsActionVisible(VNGameMenuAction.History), "History leaked into the normal Game Menu.");
            Require(!view.IsActionVisible(VNGameMenuAction.Characters), "Characters leaked into the normal Game Menu.");

            view.SetReplayMode(true);
            AssertVisibleActions(view, new[]
            {
                VNGameMenuAction.Preferences, VNGameMenuAction.History,
                VNGameMenuAction.EndReplay, VNGameMenuAction.Quit, VNGameMenuAction.Return
            });
            AssertLabel(view, VNGameMenuAction.EndReplay, "Завершить повтор");
            Require(!view.IsActionVisible(VNGameMenuAction.Save)
                && !view.IsActionVisible(VNGameMenuAction.Load)
                && !view.IsActionVisible(VNGameMenuAction.Characters)
                && !view.IsActionVisible(VNGameMenuAction.MainMenu),
                "Replay leaked campaign-only navigation actions.");

            RectTransform window = view.transform.Find("Game Menu Window") as RectTransform;
            RectTransform navigation = window != null ? window.Find("Navigation") as RectTransform : null;
            RectTransform primaryActions = navigation != null ? navigation.Find("Primary Actions") as RectTransform : null;
            RectTransform returnArea = navigation != null ? navigation.Find("Return Area") as RectTransform : null;
            Require(window != null && navigation != null && primaryActions != null && returnArea != null,
                "Responsive compact Game Menu navigation structure is missing.");
            Require(window.anchorMin.x > 0f && window.anchorMax.x < 1f && window.anchorMin.y > 0f && window.anchorMax.y < 1f,
                "Game Menu window must use proportional safe margins.");
            float navigationWidth = navigation.anchorMax.x - navigation.anchorMin.x;
            Require(navigationWidth >= 0.25f && navigationWidth <= 0.30f,
                $"Game Menu navigation width must remain compact (25-30%); actual={navigationWidth:0.00}.");
            Require(primaryActions.GetComponent<VerticalLayoutGroup>() != null,
                "Game Menu navigation must use deterministic layout instead of pixel-coordinate button placement.");
            Require(window.Find("Context Area") == null,
                "Game Menu retained the decorative empty Context Area placeholder.");
            Require(view.GetComponentsInChildren<TextMeshProUGUI>(true).All(text => text.text != "НАВИГАЦИЯ"
                && text.text != "Выберите раздел. Esc возвращает к игре."),
                "Game Menu retained placeholder navigation copy.");
            Require(view.GetButton(VNGameMenuAction.Return).transform.IsChildOf(returnArea),
                "Return must remain visually separated in the bottom navigation area.");
            Require(returnArea.anchorMax.y < primaryActions.anchorMin.y,
                "Return area overlaps the primary navigation block.");
            Require(view.GetButton(VNGameMenuAction.Save).colors.highlightedColor
                != view.GetButton(VNGameMenuAction.Save).colors.normalColor,
                "Game Menu hover feedback is not visually distinct.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifyEscapeBlockingAndQuickMenuOwnership()
    {
        Harness harness = CreateHarness("Game Menu Escape Harness");
        try
        {
            int lineBefore = GetPrivate<int>(harness.Dialogue, "currentLineIndex");
            Require(harness.Dialogue.HandleEscapePressed(), "Stable ordinary dialogue Escape was not handled.");
            Require(harness.Menu.IsOpen && harness.Menu.IsPresentationVisible, "Stable ordinary dialogue Escape did not open Game Menu.");
            Require(!harness.Dialogue.CanAdvanceDialogue, "Game Menu did not block dialogue advance.");
            Require(GetPrivate<bool>(harness.QuickMenu, "hiddenByGameMenuModal"), "Game Menu did not acquire its Quick Menu blocker.");
            Require(!harness.QuickRoot.activeSelf, "Quick Menu remained visible under Game Menu.");

            Require(harness.Dialogue.HandleEscapePressed(), "Second Escape was not handled.");
            Require(!harness.Menu.IsOpen, "Second Escape did not close Game Menu.");
            Require(GetPrivate<int>(harness.Dialogue, "currentLineIndex") == lineBefore, "Open/Return advanced the dialogue line.");
            Require(!GetPrivate<bool>(harness.QuickMenu, "hiddenByGameMenuModal"), "Closing Game Menu did not remove its own blocker.");

            harness.QuickMenu.SetPlayerInterfaceHidden(true);
            Require(harness.Dialogue.HandleEscapePressed() && harness.Menu.IsOpen, "Game Menu did not reopen for blocker composition test.");
            Require(harness.Dialogue.HandleEscapePressed() && !harness.Menu.IsOpen, "Game Menu did not close for blocker composition test.");
            Require(!harness.QuickRoot.activeSelf, "Closing Game Menu overrode the H/clean-view Quick Menu blocker.");
            harness.QuickMenu.SetPlayerInterfaceHidden(false);

            SetPrivate(harness.Dialogue, "isInterfaceHidden", true);
            harness.QuickMenu.SetPlayerInterfaceHidden(true);
            Require(harness.Dialogue.HandleEscapePressed(), "Hidden-interface Escape was not handled.");
            Require(!harness.Dialogue.IsInterfaceHidden && !harness.Menu.IsOpen,
                "One Escape restored clean view and incorrectly opened Game Menu on the same press.");

            GameObject specialOwner = new GameObject("Blocking Special Owner");
            try
            {
                Require(harness.Dialogue.TryEnterSpecialMode(specialOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease),
                    "BlockingExclusive setup failed.");
                Require(harness.Dialogue.HandleEscapePressed() && !harness.Menu.IsOpen,
                    "BlockingExclusive allowed Game Menu to open.");
                Require(harness.Dialogue.ExitSpecialMode(lease), "BlockingExclusive cleanup failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(specialOwner);
            }

            CharacterHubController hub = harness.Dialogue.gameObject.AddComponent<CharacterHubController>();
            hub.panel = new GameObject("Open Character Hub");
            hub.panel.transform.SetParent(harness.Canvas.transform, false);
            hub.panel.SetActive(true);
            harness.Dialogue.characterHubController = hub;
            Require(harness.Dialogue.HandleEscapePressed(), "Character Hub Escape was not handled.");
            Require(!hub.IsOpen && !harness.Menu.IsOpen, "Character Hub Escape also opened Game Menu on the same press.");

            MethodInfo quickMenuRoute = typeof(VNQuickMenu).GetMethod("HandleMainMenuAction", PrivateInstance);
            Require(quickMenuRoute != null, "Quick Menu Menu route was not found.");
            Require(harness.QuickMenu.historyButton != null && harness.QuickMenu.historyButton.gameObject.activeSelf,
                "Normal Game Menu cleanup removed Quick Menu History access.");
            Require(harness.QuickMenu.charactersButton != null && harness.QuickMenu.charactersButton.gameObject.activeSelf,
                "Normal Game Menu cleanup removed the existing Characters access route.");

            quickMenuRoute.Invoke(harness.QuickMenu, null);
            Require(harness.Menu.IsOpen, "Quick Menu Menu route did not open the new Game Menu.");
            harness.Menu.Close();

        }
        finally
        {
            harness.Dispose();
        }
    }

    private static void VerifyExistingPanelRoutingAndConfirmations()
    {
        Harness harness = CreateHarness("Game Menu Routing Harness");
        SaveManager saveManagerBefore = SaveManager.Instance;
        try
        {
            GameObject legacyPanel = new GameObject("Legacy Preferences Context", typeof(RectTransform));
            legacyPanel.transform.SetParent(harness.Canvas.transform, false);
            var adapter = new VNPreferencesAdapter(null, legacyPanel, null, null, null, null, null, null, null, null, null);
            var preferences = new PreferencesController(
                new FakePreferencesService(),
                adapter,
                onClosed: () => typeof(VNDialogueController).GetMethod("ResumeAfterPreferencesClosed", PrivateInstance)?.Invoke(harness.Dialogue, null));
            preferences.Initialize();
            SetPrivate(harness.Dialogue, "preferencesController", preferences);

            Require(harness.Menu.Open(), "Game Menu did not open for Preferences routing.");
            harness.Menu.View.GetButton(VNGameMenuAction.Preferences).onClick.Invoke();
            Require(preferences.IsOpen && adapter.SharedView.IsVisible, "Game Menu did not open the existing SharedPreferencesView.");
            Require(harness.Menu.IsOpen && !harness.Menu.IsPresentationVisible, "Preferences did not become the top owner over Game Menu.");
            adapter.SharedView.GetButton("back").onClick.Invoke();
            Require(!preferences.IsOpen && harness.Menu.IsPresentationVisible, "Preferences Back did not return to Game Menu.");

            GameObject saveLoadObject = new GameObject("Existing Manual Save Load Panel");
            saveLoadObject.transform.SetParent(harness.Canvas.transform, false);
            saveLoadObject.SetActive(false);
            ManualSaveLoadPanel saveLoad = saveLoadObject.AddComponent<ManualSaveLoadPanel>();
            harness.Dialogue.manualSaveLoadPanel = saveLoad;
            harness.Menu.View.GetButton(VNGameMenuAction.Save).onClick.Invoke();
            Require(saveLoad.IsOpen && saveLoad.IsSaveMode && !harness.Menu.IsPresentationVisible,
                "Game Menu Save did not use the existing ManualSaveLoadPanel save flow.");
            saveLoadObject.SetActive(false);
            typeof(VNGameMenuController).GetMethod("Update", PrivateInstance)?.Invoke(harness.Menu, null);
            Require(harness.Menu.IsPresentationVisible, "Closing Save did not restore the Phase 3 Game Menu parent.");
            harness.Menu.View.GetButton(VNGameMenuAction.Load).onClick.Invoke();
            Require(saveLoad.IsOpen && !saveLoad.IsSaveMode && !harness.Menu.IsPresentationVisible,
                "Game Menu Load did not use the existing ManualSaveLoadPanel load flow.");
            saveLoadObject.SetActive(false);
            typeof(VNGameMenuController).GetMethod("Update", PrivateInstance)?.Invoke(harness.Menu, null);
            Require(harness.Menu.IsPresentationVisible, "Closing Load did not restore the Phase 3 Game Menu parent.");

            harness.Dialogue.confirmExitPanel = new GameObject("Existing Main Menu Confirmation");
            harness.Dialogue.confirmExitPanel.transform.SetParent(harness.Canvas.transform, false);
            harness.Dialogue.confirmExitPanel.SetActive(false);
            harness.Menu.View.GetButton(VNGameMenuAction.MainMenu).onClick.Invoke();
            Require(harness.Dialogue.confirmExitPanel.activeSelf && !harness.Menu.IsPresentationVisible,
                "Main Menu action bypassed or failed to use the existing confirmation.");
            harness.Dialogue.HideConfirmExit();
            Require(!harness.Dialogue.confirmExitPanel.activeSelf && harness.Menu.IsPresentationVisible,
                "Cancelling Main Menu confirmation did not return to Game Menu.");

            harness.Menu.View.GetButton(VNGameMenuAction.Quit).onClick.Invoke();
            Require(harness.Menu.IsLocalConfirmationOpen, "Quit did not require confirmation.");
            Require(harness.Menu.TryHandleEscape() && !harness.Menu.IsLocalConfirmationOpen && harness.Menu.IsOpen,
                "Escape did not cancel Quit while retaining Game Menu.");
        }
        finally
        {
            if (saveManagerBefore == null && SaveManager.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(SaveManager.Instance.gameObject);
            }

            harness.Dispose();
        }
    }

    private static void VerifySaveAndPersistentStateIsolation()
    {
        Require(SaveData.CurrentVersion == 3, "Game Menu changed SaveData.CurrentVersion.");
        string json = JsonUtility.ToJson(new SaveData());
        Require(json.IndexOf("gameMenu", StringComparison.OrdinalIgnoreCase) < 0,
            "Transient Game Menu state leaked into campaign SaveData.");
        Require(typeof(VNGameMenuController).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(field => field.FieldType != typeof(SaveManager) && field.FieldType != typeof(GameState)),
            "Game Menu controller took ownership of SaveManager or GameState.");
        Require(typeof(VNGameMenuController).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .All(field => field.FieldType != typeof(VNGameMenuController)),
            "Game Menu became a singleton/global manager.");
        Require(typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.OpenSave)) != null
            && typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.OpenLoad)) != null,
            "Game Menu cannot route to the existing Save/Load panel.");
        Require(typeof(SceneFlowManager).GetMethod(nameof(SceneFlowManager.EndReplay)) != null
            && typeof(SceneFlowManager).GetMethod(nameof(SceneFlowManager.QuitGame)) != null,
            "Game Menu cannot reuse the existing replay cleanup or quit path.");
    }

    private static Harness CreateHarness(string name)
    {
        GameObject canvasObject = new GameObject(name + " Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject dialogueObject = new GameObject(name + " Dialogue", typeof(RectTransform));
        dialogueObject.transform.SetParent(canvasObject.transform, false);
        VNDialogueController dialogue = dialogueObject.AddComponent<VNDialogueController>();
        typeof(VNDialogueController).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, dialogue);
        dialogue.dialogueUiRoot = new GameObject("Dialogue UI Root");
        dialogue.dialogueUiRoot.transform.SetParent(canvasObject.transform, false);
        SetPrivate(dialogue, "isRuntimeReady", true);

        GameObject quickOwner = new GameObject(name + " Quick Owner", typeof(RectTransform));
        quickOwner.transform.SetParent(canvasObject.transform, false);
        quickOwner.SetActive(false);
        VNQuickMenu quickMenu = quickOwner.AddComponent<VNQuickMenu>();
        GameObject quickRoot = new GameObject("Quick Root", typeof(RectTransform));
        quickRoot.transform.SetParent(quickOwner.transform, false);
        quickMenu.dialogueController = dialogue;
        quickMenu.root = quickRoot;
        quickMenu.historyButton = CreateButton(quickRoot.transform, "History");
        quickMenu.settingsButton = CreateButton(quickRoot.transform, "Settings");
        quickMenu.charactersButton = CreateButton(quickRoot.transform, "Characters");
        quickMenu.mainMenuButton = CreateButton(quickRoot.transform, "Menu");
        quickOwner.SetActive(true);
        quickMenu.RefreshSpecialModeVisibility();

        VNGameMenuController menu = VNGameMenuController.TryCreateRuntime(dialogue);
        Require(menu != null, "Game Menu runtime controller was not created.");
        SetPrivate(dialogue, "gameMenuController", menu);
        return new Harness(canvasObject, dialogue, menu, quickMenu, quickRoot);
    }

    private static void SetupRuntimeCharacterHub(Harness harness)
    {
        CharacterProfileDefinition profile = ScriptableObject.CreateInstance<CharacterProfileDefinition>();
        profile.characterId = "game_menu_test_character";
        profile.displayName = "TEST CHARACTER";
        profile.biography = "TEST BIO";
        CharacterHubFixture[] fixtures = { new CharacterHubFixture { definition = profile } };
        CharacterHubController hub = harness.Dialogue.gameObject.GetComponent<CharacterHubController>()
            ?? harness.Dialogue.gameObject.AddComponent<CharacterHubController>();
        MethodInfo initialize = typeof(CharacterHubController).GetMethod("InitializeRuntime", PrivateInstance);
        Require(initialize != null, "Character Hub runtime initializer was not found.");
        initialize.Invoke(hub, new object[] { harness.Dialogue, harness.Canvas.GetComponent<Canvas>(), fixtures });
        harness.Dialogue.characterHubController = hub;
        harness.OwnedProfile = profile;
    }

    private static void AssertVisibleActions(VNGameMenuView view, VNGameMenuAction[] expected)
    {
        VNGameMenuAction[] actual = Enum.GetValues(typeof(VNGameMenuAction)).Cast<VNGameMenuAction>()
            .Where(view.IsActionVisible)
            .ToArray();
        Require(actual.SequenceEqual(expected),
            "Unexpected Game Menu actions. Expected: " + string.Join(", ", expected) + "; actual: " + string.Join(", ", actual));
    }

    private static void AssertLabel(VNGameMenuView view, VNGameMenuAction action, string expected)
    {
        TextMeshProUGUI label = view.GetButton(action)?.GetComponentInChildren<TextMeshProUGUI>(true);
        Require(label != null && label.text == expected, $"Unexpected label for {action}: '{label?.text}'.");
    }

    private static Button CreateButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        CreateTmp(buttonObject.transform, label).text = label;
        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateTmp(Transform parent, string name)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static T GetPrivate<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, $"Private field '{name}' was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, $"Private field '{name}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Harness : IDisposable
    {
        public Harness(GameObject canvas, VNDialogueController dialogue, VNGameMenuController menu, VNQuickMenu quickMenu, GameObject quickRoot)
        {
            Canvas = canvas;
            Dialogue = dialogue;
            Menu = menu;
            QuickMenu = quickMenu;
            QuickRoot = quickRoot;
        }

        public GameObject Canvas { get; }
        public VNDialogueController Dialogue { get; }
        public VNGameMenuController Menu { get; }
        public VNQuickMenu QuickMenu { get; }
        public GameObject QuickRoot { get; }
        public CharacterProfileDefinition OwnedProfile { get; set; }

        public void Dispose()
        {
            if (OwnedProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(OwnedProfile);
            }

            UnityEngine.Object.DestroyImmediate(Canvas);
        }
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        private GameSettings settings = new GameSettings();
        public PreferencesState Current => new PreferencesState(settings);
        public bool IsAvailable => true;
        public void Reset() => settings = new GameSettings();
        public void SetMasterVolume(float value) => settings.masterVolume = value;
        public void SetMusicVolume(float value) => settings.musicVolume = value;
        public void SetSfxVolume(float value) => settings.sfxVolume = value;
        public void SetMuteAll(bool value) => settings.muteAll = value;
        public void SetScreenMode(string value) => settings.screenMode = value;
        public void SetResolution(string value) => settings.resolution = value;
        public void SetRunInBackground(bool value) => settings.runInBackground = value;
        public void SetSkipMode(string value) => settings.skipMode = value;
        public void SetSkipBehavior(string value) => settings.skipBehavior = value;
        public void SetTextSpeed(float value) => settings.textSpeed = value;
        public void SetDialogueTextScale(float value) => settings.dialogueTextScale = value;
        public void SetTextboxOpacity(float value) => settings.textboxOpacity = value;
        public void SetAutoForwardDelay(float value) => settings.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => settings.skipAfterChoices = value;
        public void SetAutoForward(bool value) => settings.autoForward = value;
        public void SetAutoSave(bool value) => settings.autoSave = value;
    }
}
