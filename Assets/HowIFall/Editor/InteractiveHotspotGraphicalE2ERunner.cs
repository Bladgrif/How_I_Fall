using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Automated graphical proof for the Interactive Hotspot readiness gate plus the
/// polished TECH showcase: authored background display, deterministic keyboard
/// focus ownership, locked-hotspot submit safety, Game Menu round-trip with
/// disabled Save/Load, completion routing, clean re-entry, and the showcase
/// flow (Ноутбук + Записки unlock Дверь; Menu button; return to Reading).
/// TECH DEMO ONLY / NOT CANON fixtures only.
/// </summary>
[InitializeOnLoad]
public static class InteractiveHotspotGraphicalE2ERunner
{
    private const string ActiveKey = "HowIFall.HotspotE2E.Active";
    private const string StageKey = "HowIFall.HotspotE2E.Stage";
    private const string NextTimeKey = "HowIFall.HotspotE2E.NextTime";
    private const string CounterKey = "HowIFall.HotspotE2E.Counter";
    private const string NextStageKey = "HowIFall.HotspotE2E.NextStage";
    private const string CapturePathKey = "HowIFall.HotspotE2E.CapturePath";
    private const string CaptureWidthKey = "HowIFall.HotspotE2E.CaptureWidth";
    private const string CaptureHeightKey = "HowIFall.HotspotE2E.CaptureHeight";
    private const string RunStartedKey = "HowIFall.HotspotE2E.RunStartedUtc";
    private const string ErrorsKey = "HowIFall.HotspotE2E.Errors";
    private const string AutoSaveSnapshotKey = "HowIFall.HotspotE2E.AutoSaveSnapshot";
    private const string ResultPath = "hotspot_graphical_result.txt";
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private static readonly Vector2Int QaResolution = new Vector2Int(1920, 1080);
    private static readonly Vector2Int ResponsiveQaResolution = new Vector2Int(1280, 720);

    private static Sprite authoredBackgroundSprite;
    private static InteractiveSceneData authoredRoomClone;

    static InteractiveHotspotGraphicalE2ERunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    [MenuItem("How I Fall/Tests/Run Interactive Hotspot Graphical E2E")]
    public static void StartAutomatedPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Play Mode is already active.");
        }

        string root = Directory.GetCurrentDirectory();
        string proofDirectory = Path.Combine(root, "QAArtifacts", "GraphicalE2E", "Hotspot");
        if (Directory.Exists(proofDirectory)) Directory.Delete(proofDirectory, true);
        string resultPath = Path.Combine(root, ResultPath);
        if (File.Exists(resultPath)) File.Delete(resultPath);

        // VNPrototype can autosave during startup; disable it for the proof and
        // restore the original value before the editor exits.
        SessionState.SetBool(AutoSaveSnapshotKey, PlayerPrefs.HasKey("hif_auto_save"));
        SessionState.SetInt(AutoSaveSnapshotKey + ".Value", PlayerPrefs.GetInt("hif_auto_save", 1));
        PlayerPrefs.SetInt("hif_auto_save", 0);
        PlayerPrefs.Save();

        InteractiveHotspotTechnicalContentBuilder.Build();
        authoredRoomClone = null;
        authoredBackgroundSprite = null;

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "WaitRuntime");
        SessionState.SetString(NextStageKey, string.Empty);
        SessionState.SetString(CapturePathKey, string.Empty);
        SessionState.SetString(RunStartedKey, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        SessionState.SetString(ErrorsKey, string.Empty);
        SessionState.SetInt(CounterKey, 0);
        SetDelay(1d);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[HOTSPOT E2E] START: entering Play Mode from VNPrototype.");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || !EditorApplication.isPlaying
            || EditorApplication.timeSinceStartup < SessionState.GetFloat(NextTimeKey, 0f)) return;

        try
        {
            switch (SessionState.GetString(StageKey, string.Empty))
            {
                case "WaitRuntime": WaitRuntime(); break;
                case "CaptureInitial": CaptureInitial(); break;
                case "MoveFocusToLocked": MoveFocusToLocked(); break;
                case "MoveFocusToWindow": MoveFocusToWindow(); break;
                case "FocusLaptop": FocusLaptop(); break;
                case "SubmitLaptop": SubmitLaptop(); break;
                case "OpenGameMenu": OpenGameMenu(); break;
                case "CloseGameMenu": CloseGameMenu(); break;
                case "ReturnFromGameMenu": ReturnFromGameMenu(); break;
                case "SubmitDoor": SubmitDoor(); break;
                case "ReenterHotspot": ReenterHotspot(); break;
                case "ReentryResolution": ReentryResolution(); break;
                case "CloseReentryRoom": CloseReentryRoom(); break;
                case "ShowcaseResolution": ShowcaseResolution(); break;
                case "StartShowcase": StartShowcase(); break;
                case "ShowcaseFocusNotes": ShowcaseFocusNotes(); break;
                case "ShowcaseFocusDoorLocked": ShowcaseFocusDoorLocked(); break;
                case "ShowcaseSubmitLaptop": ShowcaseSubmitLaptop(); break;
                case "ShowcaseSubmitNotes": ShowcaseSubmitNotes(); break;
                case "ShowcaseGameMenu": ShowcaseGameMenuStage(); break;
                case "ShowcaseMenuWait": ShowcaseMenuWait(); break;
                case "ShowcaseMenuReturn": ShowcaseMenuReturn(); break;
                case "ShowcaseCompleteDoor": ShowcaseCompleteDoor(); break;
                case "ShowcaseReentry": ShowcaseReentry(); break;
                case "ShowcaseReentryResolution": ShowcaseReentryResolution(); break;
                case "WaitScreenshot": WaitScreenshot(); break;
            }
        }
        catch (Exception exception)
        {
            Fail("stage=" + SessionState.GetString(StageKey, string.Empty) + "\n" + exception);
        }
    }

    private static void WaitRuntime()
    {
        if (SceneManager.GetActiveScene().name != "VNPrototype") { Retry("VNPrototype scene did not become active."); return; }
        VNDialogueController dialogue = VNDialogueController.Instance;
        if (dialogue == null || !dialogue.IsRuntimeReady) { Retry("VN runtime is not ready."); return; }
        ConfigureGameViewResolution(QaResolution);
        if (Screen.width != QaResolution.x || Screen.height != QaResolution.y) { Retry("Game View did not switch to 1920x1080."); return; }
        Require(EventSystem.current != null, "The VNPrototype scene must provide an active EventSystem.");

        InteractiveSceneData room = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveHotspotTechnicalContentBuilder.InteractiveScenePath);
        Require(room != null, "TECH Interactive Room asset is missing.");
        Require(room.background == null, "The committed TECH room fixture must keep exercising the fallback path.");
        authoredRoomClone = UnityEngine.Object.Instantiate(room);
        authoredRoomClone.hideFlags = HideFlags.HideAndDontSave;
        authoredRoomClone.background = CreateAuthoredBackgroundSprite();
        Require(dialogue.TryStartInteractiveScene(authoredRoomClone, out string failure), "Hotspot did not start: " + failure);
        Require(dialogue.ActiveInteractiveSceneController != null && dialogue.ActiveInteractiveSceneController.IsRunning, "Interactive scene is not running after start.");
        SessionState.SetString(StageKey, "CaptureInitial");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void CaptureInitial()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
        Require(displayed != null && displayed.sprite == authoredBackgroundSprite,
            "The authored non-null scene.background must be displayed instead of the technical fallback.");
        Require(displayed.sprite.name == "TECH_AuthoredHotspotBackground", "Unexpected displayed sprite: " + displayed.sprite.name);
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("test_laptop").gameObject,
            "Hotspot entry must own a deterministic usable focus on the first available hotspot.");
        Capture("hotspot_initial_authored_background_1920x1080.png", "MoveFocusToLocked");
    }

    private static void MoveFocusToLocked()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Button laptop = interactive.GetHotspotButton("test_laptop");
        Button door = interactive.GetHotspotButton("test_door");
        Require(door != null && !door.interactable, "Door must start locked behind the laptop prerequisite.");
        ExecuteEvents.Execute<IMoveHandler>(laptop.gameObject,
            new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
        Require(EventSystem.current.currentSelectedGameObject == door.gameObject,
            "Navigation must reach the locked hotspot instead of hidden scene UI.");
        Capture("hotspot_locked_door_focus_1920x1080.png", "MoveFocusToWindow");
    }

    private static void MoveFocusToWindow()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Button door = interactive.GetHotspotButton("test_door");
        Button window = interactive.GetHotspotButton("test_window");
        Require(window != null && window.interactable, "Window must start available.");
        ExecuteEvents.Execute<IMoveHandler>(door.gameObject,
            new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
        Require(EventSystem.current.currentSelectedGameObject == window.gameObject,
            "Navigation must reach the available window hotspot.");
        Capture("hotspot_keyboard_focus_moved_1920x1080.png", "FocusLaptop");
    }

    private static void FocusLaptop()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Button laptop = interactive.GetHotspotButton("test_laptop");
        Button door = interactive.GetHotspotButton("test_door");
        ExecuteEvents.Execute<IMoveHandler>(interactive.GetHotspotButton("test_window").gameObject,
            new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left, moveVector = Vector2.left }, ExecuteEvents.moveHandler);
        Require(EventSystem.current.currentSelectedGameObject == door.gameObject, "Left navigation must return to the door hotspot.");
        ExecuteEvents.Execute<IMoveHandler>(door.gameObject,
            new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left, moveVector = Vector2.left }, ExecuteEvents.moveHandler);
        Require(EventSystem.current.currentSelectedGameObject == laptop.gameObject, "Left navigation must return to the laptop hotspot.");
        SessionState.SetString(StageKey, "SubmitLaptop");
        ResetCounter();
        SetDelay(0.3d);
    }

    private static void SubmitLaptop()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        GameState state = GameState.Instance;
        int suspicionBefore = state.suspicion;
        Submit(interactive.GetHotspotButton("test_laptop"));
        Require(interactive.IsHotspotCompleted("test_laptop"), "Submit must activate the selected available hotspot.");
        Require(interactive.ActivationCount == 1, "Submit must activate exactly once.");
        Require(state.suspicion == suspicionBefore, "Local hotspot completion must not touch canonical GameState.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("test_door").gameObject,
            "A completed one-shot must hand focus to the next valid hotspot.");
        Require(interactive.IsHotspotAvailable("test_door"), "Door must unlock after the laptop prerequisite action.");
        Capture("hotspot_after_unlock_1920x1080.png", "OpenGameMenu");
    }

    private static void OpenGameMenu()
    {
        RequireRunningScene();
        Require(VNDialogueController.Instance.OpenGameMenu(), "Interactive mode must permit the existing Game Menu round-trip.");
        SessionState.SetString(StageKey, "CloseGameMenu");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void CloseGameMenu()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue.IsGameMenuOpen, "Game Menu did not open over the active Hotspot.");
        Require(!dialogue.CanSave && !dialogue.CanLoad, "Save/Load must stay blocked while the Hotspot is active.");
        Button save = dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save);
        Button load = dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load);
        Require(!save.interactable && !load.interactable, "Game Menu Save/Load rows must be visibly disabled during the Hotspot.");
        Capture("hotspot_game_menu_save_load_disabled_1920x1080.png", "ReturnFromGameMenu");
    }

    private static void ReturnFromGameMenu()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue.GameMenuController.Close(), "Game Menu close must return to the active room.");
        InteractiveSceneController interactive = RequireRunningScene();
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("test_door").gameObject,
            "Game Menu return must restore a usable Hotspot focus owner without a mouse click.");
        Capture("hotspot_after_menu_return_1920x1080.png", "SubmitDoor");
    }

    private static void SubmitDoor()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneController interactive = RequireRunningScene();
        Submit(interactive.GetHotspotButton("test_door"));
        Require(!interactive.IsRunning, "Door completion must close the interactive runtime.");
        Require(!interactive.IsRuntimeUiActive, "Hotspot runtime root must be inactive after completion.");
        Require(dialogue.CanAdvanceDialogue, "Completion must restore normal dialogue eligibility.");
        Require(!dialogue.IsDialogueShellSuppressed, "Completion must release the suppressed dialogue shell.");
        Require(GameState.Instance.currentSceneId == "interactive_hotspot_complete", "Completion must route to the registered completion scene.");
        GameObject stale = EventSystem.current.currentSelectedGameObject;
        Require(stale == null || (stale != interactive.GetHotspotButton("test_laptop").gameObject
            && stale != interactive.GetHotspotButton("test_door").gameObject
            && stale != interactive.GetHotspotButton("test_window").gameObject),
            "Stale EventSystem selection must not keep pointing at hidden Hotspot controls.");
        Capture("hotspot_completion_reading_restored_1920x1080.png", "ReenterHotspot");
    }

    private static void ReenterHotspot()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneData room = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveHotspotTechnicalContentBuilder.InteractiveScenePath);
        Require(room != null, "TECH Interactive Room asset disappeared before re-entry.");
        Require(dialogue.TryStartInteractiveScene(room, out string failure), "Hotspot re-entry failed: " + failure);
        InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
        Require(interactive.IsRunning && interactive.ActivationCount == 0, "Repeated entry must start a clean run.");
        Require(interactive.IsHotspotAvailable("test_laptop"), "One-shot hotspots must reset for the new run.");
        Require(interactive.IsHotspotAvailable("test_door") == false, "Local prerequisites must reset for the new run.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("test_laptop").gameObject,
            "Repeated entry must restore the initial usable focus owner.");
        Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
        Require(displayed.sprite.name == "InteractiveHotspotTechnicalRoom", "Null-background re-entry must keep the technical fallback.");
        SessionState.SetString(StageKey, "ReentryResolution");
        ResetCounter();
        SetDelay(0.3d);
    }

    private static void ReentryResolution()
    {
        RequireRunningScene();
        ConfigureGameViewResolution(ResponsiveQaResolution);
        if (Screen.width != ResponsiveQaResolution.x || Screen.height != ResponsiveQaResolution.y)
        {
            Retry("Game View did not switch to 1280x720 for the re-entry proof.");
            return;
        }
        Capture("hotspot_reentry_fallback_background_1280x720.png", "CloseReentryRoom");
    }

    private static void CloseReentryRoom()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Submit(interactive.GetHotspotButton("test_laptop"));
        Submit(interactive.GetHotspotButton("test_door"));
        Require(!interactive.IsRunning, "The re-entry room must close before the showcase phase.");
        SessionState.SetString(StageKey, "ShowcaseResolution");
        ResetCounter();
        SetDelay(0.3d);
    }

    private static void ShowcaseResolution()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue != null && dialogue.IsRuntimeReady, "VN runtime is unavailable before the showcase phase.");
        ConfigureGameViewResolution(QaResolution);
        if (Screen.width != QaResolution.x || Screen.height != QaResolution.y)
        {
            Retry("Game View did not switch back to 1920x1080 for the showcase proof.");
            return;
        }
        SessionState.SetString(StageKey, "StartShowcase");
        ResetCounter();
        SetDelay(0.2d);
    }

    private static void StartShowcase()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneData showcase = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveHotspotTechnicalContentBuilder.ShowcaseScenePath);
        Require(showcase != null, "Hotspot showcase asset is missing.");
        Require(showcase.background != null, "The showcase asset must carry the approved background sprite.");
        Require(dialogue.TryStartInteractiveScene(showcase, out string failure), "Showcase did not start: " + failure);
        InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
        Require(interactive.IsRunning, "Showcase is not running after start.");
        Require(interactive.MenuButton != null && interactive.MenuButton.interactable, "The Hotspot Menu button must be present and usable.");
        Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
        Require(displayed != null && displayed.sprite == showcase.background, "The showcase must display the approved committed background sprite.");
        Require(!interactive.GetHotspotButton("showcase_door").interactable, "The Дверь must start locked in the showcase.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_laptop").gameObject,
            "Showcase entry must give the initial focus to the first available hotspot (Ноутбук).");
        Capture("hotspot_showcase_initial_1920x1080.png", "ShowcaseFocusNotes");
    }

    private static void ShowcaseFocusNotes()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Move(interactive.GetHotspotButton("showcase_laptop"), MoveDirection.Right);
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_notes").gameObject,
            "Keyboard navigation must reach the Записки marker.");
        Capture("hotspot_showcase_notes_focus_1920x1080.png", "ShowcaseFocusDoorLocked");
    }

    private static void ShowcaseFocusDoorLocked()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Button door = interactive.GetHotspotButton("showcase_door");
        Require(door != null && !door.interactable, "The Дверь must be locked before both prerequisites.");
        Move(interactive.GetHotspotButton("showcase_notes"), MoveDirection.Right);
        Require(EventSystem.current.currentSelectedGameObject == door.gameObject, "Keyboard navigation must reach the locked Дверь marker.");
        Capture("hotspot_showcase_door_locked_focus_1920x1080.png", "ShowcaseSubmitLaptop");
    }

    private static void ShowcaseSubmitLaptop()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Move(interactive.GetHotspotButton("showcase_door"), MoveDirection.Left);
        Move(interactive.GetHotspotButton("showcase_notes"), MoveDirection.Left);
        Button laptop = interactive.GetHotspotButton("showcase_laptop");
        Require(EventSystem.current.currentSelectedGameObject == laptop.gameObject, "Keyboard navigation must return to Ноутбук.");
        Submit(laptop);
        Require(interactive.IsHotspotCompleted("showcase_laptop"), "Ноутбук must complete as a one-shot.");
        Require(!interactive.IsHotspotAvailable("showcase_door"), "Laptop alone must not unlock the Дверь.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_notes").gameObject,
            "A completed one-shot must hand focus to the next authored hotspot.");
        Capture("hotspot_showcase_after_laptop_1920x1080.png", "ShowcaseSubmitNotes");
    }

    private static void ShowcaseSubmitNotes()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Submit(interactive.GetHotspotButton("showcase_notes"));
        Require(interactive.IsHotspotCompleted("showcase_notes"), "Записки must complete as a one-shot.");
        Require(interactive.IsHotspotAvailable("showcase_door"), "Laptop + Notes must unlock the Дверь.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_door").gameObject,
            "Focus must move to the unlocked Дверь.");
        Capture("hotspot_showcase_door_unlocked_1920x1080.png", "ShowcaseGameMenu");
    }

    private static void ShowcaseGameMenuStage()
    {
        InteractiveSceneController interactive = RequireRunningScene();
        Button menu = interactive.MenuButton;
        Require(menu != null && menu.interactable, "The Hotspot Menu button must be usable.");
        ExecuteEvents.Execute<ISubmitHandler>(menu.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        SessionState.SetString(StageKey, "ShowcaseMenuWait");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void ShowcaseMenuWait()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue.IsGameMenuOpen, "The Hotspot Menu button must open the existing Game Menu.");
        Require(!dialogue.CanSave && !dialogue.CanLoad, "Save/Load must stay blocked while the showcase is active.");
        Button save = dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save);
        Button load = dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load);
        Require(!save.interactable && !load.interactable, "Game Menu Save/Load rows must be visibly disabled during the showcase.");
        Capture("hotspot_showcase_game_menu_1920x1080.png", "ShowcaseMenuReturn");
    }

    private static void ShowcaseMenuReturn()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue.GameMenuController.Close(), "Game Menu close must return to the active showcase.");
        InteractiveSceneController interactive = RequireRunningScene();
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_door").gameObject,
            "Game Menu return must restore the unlocked Дверь focus owner.");
        Capture("hotspot_showcase_menu_return_1920x1080.png", "ShowcaseCompleteDoor");
    }

    private static void ShowcaseCompleteDoor()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneController interactive = RequireRunningScene();
        Submit(interactive.GetHotspotButton("showcase_door"));
        Require(!interactive.IsRunning, "Door completion must close the showcase runtime.");
        Require(!interactive.IsRuntimeUiActive, "Showcase runtime root must be inactive after completion.");
        Require(dialogue.CanAdvanceDialogue, "Completion must restore normal dialogue eligibility.");
        Require(!dialogue.IsDialogueShellSuppressed, "Completion must release the suppressed dialogue shell.");
        Require(GameState.Instance.currentSceneId == "interactive_hotspot_complete", "Completion must route to the registered completion scene.");
        Capture("hotspot_showcase_completion_reading_1920x1080.png", "ShowcaseReentry");
    }

    private static void ShowcaseReentry()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneData showcase = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveHotspotTechnicalContentBuilder.ShowcaseScenePath);
        Require(showcase != null, "Hotspot showcase asset disappeared before showcase re-entry.");
        Require(dialogue.TryStartInteractiveScene(showcase, out string failure), "Showcase re-entry failed: " + failure);
        InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
        Require(interactive.IsRunning && interactive.ActivationCount == 0, "Showcase re-entry must start a clean run.");
        Require(EventSystem.current.currentSelectedGameObject == interactive.GetHotspotButton("showcase_laptop").gameObject,
            "Showcase re-entry must restore the initial focus owner.");
        SessionState.SetString(StageKey, "ShowcaseReentryResolution");
        ResetCounter();
        SetDelay(0.3d);
    }

    private static void ShowcaseReentryResolution()
    {
        RequireRunningScene();
        ConfigureGameViewResolution(ResponsiveQaResolution);
        if (Screen.width != ResponsiveQaResolution.x || Screen.height != ResponsiveQaResolution.y)
        {
            Retry("Game View did not switch to 1280x720 for the showcase re-entry proof.");
            return;
        }
        Capture("hotspot_showcase_1280x720.png", "Complete");
    }

    private static void Move(Button button, MoveDirection direction)
    {
        ExecuteEvents.Execute<IMoveHandler>(button.gameObject,
            new AxisEventData(EventSystem.current) { moveDir = direction, moveVector = direction == MoveDirection.Left ? Vector2.left : Vector2.right },
            ExecuteEvents.moveHandler);
    }

    private static void Complete()
    {
        SessionState.SetString(NextStageKey, string.Empty);
        Success();
    }

    private static InteractiveSceneController RequireRunningScene()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue != null && dialogue.IsRuntimeReady, "VN runtime is unavailable.");
        InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
        Require(interactive != null && interactive.IsRunning && interactive.IsRuntimeUiActive, "The interactive scene is not running.");
        return interactive;
    }

    private static void Submit(Button button)
    {
        Require(button != null && button.interactable, "Expected an interactable hotspot for Submit.");
        ExecuteEvents.Execute<ISubmitHandler>(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    private static Sprite CreateAuthoredBackgroundSprite()
    {
        // Clearly distinguishable from the dark technical fallback: teal→amber
        // gradient with a white top band. TECH DEMO ONLY / NOT CANON.
        const int width = 64, height = 36;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "TECH_AuthoredHotspotBackground" };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y >= height - 4)
                {
                    pixels[y * width + x] = new Color(0.95f, 0.97f, 1f, 1f);
                    continue;
                }
                float t = x / (width - 1f);
                pixels[y * width + x] = Color.Lerp(new Color(0.10f, 0.62f, 0.55f, 1f), new Color(0.72f, 0.45f, 0.15f, 1f), t);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        authoredBackgroundSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 36f);
        authoredBackgroundSprite.name = texture.name;
        return authoredBackgroundSprite;
    }

    private static void Capture(string fileName, string nextStage)
    {
        Require(Screen.width > 0 && Screen.height > 0, "Capture requires a valid Game View size.");
        string path = Path.Combine(Directory.GetCurrentDirectory(), "QAArtifacts", "GraphicalE2E", "Hotspot", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        SessionState.SetString(CapturePathKey, path);
        Vector2Int captureTarget = Screen.width >= QaResolution.x ? QaResolution : ResponsiveQaResolution;
        SessionState.SetInt(CaptureWidthKey, captureTarget.x);
        SessionState.SetInt(CaptureHeightKey, captureTarget.y);
        SessionState.SetString(NextStageKey, nextStage);
        SessionState.SetString(StageKey, "WaitScreenshot");
        ResetCounter();
        SetDelay(0.25d);
    }

    private static void WaitScreenshot()
    {
        string path = SessionState.GetString(CapturePathKey, string.Empty);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) { Retry("Queued screenshot was not written: " + path); return; }
        DateTime runStarted = DateTime.Parse(SessionState.GetString(RunStartedKey, string.Empty), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Require(File.GetLastWriteTimeUtc(path) >= runStarted, "Screenshot predates this run: " + path);
        VerifyImageDimensions(path, SessionState.GetInt(CaptureWidthKey, QaResolution.x), SessionState.GetInt(CaptureHeightKey, QaResolution.y));
        string next = SessionState.GetString(NextStageKey, string.Empty);
        if (next == "Complete") Complete();
        else
        {
            SessionState.SetString(StageKey, next);
            ResetCounter();
            SetDelay(0.35d);
        }
    }

    private static void VerifyImageDimensions(string path, int width, int height)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            Require(texture.LoadImage(File.ReadAllBytes(path), true), "Screenshot is not a readable PNG: " + path);
            Require(texture.width == width && texture.height == height, $"Screenshot size is {texture.width}x{texture.height}, expected {width}x{height}.");
        }
        finally { UnityEngine.Object.Destroy(texture); }
    }

    private static void ConfigureGameViewResolution(Vector2Int resolution)
    {
        try
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type gameViewType = assembly.GetType("UnityEditor.GameView", true);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType", true), "Standalone") });
            MethodInfo countMethod = group.GetType().GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public);
            int count = (int)countMethod.Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                int width = (int)sizeType.GetProperty("width").GetValue(size, null);
                int height = (int)sizeType.GetProperty("height").GetValue(size, null);
                if (width == resolution.x && height == resolution.y) { index = i; break; }
            }
            if (index < 0)
            {
                object size = Activator.CreateInstance(sizeType, Enum.Parse(sizeKindType, "FixedResolution"), resolution.x, resolution.y, "How I Fall QA Hotspot " + resolution.x + "x" + resolution.y);
                group.GetType().GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public).Invoke(group, new[] { size });
                index = count;
            }
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gameView, index, null);
            gameView.Repaint();
            Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
        }
        catch (Exception exception) { throw new InvalidOperationException("Could not configure Game View resolution.", exception); }
    }

    private static void Retry(string message)
    {
        int attempts = SessionState.GetInt(CounterKey, 0) + 1;
        SessionState.SetInt(CounterKey, attempts);
        Require(attempts < 80, message + " Timed out after 80 attempts.");
        SetDelay(0.15d);
    }

    private static void ResetCounter() => SessionState.SetInt(CounterKey, 0);
    private static void SetDelay(double seconds) => SessionState.SetFloat(NextTimeKey, (float)(EditorApplication.timeSinceStartup + seconds));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            || condition.StartsWith("[HOTSPOT E2E] FAILURE", StringComparison.Ordinal)) return;
        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        if (errors.Length < 12000) SessionState.SetString(ErrorsKey, errors + condition + "\n");
    }

    private static bool RestoreAutoSavePreference()
    {
        if (SessionState.GetBool(AutoSaveSnapshotKey, false))
        {
            PlayerPrefs.SetInt("hif_auto_save", SessionState.GetInt(AutoSaveSnapshotKey + ".Value", 1));
        }
        else
        {
            PlayerPrefs.DeleteKey("hif_auto_save");
        }
        PlayerPrefs.Save();
        bool restored = PlayerPrefs.HasKey("hif_auto_save") == SessionState.GetBool(AutoSaveSnapshotKey, false);
        if (SessionState.GetBool(AutoSaveSnapshotKey, false))
        {
            restored &= PlayerPrefs.GetInt("hif_auto_save", -1) == SessionState.GetInt(AutoSaveSnapshotKey + ".Value", 1);
        }
        return restored;
    }

    private static void WriteResult(string status, string details)
    {
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ResultPath),
            $"status={status}\n" + $"timeUtc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}\n" + $"details={details}\n");
    }

    private static void CleanupFixtures()
    {
        if (authoredRoomClone != null) { UnityEngine.Object.Destroy(authoredRoomClone); authoredRoomClone = null; }
        if (authoredBackgroundSprite != null) { UnityEngine.Object.Destroy(authoredBackgroundSprite); authoredBackgroundSprite = null; }
    }

    private static void Success()
    {
        Require(string.IsNullOrEmpty(SessionState.GetString(ErrorsKey, string.Empty)), "Unity Console contained errors:\n" + SessionState.GetString(ErrorsKey, string.Empty));
        Require(RestoreAutoSavePreference(), "The hif_auto_save preference could not be restored.");
        WriteResult("PASS", "hotspot readiness proof captured at 1920x1080 and 1280x720; playerPrefsRestored=true");
        CleanupFixtures();
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string details)
    {
        bool restored = RestoreAutoSavePreference();
        WriteResult("FAIL", details + "\nplayerPrefsRestored=" + restored.ToString().ToLowerInvariant());
        CleanupFixtures();
        Debug.LogError("[HOTSPOT E2E] FAILURE: " + details);
        SessionState.SetString(StageKey, "ExitFailure");
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else { SessionState.SetBool(ActiveKey, false); EditorApplication.Exit(1); }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            RestoreAutoSavePreference();
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        SessionState.SetBool(ActiveKey, false);
        if (stage == "ExitSuccess") EditorApplication.Exit(0);
        if (stage == "ExitFailure") EditorApplication.Exit(1);
    }
}
