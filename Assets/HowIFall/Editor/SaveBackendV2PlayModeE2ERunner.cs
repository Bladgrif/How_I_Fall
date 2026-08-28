using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SaveBackendV2PlayModeE2ERunner
{
    private static readonly Color ActiveTabOutlineColor = new Color(0.28f, 0.54f, 0.76f, 0.62f);
    private static readonly Color InactiveTabOutlineColor = new Color(0.16f, 0.25f, 0.34f, 0.34f);

    private const string ActiveKey = "HowIFall.SaveBackendV2E2E.Active";
    private const string StageKey = "HowIFall.SaveBackendV2E2E.Stage";
    private const string NextTimeKey = "HowIFall.SaveBackendV2E2E.NextTime";
    private const string CounterKey = "HowIFall.SaveBackendV2E2E.Counter";
    private const string ErrorsKey = "HowIFall.SaveBackendV2E2E.Errors";
    private const string DirectoryKey = "HowIFall.SaveBackendV2E2E.Directory";
    private const string AutoSnapshotKey = "HowIFall.SaveBackendV2E2E.AutoSnapshot";
    private const string QuickSnapshotKey = "HowIFall.SaveBackendV2E2E.QuickSnapshot";
    private const string QuickSlotIndexKey = "HowIFall.SaveBackendV2E2E.QuickSlotIndex";
    private const string AutoNewestSnapshotKey = "HowIFall.SaveBackendV2E2E.AutoNewestSnapshot";
    private const string AutoFilesSignatureKey = "HowIFall.SaveBackendV2E2E.AutoFilesSignature";
    private const string ResultPath = "save_backend_v2_playmode_result.txt";
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private static readonly Vector2Int TabsUiResolution = new Vector2Int(1920, 1080);

    static SaveBackendV2PlayModeE2ERunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    [MenuItem("How I Fall/Tests/Run Save Backend v3 Play Mode E2E")]
    public static void StartAutomatedPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Play Mode is already active.");
        }

        DeleteIfExists(Path.Combine(Directory.GetCurrentDirectory(), ResultPath));
        CleanupTestDirectory();
        string directory = Path.Combine(
            Path.GetTempPath(),
            "HowIFall_SaveBackendV2E2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        ManualSaveSystemSceneInstaller.ValidateInstalledScenes();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "WaitMainStart");
        SessionState.SetString(ErrorsKey, string.Empty);
        SessionState.SetString(DirectoryKey, directory);
        SessionState.SetString(AutoSnapshotKey, string.Empty);
        SessionState.SetString(QuickSnapshotKey, string.Empty);
        SessionState.SetInt(QuickSlotIndexKey, 0);
        SessionState.SetString(AutoNewestSnapshotKey, string.Empty);
        SessionState.SetString(AutoFilesSignatureKey, string.Empty);
        SessionState.SetInt(CounterKey, 0);
        SetDelay(1d);

        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        Debug.Log($"[SAVE BACKEND E2E] START: temporary directory='{directory}'.");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || !EditorApplication.isPlaying
            || EditorApplication.timeSinceStartup < SessionState.GetFloat(NextTimeKey, 0f))
        {
            return;
        }

        try
        {
            switch (SessionState.GetString(StageKey, string.Empty))
            {
                case "WaitMainStart":
                    WaitMainStart();
                    break;
                case "WaitVnStart":
                    WaitVnStart();
                    break;
                case "WaitCoreScenario":
                case "WaitAutoNewestCoroutine":
                    SetDelay(0.25d);
                    break;
                case "WaitMainDirectQuickLoad":
                    WaitMainDirectQuickLoad();
                    break;
                case "WaitVnDirectQuickLoad":
                    WaitVnDirectQuickLoad();
                    break;
                case "WaitMainQuickContinue":
                    WaitMainQuickContinue();
                    break;
                case "WaitVnQuickContinue":
                    WaitVnQuickContinue();
                    break;
                case "WaitMainAutoContinue":
                    WaitMainAutoContinue();
                    break;
                case "WaitVnAutoContinue":
                    WaitVnAutoContinue();
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static void WaitMainStart()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        SaveManager manager = SaveManager.Instance;
        if (menu == null || manager == null)
        {
            Retry("MainMenu SaveManager was not initialized.");
            return;
        }

        string directory = SessionState.GetString(DirectoryKey, string.Empty);
        manager.ConfigureSaveDirectoryForTests(directory);
        Require(manager.SaveDirectoryPath == directory, "SaveManager did not switch to the temporary test directory.");
        Require(AllTypesAreEmpty(manager), "Temporary save directory is not empty at test start.");
        Require(UnityEngine.Object.FindObjectsByType<SaveManager>().Length == 1, "More than one SaveManager exists in MainMenu.");

        SessionState.SetString(StageKey, "WaitVnStart");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.75d);
        menu.StartGame();
    }

    private static void WaitVnStart()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        if (SceneManager.GetActiveScene().name != SaveManager.GameplaySceneName
            || controller == null
            || GameState.Instance == null
            || !controller.TryGetSavePosition(out _, out _, out _, out _))
        {
            Retry("VNPrototype did not reach a valid save position.");
            return;
        }

        Require(SaveManager.Instance != null, "SaveManager is missing in VNPrototype.");
        Require(UnityEngine.Object.FindObjectsByType<SaveManager>().Length == 1, "More than one SaveManager exists in VNPrototype.");
        Require(controller.sceneRegistry != null, "DialogueSceneRegistry is missing in VNPrototype.");

        SaveSlotInfo newGameAuto = SaveManager.Instance.GetSlot(SaveSlotType.Auto, 1);
        if (!newGameAuto.IsLoadable || string.IsNullOrEmpty(newGameAuto.PreviewPath))
        {
            Retry("New Game did not create Auto slot 1 with JSON and PNG.");
            return;
        }

        SessionState.SetString(StageKey, "WaitCoreScenario");
        SessionState.SetInt(CounterKey, 0);
        controller.StartCoroutine(RunSafely(RunCoreScenario(controller)));
    }

    private static IEnumerator RunCoreScenario(VNDialogueController controller)
    {
        Texture2D screenshot = null;
        try
        {
            SaveManager manager = SaveManager.Instance;
            Require(manager != null, "SaveManager disappeared before quick-save command tests.");
            VerifyNewGameAutoSave(controller, manager);
            yield return RunSafely(VerifyQuickSaveCommandBeforeChoice(controller, manager));

            yield return RunSafely(AdvanceToChoice(controller));
            yield return RunSafely(VerifyChoiceAutoSave(controller, manager));
            yield return RunSafely(VerifyQuickSaveCommandOnChoice(controller, manager));
            yield return RunSafely(VerifyNoAutoSaveOnRestore(controller, manager));
            CleanupQuickCommandSlots(manager);
            CleanupSlots(manager, SaveSlotType.Manual);
            CleanupSlots(manager, SaveSlotType.Auto);
            yield return new WaitForSecondsRealtime(controller.notificationDuration + 0.1f);

            Require(controller.choiceMashaButton != null, "The first real VN choice button is missing.");
            controller.choiceMashaButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.15f);

            GameState state = GameState.Instance;
            Require(state != null && state.choiceResultActive && state.selectedChoiceIndex == 0, "The real VN choice was not stored before backend saves.");

            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Require(screenshot != null && screenshot.width > 0 && screenshot.height > 0, "ScreenCapture returned no real VN screenshot.");

            Require(manager.SaveSlot(1, screenshot), "Public Manual SaveSlot failed while preparing isolation and Continue data.");
            VerifySlot(manager, SaveSlotType.Manual, 1);
            Dictionary<string, byte[]> manualFiles = CaptureTypeFiles(manager, SaveSlotType.Manual);

            state.suspicion = 101;
            state.trustMasha = 201;
            SaveData autoSnapshot = null;
            string autoSlotOneBeforeSeventh = string.Empty;
            DateTime previousAutoTime = DateTime.MinValue;
            for (int call = 1; call <= 7; call++)
            {
                Require(manager.SaveAuto(screenshot), $"Public SaveAuto failed on call {call}.");
                int expectedSlot = call <= SaveManager.SlotCount ? call : 1;
                SaveSlotInfo slot = VerifySlot(manager, SaveSlotType.Auto, expectedSlot);
                Require(slot.CreatedAtUtc > previousAutoTime, $"SaveAuto call {call} did not produce a distinguishable createdAtUtc.");
                previousAutoTime = slot.CreatedAtUtc;

                if (call == 1)
                {
                    autoSlotOneBeforeSeventh = slot.Data.createdAtUtc;
                }
                else if (call == 7)
                {
                    Require(slot.Data.createdAtUtc != autoSlotOneBeforeSeventh, "The seventh SaveAuto did not overwrite the oldest Auto slot 1.");
                    autoSnapshot = Clone(slot.Data);
                }

                yield return new WaitForSecondsRealtime(0.08f);
            }

            Require(manager.GetAllSlots(SaveSlotType.Auto).Count(slot => slot.IsLoadable) == SaveManager.SlotCount, "Seven SaveAuto calls did not leave six loadable Auto slots.");
            Require(manager.GetAllSlots(SaveSlotType.Quick).All(slot => !slot.IsOccupied), "SaveAuto changed Quick slots.");
            RequireFilesEqual(manualFiles, CaptureTypeFiles(manager, SaveSlotType.Manual), "SaveAuto changed Manual files.");
            Require(autoSnapshot != null, "Auto snapshot was not captured after seven real SaveAuto calls.");
            SessionState.SetString(AutoSnapshotKey, JsonUtility.ToJson(autoSnapshot));
            Pass("SaveAuto created slots 1-6 and the seventh call overwrote oldest slot 1");

            Dictionary<string, byte[]> autoFiles = CaptureTypeFiles(manager, SaveSlotType.Auto);
            state.suspicion = 301;
            state.trustMasha = 401;
            SaveData quickSnapshot = null;
            string quickSlotOneBeforeSeventh = string.Empty;
            DateTime previousQuickTime = DateTime.MinValue;
            for (int call = 1; call <= 7; call++)
            {
                Require(manager.SaveQuick(screenshot), $"Public SaveQuick failed on call {call}.");
                int expectedSlot = call <= SaveManager.SlotCount ? call : 1;
                SaveSlotInfo slot = VerifySlot(manager, SaveSlotType.Quick, expectedSlot);
                Require(slot.CreatedAtUtc > previousQuickTime, $"SaveQuick call {call} did not produce a distinguishable createdAtUtc.");
                previousQuickTime = slot.CreatedAtUtc;

                if (call == 1)
                {
                    quickSlotOneBeforeSeventh = slot.Data.createdAtUtc;
                }
                else if (call == 7)
                {
                    Require(slot.Data.createdAtUtc != quickSlotOneBeforeSeventh, "The seventh SaveQuick did not overwrite the oldest Quick slot 1.");
                    quickSnapshot = Clone(slot.Data);
                }

                yield return new WaitForSecondsRealtime(0.08f);
            }

            Require(manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsLoadable) == SaveManager.SlotCount, "Seven SaveQuick calls did not leave six loadable Quick slots.");
            RequireFilesEqual(manualFiles, CaptureTypeFiles(manager, SaveSlotType.Manual), "SaveQuick changed Manual files.");
            RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto), "SaveQuick changed Auto files.");
            Require(quickSnapshot != null, "Quick snapshot was not captured after seven real SaveQuick calls.");
            Require(ParseUtc(quickSnapshot.createdAtUtc) > ParseUtc(autoSnapshot.createdAtUtc), "Quick was not newer than Auto before the first Continue test.");
            SessionState.SetString(QuickSnapshotKey, JsonUtility.ToJson(quickSnapshot));
            Pass("SaveQuick created slots 1-6 and the seventh call overwrote oldest slot 1");

            yield return RunSafely(VerifyTabbedUiAndCapture(controller, manager));

            SaveData manualSnapshot = Clone(VerifySlot(manager, SaveSlotType.Manual, 1).Data);
            yield return RunSafely(VerifyVnLoadConfirmation(
                controller,
                SaveSlotType.Manual,
                1,
                manualSnapshot,
                verifyCancel: true,
                verifyEscape: false));

            yield return RunSafely(VerifyVnLoadConfirmation(
                controller,
                SaveSlotType.Auto,
                1,
                autoSnapshot,
                verifyCancel: false,
                verifyEscape: false));

            yield return RunSafely(VerifyVnLoadConfirmation(
                controller,
                SaveSlotType.Quick,
                1,
                quickSnapshot,
                verifyCancel: false,
                verifyEscape: true));

            UnityEngine.Object.Destroy(screenshot);
            screenshot = null;
            yield return RunSafely(CreateNewestQuickForContinue(controller, manager));
            SessionState.SetString(StageKey, "WaitMainDirectQuickLoad");
            SessionState.SetInt(CounterKey, 0);
            SetDelay(0.75d);
            SceneFlowManager.EnsureInstance().ReturnToMainMenu();
        }
        finally
        {
            if (screenshot != null)
            {
                UnityEngine.Object.Destroy(screenshot);
            }
        }
    }

    private static IEnumerator CreateNewestQuickForContinue(VNDialogueController controller, SaveManager manager)
    {
        Texture2D screenshot = null;
        try
        {
            Dictionary<string, byte[]> autoFiles = CaptureTypeFiles(manager, SaveSlotType.Auto);
            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Require(screenshot != null, "ScreenCapture failed while preparing newest Quick for Continue.");
            Require(manager.SaveQuick(screenshot), "Public SaveQuick failed while making Quick newest for Continue.");

            SaveSlotInfo newestQuick = manager.GetAllSlots(SaveSlotType.Quick)
                .Where(slot => slot.IsLoadable)
                .OrderByDescending(slot => slot.CreatedAtUtc)
                .FirstOrDefault();
            SaveSlotInfo newestAuto = manager.GetAllSlots(SaveSlotType.Auto)
                .Where(slot => slot.IsLoadable)
                .OrderByDescending(slot => slot.CreatedAtUtc)
                .FirstOrDefault();
            Require(newestQuick != null && newestAuto != null, "Could not resolve newest Quick and Auto saves for Continue.");
            VerifySlot(manager, SaveSlotType.Quick, newestQuick.SlotIndex);
            Require(ParseUtc(newestQuick.Data.createdAtUtc) > ParseUtc(newestAuto.Data.createdAtUtc),
                "The fresh Quick save did not become newer than every Auto save before Continue.");
            RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto),
                "Preparing newest Quick changed Auto files.");
            SessionState.SetString(QuickSnapshotKey, JsonUtility.ToJson(newestQuick.Data));
            SessionState.SetInt(QuickSlotIndexKey, newestQuick.SlotIndex);
            Pass("Fresh Quick save became newest after pre-load autosaves");
        }
        finally
        {
            if (screenshot != null)
            {
                UnityEngine.Object.Destroy(screenshot);
            }
        }
    }

    private static void VerifyNewGameAutoSave(
        VNDialogueController controller,
        SaveManager manager)
    {
        SaveSlotInfo slot = VerifySlot(manager, SaveSlotType.Auto, 1);
        Require(
            controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error),
            $"VN position is unavailable while checking New Game autosave: {error}");
        Require(slot.Data.sceneId == sceneId, "New Game autosave sceneId does not match the displayed scene.");
        Require(slot.Data.lineId == lineId, "New Game autosave lineId does not match the displayed first line.");
        Require(slot.Data.lineIndex == lineIndex && lineIndex == 0, "New Game autosave does not point to the first displayed line.");
        Require(!slot.Data.choiceResultActive && slot.Data.selectedChoiceIndex == -1, "New Game autosave contains an unexpected choice result.");
        VerifyBacklogMatches(slot.Data, controller, "New Game autosave");
        Require(controller.CaptureBacklogSnapshot().Count == 1, "New Game did not begin with only its first displayed line in History.");
        Require(controller.notificationPanel == null || !controller.notificationPanel.activeSelf, "Successful autosave displayed a user toast.");
        Pass("New Game automatically created loadable Auto slot 1 at the first displayed line");
    }

    private static IEnumerator VerifyChoiceAutoSave(
        VNDialogueController controller,
        SaveManager manager)
    {
        yield return WaitForOccupiedSlotCount(manager, SaveSlotType.Auto, 2, "choice checkpoint autosave");
        SaveSlotInfo choiceSlot = VerifySlot(manager, SaveSlotType.Auto, 2);
        SaveData checkpoint = Clone(choiceSlot.Data);

        Require(controller.choicePanel != null && controller.choicePanel.activeSelf, "Choice screen closed before its autosave was verified.");
        Require(!checkpoint.choiceResultActive, "Choice autosave was created after applying a choice result.");
        Require(checkpoint.selectedChoiceIndex == -1, "Choice autosave contains a selected choice.");
        Require(string.IsNullOrEmpty(checkpoint.pendingNextSceneId), "Choice autosave contains a pending next scene.");
        VerifyRestoredSnapshot(checkpoint, "choice autosave checkpoint");

        Dictionary<string, byte[]> autoFiles = CaptureTypeFiles(manager, SaveSlotType.Auto);
        controller.choiceMashaButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.1f);
        Require(GameState.Instance.choiceResultActive, "Choice did not alter state before loading its autosave.");
        Require(manager.LoadSlot(SaveSlotType.Auto, 2), "Public LoadSlot(Auto, 2) failed for the choice checkpoint.");
        yield return new WaitForSecondsRealtime(0.2f);

        VerifyChoiceCheckpoint(checkpoint, controller, "public Auto choice-checkpoint LoadSlot");
        RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto), "Loading the choice autosave created or overwrote an Auto slot.");
        Pass("Choice checkpoint autosave restored the real choice screen before selection");
    }

    private static IEnumerator VerifyNoAutoSaveOnRestore(
        VNDialogueController controller,
        SaveManager manager)
    {
        Require(controller.choicePanel != null && controller.choicePanel.activeSelf, "Choice screen is not active before restore suppression tests.");

        Texture2D screenshot = null;
        try
        {
            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Require(screenshot != null, "Could not capture the real choice screen for Manual restore suppression.");
            Require(manager.SaveSlot(1, screenshot), "Could not create Manual slot 1 for restore suppression.");
        }
        finally
        {
            if (screenshot != null)
            {
                UnityEngine.Object.Destroy(screenshot);
            }
        }

        SaveData manualCheckpoint = Clone(VerifySlot(manager, SaveSlotType.Manual, 1).Data);
        SaveSlotInfo newestQuick = manager.GetAllSlots(SaveSlotType.Quick)
            .Where(slot => slot.IsLoadable)
            .OrderByDescending(slot => slot.CreatedAtUtc)
            .FirstOrDefault();
        Require(newestQuick != null, "No Quick choice checkpoint exists for restore suppression.");
        SaveData quickCheckpoint = Clone(newestQuick.Data);
        Dictionary<string, byte[]> autoFiles = CaptureTypeFiles(manager, SaveSlotType.Auto);

        controller.choiceMashaButton.onClick.Invoke();
        Require(manager.LoadSlot(SaveSlotType.Manual, 1), "Public Manual LoadSlot failed during autosave suppression test.");
        yield return new WaitForSecondsRealtime(0.2f);
        VerifyChoiceCheckpoint(manualCheckpoint, controller, "Manual restore suppression");
        RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto), "Manual Load created or overwrote an Auto slot.");

        controller.choiceArtemButton.onClick.Invoke();
        Require(manager.LoadSlot(SaveSlotType.Quick, newestQuick.SlotIndex), "Public Quick LoadSlot failed during autosave suppression test.");
        yield return new WaitForSecondsRealtime(0.2f);
        VerifyChoiceCheckpoint(quickCheckpoint, controller, "Quick restore suppression");
        RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto), "Quick Load created or overwrote an Auto slot.");

        controller.choiceMashaButton.onClick.Invoke();
        SaveData continueCheckpoint = ParseUtc(manualCheckpoint.createdAtUtc) >= ParseUtc(quickCheckpoint.createdAtUtc)
            ? manualCheckpoint
            : quickCheckpoint;
        Require(manager.LoadLatest(), "Public LoadLatest failed during autosave suppression test.");
        yield return new WaitForSecondsRealtime(0.2f);
        VerifyChoiceCheckpoint(continueCheckpoint, controller, "Continue restore suppression");
        RequireFilesEqual(autoFiles, CaptureTypeFiles(manager, SaveSlotType.Auto), "Continue created or overwrote an Auto slot.");
        Pass("Manual, Quick and Continue restoration did not trigger autosave");
    }

    private static void VerifyChoiceCheckpoint(
        SaveData checkpoint,
        VNDialogueController controller,
        string context)
    {
        VerifyRestoredSnapshot(checkpoint, context);
        Require(controller.choicePanel != null && controller.choicePanel.activeSelf, $"Choice screen was not restored during {context}.");
        Require(GameState.Instance.selectedChoiceIndex == -1, $"A selected choice remained during {context}.");
        Require(!GameState.Instance.choiceResultActive, $"Choice result remained active during {context}.");
        Require(string.IsNullOrEmpty(GameState.Instance.pendingNextSceneId), $"Pending next scene remained during {context}.");
    }

    private static IEnumerator WaitForOccupiedSlotCount(
        SaveManager manager,
        SaveSlotType type,
        int expectedCount,
        string context)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            int occupied = manager.GetAllSlots(type).Count(slot => slot.IsOccupied);
            if (occupied == expectedCount)
            {
                yield break;
            }

            yield return null;
        }

        throw new InvalidOperationException($"Timed out waiting for {context}; expected {expectedCount} occupied {type} slots.");
    }

    private static IEnumerator VerifyQuickSaveCommandBeforeChoice(
        VNDialogueController controller,
        SaveManager manager)
    {
        Require(manager.GetAllSlots(SaveSlotType.Quick).All(slot => !slot.IsOccupied), "Quick slots are not empty before command-flow tests.");
        SaveData initialState = CaptureRuntimeState(controller);

        controller.RequestQuickSave();
        yield return WaitForQuickSlotCount(manager, 1, "single quick-save request");
        SaveSlotInfo first = VerifySlot(manager, SaveSlotType.Quick, 1);
        Require(first.Data.version == SaveData.CurrentVersion, "RequestQuickSave wrote an unexpected save version.");
        VerifyRuntimeState(initialState, controller, "single RequestQuickSave");
        VerifyQuickSaveToast(controller);
        Pass("RequestQuickSave created Quick slot 1 without changing VN position or GameState");

        controller.RequestQuickSave();
        controller.RequestQuickSave();
        yield return WaitForQuickSlotCount(manager, 2, "double quick-save request");
        yield return new WaitForSecondsRealtime(0.2f);
        Require(manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsOccupied) == 2, "Double RequestQuickSave created more than one new slot.");
        Require(!manager.GetSlot(SaveSlotType.Quick, 3).IsOccupied, "Double RequestQuickSave unexpectedly created Quick slot 3.");
        VerifySlot(manager, SaveSlotType.Quick, 2);
        Pass("Concurrent RequestQuickSave calls created only one new Quick save");

        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        Require(panel != null, "VNPrototype has no ManualSaveLoadPanel for modal blocking test.");
        panel.OpenLoad();
        panel.SelectAutoTab();
        SaveSlotType selectedType = panel.CurrentSlotType;
        int occupiedBeforeBlockedRequest = manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsOccupied);
        controller.RequestQuickSave();
        yield return new WaitForSecondsRealtime(0.2f);
        Require(manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsOccupied) == occupiedBeforeBlockedRequest, "Quick save ran while Save/Load panel was open.");
        Require(panel.IsOpen, "RequestQuickSave closed the Save/Load panel.");
        Require(panel.CurrentSlotType == selectedType, "RequestQuickSave changed the current Save/Load tab.");

        panel.Close();
        for (int frame = 0; frame < 180 && panel.IsOpen; frame++)
        {
            yield return null;
        }

        Require(!panel.IsOpen, "Save/Load panel did not close before the post-modal quick-save test.");
        controller.RequestQuickSave();
        yield return WaitForQuickSlotCount(manager, 3, "quick save after closing modal UI");
        VerifySlot(manager, SaveSlotType.Quick, 3);
        VerifyQuickSaveToast(controller);
        Pass("Save/Load modal blocked RequestQuickSave without changing its tab, and quick save resumed after close");
    }

    private static IEnumerator VerifyQuickSaveCommandOnChoice(
        VNDialogueController controller,
        SaveManager manager)
    {
        Require(controller.choicePanel != null && controller.choicePanel.activeSelf, "Real choice screen is not active before quick-save test.");
        int occupiedBefore = manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsOccupied);
        controller.RequestQuickSave();
        yield return WaitForQuickSlotCount(manager, occupiedBefore + 1, "quick save on choice screen");

        SaveSlotInfo choiceSlot = VerifySlot(manager, SaveSlotType.Quick, occupiedBefore + 1);
        Require(choiceSlot.IsLoadable, "Quick save created on the choice screen is not loadable.");
        Require(controller.choicePanel.activeSelf, "RequestQuickSave closed the real choice screen.");
        VerifyQuickSaveToast(controller);
        Pass("RequestQuickSave created a loadable save on the real choice screen");
    }

    private static IEnumerator WaitForQuickSlotCount(
        SaveManager manager,
        int expectedCount,
        string context)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            int occupied = manager.GetAllSlots(SaveSlotType.Quick).Count(slot => slot.IsOccupied);
            if (occupied == expectedCount)
            {
                yield break;
            }

            yield return null;
        }

        throw new InvalidOperationException($"Timed out waiting for {context}; expected {expectedCount} occupied Quick slots.");
    }

    private static void CleanupQuickCommandSlots(SaveManager manager)
    {
        CleanupSlots(manager, SaveSlotType.Quick);
    }

    private static void CleanupSlots(SaveManager manager, SaveSlotType type)
    {
        for (int index = 1; index <= SaveManager.SlotCount; index++)
        {
            if (manager.GetSlot(type, index).IsOccupied)
            {
                Require(manager.DeleteSlot(type, index), $"Could not clean {type} test slot {index}.");
            }
        }

        Require(manager.GetAllSlots(type).All(slot => !slot.IsOccupied), $"{type} test cleanup left occupied slots.");
    }

    private static IEnumerator VerifyTabbedUiAndCapture(VNDialogueController controller, SaveManager manager)
    {
        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        Require(panel != null, "VNPrototype has no shared ManualSaveLoadPanel.");
        Require(panel.slotViews != null && panel.slotViews.Length == SaveManager.SlotCount, "Tabbed panel does not contain six slot views.");

        panel.OpenLoad();
        yield return new WaitForSecondsRealtime(0.2f);
        VerifyTabPresentation(panel, manager, SaveSlotType.Manual, false);
        Require(panel.CurrentSlotType == SaveSlotType.Manual, "OpenLoad did not reset the panel to Manual.");

        Vector2Int resolution = TabsUiResolution;
        ConfigureGameViewResolution(resolution);
        for (int frame = 0; frame < 40 && (Screen.width != resolution.x || Screen.height != resolution.y); frame++)
        {
            yield return null;
        }

        Require(Screen.width == resolution.x && Screen.height == resolution.y, $"Game View did not switch to {resolution.x}x{resolution.y}.");

        foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
        {
            SelectTab(panel, type);
            yield return new WaitForSecondsRealtime(0.12f);
            VerifyTabPresentation(panel, manager, type, false);
            VerifyTabsLayout(panel, resolution);
            yield return new WaitForEndOfFrame();
            CaptureTabsScreenshot(type, resolution);
            if (type == SaveSlotType.Manual)
            {
                panel.SelectManualPage(2);
                yield return new WaitForSecondsRealtime(0.12f);
                VerifyTabPresentation(panel, manager, type, false);
                Require(panel.CurrentManualPage == 2 && panel.manualPaginationRoot.activeSelf,
                    "Manual page 2 selection or pagination visibility is incorrect.");
                yield return new WaitForEndOfFrame();
                CaptureTabsScreenshot(type, resolution, "page_2");
            }
        }

        Vector2Int responsiveResolution = new Vector2Int(1280, 720);
        panel.SelectManualTab();
        panel.SelectManualPage(2);
        ConfigureGameViewResolution(responsiveResolution);
        for (int frame = 0; frame < 40 && (Screen.width != responsiveResolution.x || Screen.height != responsiveResolution.y); frame++)
        {
            yield return null;
        }

        Require(Screen.width == responsiveResolution.x && Screen.height == responsiveResolution.y,
            "Game View did not switch to 1280x720 for Save/Load responsive proof.");
        VerifyTabPresentation(panel, manager, SaveSlotType.Manual, false);
        VerifyTabsLayout(panel, responsiveResolution);
        VerifyManualPaginationLayout(panel, responsiveResolution);
        yield return new WaitForEndOfFrame();
        CaptureTabsScreenshot(SaveSlotType.Manual, responsiveResolution, "page_2");
        ConfigureGameViewResolution(resolution);
        yield return null;

        panel.OpenSave();
        yield return new WaitForSecondsRealtime(0.12f);
        VerifyTabPresentation(panel, manager, SaveSlotType.Manual, true);
        Require(panel.CurrentSlotType == SaveSlotType.Manual, "OpenSave did not reset the panel to Manual.");
        Require(panel.slotViews.All(view => view.button.interactable), "Manual slots are not writable in Save mode.");

        Dictionary<string, byte[]> autoFilesBeforeReadOnlyClick = CaptureTypeFiles(manager, SaveSlotType.Auto);
        panel.SelectAutoTab();
        VerifyTabPresentation(panel, manager, SaveSlotType.Auto, true);
        Require(panel.slotViews.All(view => !view.button.interactable), "Auto cards have an active primary click in Save mode.");
        panel.OnSlotSelected(2);
        yield return null;
        RequireFilesEqual(autoFilesBeforeReadOnlyClick, CaptureTypeFiles(manager, SaveSlotType.Auto), "Auto primary click changed files in Save mode.");
        Require(!panel.IsConfirmationOpen, "Auto primary click opened overwrite confirmation in Save mode.");

        Dictionary<string, byte[]> quickFilesBeforeReadOnlyClick = CaptureTypeFiles(manager, SaveSlotType.Quick);
        panel.SelectQuickTab();
        VerifyTabPresentation(panel, manager, SaveSlotType.Quick, true);
        Require(panel.slotViews.All(view => !view.button.interactable), "Quick cards have an active primary click in Save mode.");
        panel.OnSlotSelected(2);
        yield return null;
        RequireFilesEqual(quickFilesBeforeReadOnlyClick, CaptureTypeFiles(manager, SaveSlotType.Quick), "Quick primary click changed files in Save mode.");
        Require(!panel.IsConfirmationOpen, "Quick primary click opened overwrite confirmation in Save mode.");
        Pass("Save mode keeps Manual writable and makes Auto/Quick view-delete only");

        panel.OpenLoad();
        panel.SelectAutoTab();
        Dictionary<string, byte[]> manualBeforeAutoDelete = CaptureTypeFiles(manager, SaveSlotType.Manual);
        Dictionary<string, byte[]> quickBeforeAutoDelete = CaptureTypeFiles(manager, SaveSlotType.Quick);
        panel.OnDeleteRequested(6);
        Require(panel.IsConfirmationOpen, "Auto Delete did not open confirmation.");
        Require(panel.PendingConfirmationSlotType == SaveSlotType.Auto && panel.PendingConfirmationSlot == 6, "Auto Delete confirmation lost type/index.");
        panel.SelectQuickTab();
        Require(panel.CurrentSlotType == SaveSlotType.Auto, "Tab switched while confirmation was open.");
        Require(panel.HandleEscape(), "Escape did not handle Auto Delete confirmation.");
        Require(!panel.IsConfirmationOpen && !panel.PendingConfirmationSlotType.HasValue && panel.PendingConfirmationSlot == 0, "Escape did not clear pending confirmation.");
        Require(manager.GetSlot(SaveSlotType.Auto, 6).IsOccupied, "Escape deleted Auto slot 6.");
        Require(panel.IsOpen, "Escape closed the panel instead of confirmation first.");
        Require(panel.HandleEscape(), "Second Escape did not close the Save/Load panel.");
        yield return new WaitForSecondsRealtime(0.25f);
        Require(!panel.IsOpen, "Second Escape did not close the Save/Load panel.");

        panel.OpenLoad();
        panel.SelectAutoTab();
        panel.OnDeleteRequested(6);
        panel.confirmationYesButton.onClick.Invoke();
        yield return null;
        Require(!manager.GetSlot(SaveSlotType.Auto, 6).IsOccupied, "Confirmed Auto Delete left slot 6 occupied.");
        RequireFilesEqual(manualBeforeAutoDelete, CaptureTypeFiles(manager, SaveSlotType.Manual), "Auto Delete changed Manual files.");
        RequireFilesEqual(quickBeforeAutoDelete, CaptureTypeFiles(manager, SaveSlotType.Quick), "Auto Delete changed Quick files.");
        Require(!panel.slotViews[5].button.interactable, "Deleted Auto slot is active in Load mode.");
        RestoreCapturedFiles(manualBeforeAutoDelete);
        RestoreCapturedFiles(quickBeforeAutoDelete);
        RestoreCapturedFiles(autoFilesBeforeReadOnlyClick);
        Require(manager.GetSlot(SaveSlotType.Auto, 6).IsLoadable, "Auto slot 6 test fixture was not restored after Delete verification.");

        Dictionary<string, byte[]> manualBeforeQuickDelete = CaptureTypeFiles(manager, SaveSlotType.Manual);
        Dictionary<string, byte[]> autoBeforeQuickDelete = CaptureTypeFiles(manager, SaveSlotType.Auto);
        panel.SelectQuickTab();
        panel.OnDeleteRequested(6);
        Require(panel.PendingConfirmationSlotType == SaveSlotType.Quick && panel.PendingConfirmationSlot == 6, "Quick Delete confirmation lost type/index.");
        panel.confirmationYesButton.onClick.Invoke();
        yield return null;
        Require(!manager.GetSlot(SaveSlotType.Quick, 6).IsOccupied, "Confirmed Quick Delete left slot 6 occupied.");
        RequireFilesEqual(manualBeforeQuickDelete, CaptureTypeFiles(manager, SaveSlotType.Manual), "Quick Delete changed Manual files.");
        RequireFilesEqual(autoBeforeQuickDelete, CaptureTypeFiles(manager, SaveSlotType.Auto), "Quick Delete changed Auto files.");
        Require(!panel.slotViews[5].button.interactable, "Deleted Quick slot is active in Load mode.");
        RestoreCapturedFiles(quickFilesBeforeReadOnlyClick);
        Require(manager.GetSlot(SaveSlotType.Quick, 6).IsLoadable, "Quick slot 6 test fixture was not restored after Delete verification.");
        Pass("Typed Delete confirmation is isolated and Escape clears pending state before closing panel");

        panel.Close();
        yield return new WaitForSecondsRealtime(0.2f);
    }

    private static void VerifyTabPresentation(ManualSaveLoadPanel panel, SaveManager manager, SaveSlotType type, bool saveMode)
    {
        Require(panel.CurrentSlotType == type, $"Panel current type is {panel.CurrentSlotType}; expected {type}.");
        string expectedSubtitle = type switch
        {
            SaveSlotType.Auto => "АВТОСОХРАНЕНИЯ",
            SaveSlotType.Quick => "БЫСТРЫЕ СОХРАНЕНИЯ",
            _ => "РУЧНЫЕ СОХРАНЕНИЯ"
        };
        Require(panel.subtitleText != null && panel.subtitleText.text == expectedSubtitle, $"{type} subtitle is incorrect.");

        string expectedHint = saveMode
            ? type switch
            {
                SaveSlotType.Auto => "Автосохранения создаются игрой автоматически",
                SaveSlotType.Quick => "Быстрые сохранения создаются отдельной командой",
                _ => string.Empty
            }
            : string.Empty;
        Require(panel.slotTypeHintText != null && panel.slotTypeHintText.text == expectedHint, $"{type} Save hint is incorrect.");
        Require(panel.slotTypeHintText.gameObject.activeSelf == !string.IsNullOrEmpty(expectedHint), $"{type} Save hint visibility is incorrect.");

        Require(IsTabActive(panel.manualTabButton) == (type == SaveSlotType.Manual), "Manual tab active state is incorrect.");
        Require(IsTabActive(panel.autoTabButton) == (type == SaveSlotType.Auto), "Auto tab active state is incorrect.");
        Require(IsTabActive(panel.quickTabButton) == (type == SaveSlotType.Quick), "Quick tab active state is incorrect.");
        VerifyTabOutline(panel.manualTabButton, type == SaveSlotType.Manual, "Manual");
        VerifyTabOutline(panel.autoTabButton, type == SaveSlotType.Auto, "Auto");
        VerifyTabOutline(panel.quickTabButton, type == SaveSlotType.Quick, "Quick");

        Require(panel.manualPaginationRoot != null && panel.manualPaginationRoot.activeSelf == (type == SaveSlotType.Manual),
            $"{type} pagination visibility is incorrect.");
        for (int i = 0; i < SaveManager.SlotsPerPage; i++)
        {
            int localSlotIndex = i + 1;
            int slotIndex = type == SaveSlotType.Manual
                ? ManualSaveLoadPanel.GetGlobalManualSlot(panel.CurrentManualPage, localSlotIndex)
                : localSlotIndex;
            ManualSaveSlotView view = panel.slotViews[i];
            SaveSlotInfo slot = manager.GetSlot(type, slotIndex);
            string expectedNumber = slot.IsLoadable ? localSlotIndex.ToString() : string.Empty;
            Require(view.slotNumberText.text == expectedNumber, $"{type} card {localSlotIndex} number label is incorrect.");
            Require(!view.backgroundSlotNumberText.gameObject.activeSelf, $"{type} card {localSlotIndex} exposes a giant background slot number.");
            bool expectedPrimary = saveMode ? type == SaveSlotType.Manual : slot.IsLoadable;
            Require(view.button.interactable == expectedPrimary, $"{type} card {localSlotIndex} primary interaction is incorrect.");
            if (!slot.IsOccupied) Require(view.emptyText.text == "Пусто", $"{type} card {localSlotIndex} empty label is incorrect.");
        }    }

    private static string GetButtonLabel(UnityEngine.UI.Button button)
    {
        TMPro.TextMeshProUGUI label = button != null
            ? button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true)
            : null;
        return label != null ? label.text : string.Empty;
    }

    private static bool IsTabActive(UnityEngine.UI.Button button)
    {
        Transform accent = button != null ? button.transform.Find("Active Accent") : null;
        return accent != null && accent.gameObject.activeSelf;
    }

    private static void VerifyTabOutline(UnityEngine.UI.Button button, bool active, string label)
    {
        UnityEngine.UI.Outline outline = button != null ? button.GetComponent<UnityEngine.UI.Outline>() : null;
        Require(outline != null, $"{label} tab has no Outline.");
        Color expected = active ? ActiveTabOutlineColor : InactiveTabOutlineColor;
        Require(
            Mathf.Abs(outline.effectColor.r - expected.r) < 0.001f
                && Mathf.Abs(outline.effectColor.g - expected.g) < 0.001f
                && Mathf.Abs(outline.effectColor.b - expected.b) < 0.001f
                && Mathf.Abs(outline.effectColor.a - expected.a) < 0.001f,
            $"{label} tab Outline color is incorrect for active={active}.");
    }

    private static void SelectTab(ManualSaveLoadPanel panel, SaveSlotType type)
    {
        switch (type)
        {
            case SaveSlotType.Auto:
                panel.SelectAutoTab();
                break;
            case SaveSlotType.Quick:
                panel.SelectQuickTab();
                break;
            default:
                panel.SelectManualTab();
                break;
        }
    }

    private static void VerifyTabsLayout(ManualSaveLoadPanel panel, Vector2Int resolution)
    {
        Require(Screen.width == resolution.x && Screen.height == resolution.y, "Tabbed layout resolution does not match Game View.");
        Rect window = GetScreenRect(panel.windowRect);
        Require(window.xMin >= -2f && window.yMin >= -2f && window.xMax <= Screen.width + 2f && window.yMax <= Screen.height + 2f, $"Save window is clipped at {resolution.x}x{resolution.y}.");

        Rect title = GetScreenRect(panel.titleText.rectTransform);
        Rect tabs = GetScreenRect(panel.manualTabButton.transform.parent as RectTransform);
        Require(tabs.yMax < title.yMin, $"Tabs overlap the title at {resolution.x}x{resolution.y}.");

        Rect[] cards = panel.slotViews.Select(view => GetScreenRect(view.cardRect)).ToArray();
        Require(cards.All(card => card.width > 200f && card.height > 140f), $"Cards became unreadable at {resolution.x}x{resolution.y}.");
        Require(cards.All(card => card.xMin >= 0f && card.yMin >= 0f && card.xMax <= Screen.width && card.yMax <= Screen.height), $"A card is outside screen at {resolution.x}x{resolution.y}.");
        Require(tabs.yMin > cards.Max(card => card.yMax), $"Tabs overlap cards at {resolution.x}x{resolution.y}.");
        Require(GetScreenRect(panel.closeButton.transform as RectTransform).xMax <= Screen.width + 1f, $"Back button is clipped at {resolution.x}x{resolution.y}.");
        Require(GetScreenRect(panel.statusText.rectTransform).yMax <= cards.Min(card => card.yMin) + 2f, $"Toast overlaps cards at {resolution.x}x{resolution.y}.");
    }

    private static void VerifyManualPaginationLayout(ManualSaveLoadPanel panel, Vector2Int resolution)
    {
        Require(panel.manualPaginationRoot != null && panel.manualPaginationRoot.activeSelf,
            "Manual pagination is hidden in responsive proof.");
        Require(panel.manualPageButtons != null && panel.manualPageButtons.Length == SaveManager.ManualPageCount,
            "Manual pagination does not expose ten page buttons.");
        Rect row = GetScreenRect(panel.manualPaginationRoot.transform as RectTransform);
        Require(row.xMin >= 0f && row.yMin >= 0f && row.xMax <= resolution.x && row.yMax <= resolution.y,
            "Manual pagination row is outside the responsive viewport.");
        Require(GetScreenRect(panel.previousManualPageButton.transform as RectTransform).xMin >= row.xMin
                && GetScreenRect(panel.nextManualPageButton.transform as RectTransform).xMax <= row.xMax,
            "Manual pagination arrows are outside the row.");
        Require(panel.manualPageButtons.All(button => GetScreenRect(button.transform as RectTransform).width >= 15f),
            "Manual page buttons are too small at 1280x720.");
    }

    private static Rect GetScreenRect(RectTransform rectTransform)
    {
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        return Rect.MinMaxRect(
            corners.Min(point => point.x),
            corners.Min(point => point.y),
            corners.Max(point => point.x),
            corners.Max(point => point.y));
    }

    private static void CaptureTabsScreenshot(SaveSlotType type, Vector2Int resolution, string suffix = null)
    {
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        Require(screenshot != null, $"Could not capture {type} tab screenshot at {resolution.x}x{resolution.y}.");
        try
        {
            Require(screenshot.width == resolution.x && screenshot.height == resolution.y, $"{type} screenshot size is {screenshot.width}x{screenshot.height}; expected {resolution.x}x{resolution.y}.");
            string path = GetTabsScreenshotPath(type, resolution, suffix);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            Require(File.Exists(path), $"Tabbed screenshot was not written to '{path}'.");
        }
        finally
        {
            UnityEngine.Object.Destroy(screenshot);
        }
    }

    private static string GetTabsScreenshotPath(SaveSlotType type, Vector2Int resolution, string suffix = null)
    {
        string typeName = type.ToString().ToLowerInvariant();
        return Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "QAArtifacts",
            "GraphicalE2E",
            "SaveBackendV2",
            $"save_load_{typeName}" + (string.IsNullOrEmpty(suffix) ? string.Empty : $"_{suffix}") + $"_{resolution.x}x{resolution.y}.png"));
    }

    private static IEnumerator AdvanceToChoice(VNDialogueController controller)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            if (controller.choicePanel != null && controller.choicePanel.activeSelf)
            {
                yield break;
            }

            controller.AdvanceDialogue();
            yield return new WaitForSecondsRealtime(0.05f);
        }

        throw new InvalidOperationException("The real VN choice did not appear after 80 advance attempts.");
    }

    private static IEnumerator VerifyVnLoadConfirmation(
        VNDialogueController controller,
        SaveSlotType slotType,
        int slotIndex,
        SaveData snapshot,
        bool verifyCancel,
        bool verifyEscape)
    {
        yield return MutateDialogueAndState(controller, snapshot);
        SaveData mutatedState = CaptureRuntimeState(controller);
        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        Require(panel != null, "VNPrototype has no ManualSaveLoadPanel for Load confirmation.");
        SaveManager manager = SaveManager.Instance;
        Require(manager != null, "SaveManager is missing before VN Load confirmation.");
        string autoSignatureBeforeConfirmation = GetTypeFilesSignature(manager, SaveSlotType.Auto);
        Dictionary<string, byte[]> autoFilesBeforeConfirmation = CaptureTypeFiles(manager, SaveSlotType.Auto);
        Dictionary<int, string> autoTimesBeforeConfirmation = manager.GetAllSlots(SaveSlotType.Auto)
            .ToDictionary(slot => slot.SlotIndex, slot => slot.Data != null ? slot.Data.createdAtUtc : string.Empty);

        panel.OpenLoad();
        SelectTab(panel, slotType);
        panel.OnSlotSelected(slotIndex);

        Require(panel.IsConfirmationOpen, $"{slotType} VN Load did not open confirmation.");
        Require(panel.PendingConfirmationSlotType == slotType && panel.PendingConfirmationSlot == slotIndex,
            $"{slotType} Load confirmation lost its pending type or index.");
        Require(panel.confirmationText != null
                && panel.confirmationText.text == "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u044d\u0442\u043e \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435? \u041d\u0435\u0441\u043e\u0445\u0440\u0430\u043d\u0451\u043d\u043d\u044b\u0439 \u043f\u0440\u043e\u0433\u0440\u0435\u0441\u0441 \u0431\u0443\u0434\u0435\u0442 \u043f\u043e\u0442\u0435\u0440\u044f\u043d.",
            $"{slotType} Load confirmation text is incorrect: '{panel.confirmationText?.text}'.");
        Require(GetButtonLabel(panel.confirmationYesButton) == "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c", $"{slotType} Load confirmation button label is incorrect.");
        Require(GetButtonLabel(panel.confirmationNoButton) == "\u041e\u0442\u043c\u0435\u043d\u0430", $"{slotType} Load confirmation cancel label is incorrect.");
        VerifyRuntimeState(mutatedState, controller, $"{slotType} Load before confirmation");

        SaveSlotType otherType = slotType == SaveSlotType.Quick ? SaveSlotType.Manual : SaveSlotType.Quick;
        SelectTab(panel, otherType);
        Require(panel.CurrentSlotType == slotType, $"{slotType} Load confirmation allowed a tab switch.");

        if (verifyCancel)
        {
            panel.confirmationNoButton.onClick.Invoke();
            Require(!panel.IsConfirmationOpen && panel.IsOpen, "Load Cancel did not return to the open panel.");
            Require(!panel.PendingConfirmationSlotType.HasValue && panel.PendingConfirmationSlot == 0,
                "Load Cancel did not clear pending confirmation.");
            Require(GetTypeFilesSignature(manager, SaveSlotType.Auto) == autoSignatureBeforeConfirmation,
                "Load Cancel created or overwrote an Auto slot.");
            VerifyRuntimeState(mutatedState, controller, "Manual Load after Cancel");

            panel.confirmationYesButton.onClick.Invoke();
            VerifyRuntimeState(mutatedState, controller, "stale Load confirmation after Cancel");

            panel.OnSlotSelected(slotIndex);
            Require(panel.IsConfirmationOpen, "Manual Load did not reopen confirmation after Cancel.");
        }

        if (verifyEscape)
        {
            Require(panel.HandleEscape(), "Escape did not handle the VN Load confirmation.");
            Require(!panel.IsConfirmationOpen && panel.IsOpen, "First Escape did not close only the Load confirmation.");
            Require(!panel.PendingConfirmationSlotType.HasValue && panel.PendingConfirmationSlot == 0,
                "Escape did not clear pending Load confirmation.");
            Require(GetTypeFilesSignature(manager, SaveSlotType.Auto) == autoSignatureBeforeConfirmation,
                "Load Escape created or overwrote an Auto slot.");
            VerifyRuntimeState(mutatedState, controller, "Quick Load after Escape");

            Require(panel.HandleEscape(), "Second Escape did not close the Save/Load panel.");
            for (int frame = 0; frame < 40 && panel.IsOpen; frame++)
            {
                yield return null;
            }

            Require(!panel.IsOpen, "Second Escape did not close the Save/Load panel.");
            panel.OpenLoad();
            SelectTab(panel, slotType);
            panel.OnSlotSelected(slotIndex);
            Require(panel.IsConfirmationOpen, "Quick Load did not reopen confirmation after Escape.");
        }

        panel.confirmationYesButton.onClick.Invoke();
        bool requiresPreLoadAutoSave = slotType == SaveSlotType.Manual || slotType == SaveSlotType.Quick;
        if (requiresPreLoadAutoSave)
        {
            Require(panel.LoadInProgress, $"{slotType} Load did not block the panel during its pre-load autosave.");
            Require(panel.canvasGroup == null || panel.canvasGroup.alpha < 0.01f,
                $"{slotType} pre-load autosave did not hide Save/Load UI before screenshot capture.");
            Require(!panel.HandleEscape(), $"{slotType} Load allowed Escape to interrupt its pre-load autosave.");
            SelectTab(panel, otherType);
            Require(panel.CurrentSlotType == slotType, $"{slotType} Load allowed a tab switch during its pre-load autosave.");
        }

        yield return new WaitForSecondsRealtime(0.35f);
        VerifyRestoredSnapshot(snapshot, $"confirmed {slotType} Load");
        Require(!panel.IsOpen, $"Confirmed {slotType} Load did not close the Save/Load panel.");

        if (requiresPreLoadAutoSave)
        {
            Require(CountChangedAutoSlots(manager, autoTimesBeforeConfirmation) == 1,
                $"{slotType} Load did not create exactly one rotating Auto checkpoint.");
            SaveSlotInfo preLoadCheckpoint = manager.GetAllSlots(SaveSlotType.Auto)
                .Where(slot => slot.IsLoadable)
                .OrderByDescending(slot => slot.CreatedAtUtc)
                .FirstOrDefault();
            Require(preLoadCheckpoint != null, $"{slotType} pre-load checkpoint is missing.");
            VerifySlot(manager, SaveSlotType.Auto, preLoadCheckpoint.SlotIndex);
            VerifySaveDataMatchesRuntime(mutatedState, preLoadCheckpoint.Data, $"{slotType} pre-load checkpoint");

            SaveData preLoadSnapshot = Clone(preLoadCheckpoint.Data);
            Require(manager.LoadSlot(SaveSlotType.Auto, preLoadCheckpoint.SlotIndex),
                $"Could not load the {slotType} pre-load Auto checkpoint.");
            yield return new WaitForSecondsRealtime(0.2f);
            VerifyRestoredSnapshot(preLoadSnapshot, $"{slotType} pre-load Auto checkpoint restore");

            Require(manager.LoadSlot(slotType, slotIndex),
                $"Could not return from the {slotType} pre-load checkpoint to the target save.");
            yield return new WaitForSecondsRealtime(0.2f);
            VerifyRestoredSnapshot(snapshot, $"{slotType} target restore after pre-load checkpoint");
        }
        else
        {
            RequireFilesEqual(autoFilesBeforeConfirmation, CaptureTypeFiles(manager, SaveSlotType.Auto),
                "Auto Load created or overwrote an Auto slot.");
        }

        Pass($"VN {slotType} Load required confirmation and restored only after confirmation");
    }

    private static int CountChangedAutoSlots(SaveManager manager, IReadOnlyDictionary<int, string> before)
    {
        return manager.GetAllSlots(SaveSlotType.Auto)
            .Count(slot => !before.TryGetValue(slot.SlotIndex, out string previousCreatedAt)
                || !string.Equals(previousCreatedAt, slot.Data != null ? slot.Data.createdAtUtc : string.Empty, StringComparison.Ordinal));
    }

    private static void VerifySaveDataMatchesRuntime(SaveData expected, SaveData actual, string context)
    {
        Require(actual != null, $"SaveData is missing during {context}.");
        Require(actual.sceneId == expected.sceneId, $"sceneId mismatch during {context}.");
        Require(actual.lineId == expected.lineId, $"lineId mismatch during {context}.");
        Require(actual.lineIndex == expected.lineIndex, $"lineIndex mismatch during {context}.");
        Require(actual.selectedChoiceIndex == expected.selectedChoiceIndex, $"choice index mismatch during {context}.");
        Require(actual.choiceResultActive == expected.choiceResultActive, $"choice result mismatch during {context}.");
        Require(actual.pendingNextSceneId == expected.pendingNextSceneId, $"pending scene mismatch during {context}.");
        Require(actual.lust == expected.lust && actual.romance == expected.romance && actual.purity == expected.purity,
            $"core relation values mismatch during {context}.");
        Require(actual.corruptionLevel == expected.corruptionLevel && actual.selfControl == expected.selfControl,
            $"progress values mismatch during {context}.");
        Require(actual.suspicion == expected.suspicion && actual.trustMasha == expected.trustMasha
                && actual.trustArtem == expected.trustArtem && actual.leraInterest == expected.leraInterest,
            $"character relation values mismatch during {context}.");
        Require(BacklogTexts(actual).SequenceEqual(BacklogTexts(expected)),
            $"backlog snapshot mismatch during {context}.");
    }

    private static IEnumerator MutateDialogueAndState(VNDialogueController controller, SaveData snapshot)
    {
        GameState state = GameState.Instance;
        DialogueSceneData scene = controller.sceneRegistry.FindById(snapshot.sceneId);
        Require(scene != null && scene.lines != null && scene.lines.Count > 1 && scene.lines[0] != null,
            "Could not find an earlier valid line for LoadSlot mutation.");
        state.currentSceneId = scene.sceneId;
        state.currentLineIndex = 0;
        state.currentLineId = scene.lines[0].lineId;
        state.selectedChoiceIndex = -1;
        state.choiceResultActive = false;
        state.pendingNextSceneId = string.Empty;
        Require(controller.RestoreFromGameState(), "Could not move VN to an earlier real line before LoadSlot.");
        yield return new WaitForSecondsRealtime(0.1f);

        state.suspicion = snapshot.suspicion + 700;
        state.trustMasha = snapshot.trustMasha + 700;

        bool positionChanged = state.currentSceneId != snapshot.sceneId
            || state.currentLineId != snapshot.lineId
            || state.currentLineIndex != snapshot.lineIndex;
        Require(positionChanged, "The real VN dialogue did not move away from the saved position before LoadSlot.");
        Require(state.suspicion != snapshot.suspicion && state.trustMasha != snapshot.trustMasha, "GameState was not changed before LoadSlot.");
    }

    private static void WaitMainDirectQuickLoad()
    {
        if (!TryGetReadyMainMenu(out MainMenuController menu, out SaveManager manager))
        {
            Retry("MainMenu was not ready for direct Quick Load.");
            return;
        }

        ConfigureTemporaryDirectory(manager);
        SaveData quick = ReadSnapshot(QuickSnapshotKey);
        SaveSlotInfo newestAuto = manager.GetAllSlots(SaveSlotType.Auto).Where(slot => slot.IsLoadable).OrderByDescending(slot => slot.CreatedAtUtc).FirstOrDefault();
        int quickSlotIndex = SessionState.GetInt(QuickSlotIndexKey, 0);
        Require(quickSlotIndex > 0, "Fresh Quick slot index is unavailable before direct Load.");
        Require(newestAuto != null && ParseUtc(quick.createdAtUtc) > ParseUtc(newestAuto.Data.createdAtUtc), "Quick is not the newest save before direct Load.");
        Require(manager.HasAnyValidSave(), "Continue found no valid backend saves.");
        SessionState.SetString(AutoFilesSignatureKey, GetTypeFilesSignature(manager, SaveSlotType.Auto));

        ManualSaveLoadPanel panel = menu.manualSaveLoadPanel;
        Require(panel != null, "MainMenu has no ManualSaveLoadPanel for immediate Load test.");
        panel.OpenLoad();
        panel.SelectQuickTab();
        Require(panel.slotViews[quickSlotIndex - 1].button.interactable, $"Quick slot {quickSlotIndex} is disabled in Main Menu Load mode.");
        panel.OnSlotSelected(quickSlotIndex);
        Require(!panel.IsConfirmationOpen, "Main Menu Load opened a confirmation modal.");
        Pass("Main Menu Quick Load started immediately without confirmation");

        SessionState.SetString(StageKey, "WaitVnDirectQuickLoad");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.75d);
    }

    private static void WaitVnDirectQuickLoad()
    {
        SaveData quick = ReadSnapshot(QuickSnapshotKey);
        if (!IsVnReadyForSnapshot(quick))
        {
            Retry("VNPrototype was not ready after direct Quick Load.");
            return;
        }

        VerifyRestoredSnapshot(quick, "Main Menu direct Quick Load");
        Require(
            GetTypeFilesSignature(SaveManager.Instance, SaveSlotType.Auto)
                == SessionState.GetString(AutoFilesSignatureKey, string.Empty),
            "Main Menu direct Quick Load created or overwrote an Auto slot during restoration.");
        Pass("Main Menu Quick Load started immediately without confirmation and restored Quick");

        SessionState.SetString(StageKey, "WaitMainQuickContinue");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.75d);
        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    private static void WaitMainQuickContinue()
    {
        if (!TryGetReadyMainMenu(out MainMenuController menu, out SaveManager manager))
        {
            Retry("MainMenu was not ready for Quick Continue.");
            return;
        }

        ConfigureTemporaryDirectory(manager);
        SaveData quick = ReadSnapshot(QuickSnapshotKey);
        SaveSlotInfo newestAuto = manager.GetAllSlots(SaveSlotType.Auto).Where(slot => slot.IsLoadable).OrderByDescending(slot => slot.CreatedAtUtc).FirstOrDefault();
        Require(newestAuto != null && ParseUtc(quick.createdAtUtc) > ParseUtc(newestAuto.Data.createdAtUtc), "Quick is not the newest save before Continue.");
        Require(manager.HasAnyValidSave(), "Continue found no valid backend saves.");
        SessionState.SetString(AutoFilesSignatureKey, GetTypeFilesSignature(manager, SaveSlotType.Auto));

        SessionState.SetString(StageKey, "WaitVnQuickContinue");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.75d);
        menu.ContinueFromLatestSave();
    }

    private static void WaitVnQuickContinue()
    {
        SaveData quick = ReadSnapshot(QuickSnapshotKey);
        if (!IsVnReadyForSnapshot(quick))
        {
            Retry("VNPrototype was not ready after Quick Continue.");
            return;
        }

        VerifyRestoredSnapshot(quick, "Quick Continue");
        Require(
            GetTypeFilesSignature(SaveManager.Instance, SaveSlotType.Auto)
                == SessionState.GetString(AutoFilesSignatureKey, string.Empty),
            "Quick Continue created or overwrote an Auto slot during restoration.");
        Pass("Continue selected the newest Quick save");
        SessionState.SetString(StageKey, "WaitAutoNewestCoroutine");
        SessionState.SetInt(CounterKey, 0);
        VNDialogueController.Instance.StartCoroutine(RunSafely(CreateNewestAutoAndReturn()));
    }

    private static IEnumerator CreateNewestAutoAndReturn()
    {
        Texture2D screenshot = null;
        try
        {
            yield return new WaitForSecondsRealtime(0.12f);
            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Require(screenshot != null, "ScreenCapture failed while preparing newest Auto for Continue.");

            SaveManager manager = SaveManager.Instance;
            Dictionary<string, byte[]> quickFiles = CaptureTypeFiles(manager, SaveSlotType.Quick);
            Dictionary<int, string> autoTimesBefore = manager.GetAllSlots(SaveSlotType.Auto)
                .ToDictionary(slot => slot.SlotIndex, slot => slot.Data != null ? slot.Data.createdAtUtc : string.Empty);
            GameState.Instance.suspicion = 501;
            GameState.Instance.trustMasha = 601;
            Require(manager.SaveAuto(screenshot), "Public SaveAuto failed while making Auto newest for Continue.");
            Require(CountChangedAutoSlots(manager, autoTimesBefore) == 1,
                "SaveAuto did not create or overwrite exactly one logical Auto slot while preparing Continue.");

            SaveSlotInfo newestAuto = manager.GetAllSlots(SaveSlotType.Auto)
                .Single(slot => !autoTimesBefore.TryGetValue(slot.SlotIndex, out string previousCreatedAt)
                    || !string.Equals(previousCreatedAt, slot.Data != null ? slot.Data.createdAtUtc : string.Empty, StringComparison.Ordinal));
            VerifySlot(manager, SaveSlotType.Auto, newestAuto.SlotIndex);
            SaveData quick = ReadSnapshot(QuickSnapshotKey);
            Require(ParseUtc(newestAuto.Data.createdAtUtc) > ParseUtc(quick.createdAtUtc), "The additional SaveAuto did not become newest.");
            RequireFilesEqual(quickFiles, CaptureTypeFiles(manager, SaveSlotType.Quick), "SaveAuto changed Quick files while preparing Continue.");
            SessionState.SetString(AutoNewestSnapshotKey, JsonUtility.ToJson(newestAuto.Data));

            UnityEngine.Object.Destroy(screenshot);
            screenshot = null;
            SessionState.SetString(StageKey, "WaitMainAutoContinue");
            SessionState.SetInt(CounterKey, 0);
            SetDelay(0.75d);
            SceneFlowManager.EnsureInstance().ReturnToMainMenu();
        }
        finally
        {
            if (screenshot != null)
            {
                UnityEngine.Object.Destroy(screenshot);
            }
        }
    }

    private static IEnumerator RunSafely(IEnumerator routine)
    {
        while (true)
        {
            bool moved = false;
            bool failed = false;
            object current = null;
            Exception failure = null;

            try
            {
                moved = routine.MoveNext();
                if (moved)
                {
                    current = routine.Current;
                }
            }
            catch (Exception exception)
            {
                failed = true;
                failure = exception;
            }

            if (failed)
            {
                (routine as IDisposable)?.Dispose();
                Fail(failure.ToString());
                yield break;
            }

            if (!moved)
            {
                (routine as IDisposable)?.Dispose();
                yield break;
            }

            yield return current;
        }
    }

    private static void WaitMainAutoContinue()
    {
        if (!TryGetReadyMainMenu(out MainMenuController menu, out SaveManager manager))
        {
            Retry("MainMenu was not ready for Auto Continue.");
            return;
        }

        ConfigureTemporaryDirectory(manager);
        SaveData auto = ReadSnapshot(AutoNewestSnapshotKey);
        SaveData quick = ReadSnapshot(QuickSnapshotKey);
        Require(ParseUtc(auto.createdAtUtc) > ParseUtc(quick.createdAtUtc), "Auto is not the newest save before Continue.");
        SessionState.SetString(AutoFilesSignatureKey, GetTypeFilesSignature(manager, SaveSlotType.Auto));

        SessionState.SetString(StageKey, "WaitVnAutoContinue");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.75d);
        menu.ContinueFromLatestSave();
    }

    private static void WaitVnAutoContinue()
    {
        SaveData auto = ReadSnapshot(AutoNewestSnapshotKey);
        if (!IsVnReadyForSnapshot(auto))
        {
            Retry("VNPrototype was not ready after Auto Continue.");
            return;
        }

        VerifyRestoredSnapshot(auto, "Auto Continue");
        Require(
            GetTypeFilesSignature(SaveManager.Instance, SaveSlotType.Auto)
                == SessionState.GetString(AutoFilesSignatureKey, string.Empty),
            "Auto Continue created or overwrote an Auto slot during restoration.");
        Pass("Continue selected the newest Auto save");
        Success();
    }

    private static SaveSlotInfo VerifySlot(SaveManager manager, SaveSlotType type, int index)
    {
        SaveSlotInfo slot = manager.GetSlot(type, index);
        Require(slot.IsLoadable && slot.Data != null, $"{type} slot {index} is not loadable: {slot.Error}");
        Require(File.Exists(slot.JsonPath), $"{type} slot {index} JSON is missing: '{slot.JsonPath}'.");
        Require(File.Exists(slot.PreviewPath), $"{type} slot {index} PNG is missing: '{slot.PreviewPath}'.");
        Require(slot.Data.version == SaveData.CurrentVersion,
            $"{type} slot {index} version is {slot.Data.version}; expected {SaveData.CurrentVersion}.");
        Require(slot.Data.slotType == type, $"{type} slot {index} JSON contains slotType {slot.Data.slotType}.");
        Require(slot.Data.slotIndex == index, $"{type} slot {index} JSON contains slotIndex {slot.Data.slotIndex}.");

        string expectedStem = type switch
        {
            SaveSlotType.Manual => $"slot_{index:D2}",
            SaveSlotType.Auto => $"auto_{index:D2}",
            SaveSlotType.Quick => $"quick_{index:D2}",
            _ => string.Empty
        };
        Require(Path.GetFileName(slot.JsonPath) == expectedStem + ".json", $"{type} slot {index} JSON filename is incorrect.");
        Require(Path.GetFileName(slot.PreviewPath) == expectedStem + ".png", $"{type} slot {index} PNG filename is incorrect.");
        VerifyPng(slot.PreviewPath);
        return slot;
    }

    private static void VerifyPng(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            Require(texture.LoadImage(File.ReadAllBytes(path), true), $"Preview '{path}' is not a readable PNG.");
            Require(texture.width == SaveManager.PreviewWidth && texture.height == SaveManager.PreviewHeight,
                $"Preview '{path}' is {texture.width}x{texture.height}; expected {SaveManager.PreviewWidth}x{SaveManager.PreviewHeight}.");
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    private static void VerifyRestoredSnapshot(SaveData snapshot, string context)
    {
        GameState state = GameState.Instance;
        Require(state != null, $"GameState is missing during {context}.");
        Require(state.currentSceneId == snapshot.sceneId, $"sceneId mismatch during {context}.");
        Require(state.currentLineId == snapshot.lineId, $"lineId mismatch during {context}.");
        Require(state.currentLineIndex == snapshot.lineIndex, $"lineIndex mismatch during {context}.");
        Require(state.suspicion == snapshot.suspicion, $"suspicion mismatch during {context}.");
        Require(state.trustMasha == snapshot.trustMasha, $"trustMasha mismatch during {context}.");
        Require(state.selectedChoiceIndex == snapshot.selectedChoiceIndex, $"selectedChoiceIndex mismatch during {context}.");
        Require(state.choiceResultActive == snapshot.choiceResultActive, $"choiceResultActive mismatch during {context}.");
        Require(state.pendingNextSceneId == snapshot.pendingNextSceneId, $"pendingNextSceneId mismatch during {context}.");

        VNDialogueController controller = VNDialogueController.Instance;
        Require(controller != null, $"VNDialogueController is missing during {context}.");
        Require(controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error),
            $"VN position is unavailable during {context}: {error}");
        Require(sceneId == snapshot.sceneId && lineId == snapshot.lineId && lineIndex == snapshot.lineIndex,
            $"VNDialogueController position mismatch during {context}.");
        VerifyBacklogMatches(snapshot, controller, context);
    }

    private static SaveData CaptureRuntimeState(VNDialogueController controller)
    {
        GameState state = GameState.Instance;
        Require(state != null, "GameState is missing while capturing quick-save command state.");
        Require(
            controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error),
            $"VN position is unavailable before quick save: {error}");

        return new SaveData
        {
            sceneId = sceneId,
            lineId = lineId,
            lineIndex = lineIndex,
            selectedChoiceIndex = state.selectedChoiceIndex,
            choiceResultActive = state.choiceResultActive,
            pendingNextSceneId = state.pendingNextSceneId,
            backlogEntries = controller.CaptureBacklogSnapshot().Select(entry => new BacklogEntryData
            {
                speaker = entry.speaker,
                text = entry.text
            }).ToList(),
            lust = state.lust,
            romance = state.romance,
            purity = state.purity,
            corruptionLevel = state.corruptionLevel,
            selfControl = state.selfControl,
            suspicion = state.suspicion,
            trustMasha = state.trustMasha,
            trustArtem = state.trustArtem,
            leraInterest = state.leraInterest
        };
    }

    private static void VerifyRuntimeState(
        SaveData expected,
        VNDialogueController controller,
        string context)
    {
        GameState state = GameState.Instance;
        Require(state != null, $"GameState is missing during {context}.");
        Require(state.currentSceneId == expected.sceneId, $"sceneId changed during {context}.");
        Require(state.currentLineId == expected.lineId, $"lineId changed during {context}.");
        Require(state.currentLineIndex == expected.lineIndex, $"lineIndex changed during {context}.");
        Require(state.selectedChoiceIndex == expected.selectedChoiceIndex, $"selectedChoiceIndex changed during {context}.");
        Require(state.choiceResultActive == expected.choiceResultActive, $"choiceResultActive changed during {context}.");
        Require(state.pendingNextSceneId == expected.pendingNextSceneId, $"pendingNextSceneId changed during {context}.");
        Require(state.lust == expected.lust, $"lust changed during {context}.");
        Require(state.romance == expected.romance, $"romance changed during {context}.");
        Require(state.purity == expected.purity, $"purity changed during {context}.");
        Require(state.corruptionLevel == expected.corruptionLevel, $"corruptionLevel changed during {context}.");
        Require(state.selfControl == expected.selfControl, $"selfControl changed during {context}.");
        Require(state.suspicion == expected.suspicion, $"suspicion changed during {context}.");
        Require(state.trustMasha == expected.trustMasha, $"trustMasha changed during {context}.");
        Require(state.trustArtem == expected.trustArtem, $"trustArtem changed during {context}.");
        Require(state.leraInterest == expected.leraInterest, $"leraInterest changed during {context}.");
        Require(
            controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error),
            $"VN position is unavailable during {context}: {error}");
        Require(
            sceneId == expected.sceneId && lineId == expected.lineId && lineIndex == expected.lineIndex,
            $"VNDialogueController position changed during {context}.");
        VerifyBacklogMatches(expected, controller, context);
    }

    private static void VerifyBacklogMatches(
        SaveData expected,
        VNDialogueController controller,
        string context)
    {
        Require(expected != null && expected.backlogEntries != null,
            $"Expected backlog snapshot is missing during {context}.");
        List<DialogueBacklogEntry> actual = controller.CaptureBacklogSnapshot();
        Require(actual.Count == expected.backlogEntries.Count,
            $"History count mismatch during {context}: actual {actual.Count}, expected {expected.backlogEntries.Count}.");

        for (int index = 0; index < actual.Count; index++)
        {
            BacklogEntryData expectedEntry = expected.backlogEntries[index];
            Require(expectedEntry != null, $"Expected History entry {index} is null during {context}.");
            Require(actual[index].speaker == (expectedEntry.speaker ?? string.Empty)
                    && actual[index].text == expectedEntry.text,
                $"History entry {index} mismatch during {context}.");
        }

        if (expected.choiceResultActive)
        {
            Require(expected.backlogEntries.Count > 0, $"Choice result snapshot is empty during {context}.");
            string resultText = expected.backlogEntries[expected.backlogEntries.Count - 1].text;
            Require(actual.Count(entry => entry.text == resultText) == 1,
                $"Choice result was duplicated in History during {context}.");
        }
    }

    private static IEnumerable<string> BacklogTexts(SaveData data)
    {
        return data?.backlogEntries == null
            ? Enumerable.Empty<string>()
            : data.backlogEntries.Where(entry => entry != null).Select(entry => $"{entry.speaker ?? string.Empty}\u001f{entry.text}");
    }

    private static void VerifyQuickSaveToast(VNDialogueController controller)
    {
        Require(controller.notificationPanel != null && controller.notificationPanel.activeSelf, "Quick-save success toast is not visible.");
        Require(
            controller.notificationText != null
                && controller.notificationText.text == "Быстрое сохранение создано",
            "Quick-save success toast text is incorrect.");
    }

    private static bool IsVnReadyForSnapshot(SaveData snapshot)
    {
        VNDialogueController controller = VNDialogueController.Instance;
        GameState state = GameState.Instance;
        return SceneManager.GetActiveScene().name == SaveManager.GameplaySceneName
            && controller != null
            && state != null
            && !SaveManager.Instance.HasPendingSceneRestore
            && state.currentSceneId == snapshot.sceneId
            && state.currentLineId == snapshot.lineId
            && state.currentLineIndex == snapshot.lineIndex
            && controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out _)
            && sceneId == snapshot.sceneId
            && lineId == snapshot.lineId
            && lineIndex == snapshot.lineIndex;
    }

    private static Dictionary<string, byte[]> CaptureTypeFiles(SaveManager manager, SaveSlotType type)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index <= SaveManager.SlotCount; index++)
        {
            string json = manager.GetSlotJsonPath(type, index);
            string png = manager.GetSlotPreviewPath(type, index);
            if (File.Exists(json))
            {
                files[json] = File.ReadAllBytes(json);
            }

            if (File.Exists(png))
            {
                files[png] = File.ReadAllBytes(png);
            }
        }

        return files;
    }

    private static string GetTypeFilesSignature(SaveManager manager, SaveSlotType type)
    {
        IReadOnlyDictionary<string, byte[]> files = CaptureTypeFiles(manager, type);
        ulong hash = 14695981039346656037UL;

        foreach (KeyValuePair<string, byte[]> pair in files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            string fileName = Path.GetFileName(pair.Key);
            foreach (char character in fileName)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }

            foreach (byte value in pair.Value)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
        }

        return $"{files.Count}:{hash:X16}";
    }

    private static void RequireFilesEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual,
        string message)
    {
        Require(expected.Count == actual.Count, message + " File count changed.");
        foreach (KeyValuePair<string, byte[]> pair in expected)
        {
            Require(actual.TryGetValue(pair.Key, out byte[] bytes), message + $" Missing '{pair.Key}'.");
            Require(pair.Value.SequenceEqual(bytes), message + $" Contents changed for '{pair.Key}'.");
        }
    }

    private static void RestoreCapturedFiles(IReadOnlyDictionary<string, byte[]> files)
    {
        foreach (KeyValuePair<string, byte[]> pair in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pair.Key));
            File.WriteAllBytes(pair.Key, pair.Value);
        }
    }

    private static bool AllTypesAreEmpty(SaveManager manager)
    {
        return new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick }
            .All(type => manager.GetAllSlots(type).All(slot => !slot.IsOccupied));
    }

    private static bool TryGetReadyMainMenu(out MainMenuController menu, out SaveManager manager)
    {
        menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        manager = SaveManager.Instance;
        return SceneManager.GetActiveScene().name == "MainMenu" && menu != null && manager != null;
    }

    private static void ConfigureTemporaryDirectory(SaveManager manager)
    {
        string directory = SessionState.GetString(DirectoryKey, string.Empty);
        manager.ConfigureSaveDirectoryForTests(directory);
        Require(manager.SaveDirectoryPath == directory, "SaveManager lost the temporary test directory after a scene transition.");
    }

    private static void ConfigureGameViewResolution(Vector2Int resolution)
    {
        try
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", true);
            Type gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type gameViewSizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
            Type gameViewSizeModeType = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);

            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(gameViewSizesType);
            PropertyInfo instanceProperty = singletonType.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object sizesInstance = instanceProperty?.GetValue(null);
            Require(sizesInstance != null, "Unity GameViewSizes singleton is unavailable.");

            MethodInfo getGroup = gameViewSizesType.GetMethod(
                "GetGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(getGroup != null, "Unity GameViewSizes.GetGroup() is unavailable.");
            Type groupTypeEnum = getGroup.GetParameters()[0].ParameterType;
            object standaloneGroup = Enum.Parse(groupTypeEnum, "Standalone");
            object group = getGroup.Invoke(sizesInstance, new[] { standaloneGroup });
            Require(group != null, "Unity Standalone Game View size group is unavailable.");

            MethodInfo getTotalCount = group.GetType().GetMethod(
                "GetTotalCount",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo getGameViewSize = group.GetType().GetMethod(
                "GetGameViewSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo addCustomSize = group.GetType().GetMethod(
                "AddCustomSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(getTotalCount != null && getGameViewSize != null && addCustomSize != null, "Unity Game View size group API is incomplete.");

            PropertyInfo widthProperty = gameViewSizeType.GetProperty(
                "width",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo heightProperty = gameViewSizeType.GetProperty(
                "height",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(widthProperty != null && heightProperty != null, "Unity GameViewSize dimensions are unavailable.");

            int selectedIndex = -1;
            int count = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                if ((int)widthProperty.GetValue(size) == resolution.x
                    && (int)heightProperty.GetValue(size) == resolution.y)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                object fixedResolution = Enum.Parse(gameViewSizeModeType, "FixedResolution");
                ConstructorInfo constructor = gameViewSizeType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { gameViewSizeModeType, typeof(int), typeof(int), typeof(string) },
                    null);
                Require(constructor != null, "Unity GameViewSize constructor is unavailable.");
                object newSize = constructor.Invoke(new object[]
                {
                    fixedResolution,
                    resolution.x,
                    resolution.y,
                    $"How I Fall tabs {resolution.x}x{resolution.y}"
                });
                addCustomSize.Invoke(group, new[] { newSize });
                selectedIndex = (int)getTotalCount.Invoke(group, null) - 1;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selectedSizeProperty = gameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(selectedSizeProperty != null && selectedSizeProperty.CanWrite, "Unity GameView.selectedSizeIndex is unavailable.");
            selectedSizeProperty.SetValue(gameView, selectedIndex);
            gameView.Repaint();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Could not configure Game View to {resolution.x}x{resolution.y}.", exception);
        }

        Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
    }

    private static SaveData ReadSnapshot(string key)
    {
        SaveData data = JsonUtility.FromJson<SaveData>(SessionState.GetString(key, string.Empty));
        Require(data != null, $"Snapshot '{key}' is unavailable.");
        return data;
    }

    private static SaveData Clone(SaveData source)
    {
        return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source));
    }

    private static DateTime ParseUtc(string value)
    {
        Require(DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed),
            $"Invalid createdAtUtc '{value}'.");
        return parsed.UtcDateTime;
    }

    private static void Retry(string timeoutMessage)
    {
        int attempts = SessionState.GetInt(CounterKey, 0) + 1;
        SessionState.SetInt(CounterKey, attempts);
        Require(attempts < 120, timeoutMessage);
        SetDelay(0.25d);
    }

    private static void SetDelay(double seconds)
    {
        SessionState.SetFloat(NextTimeKey, (float)(EditorApplication.timeSinceStartup + seconds));
    }

    private static void Pass(string message)
    {
        Debug.Log("[SAVE BACKEND E2E] PASS: " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Success()
    {
        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        Require(string.IsNullOrEmpty(errors), "Unity Console contained errors:\n" + errors);
        WriteResult("PASS", string.Empty);
        Debug.Log("[SAVE BACKEND E2E] COMPLETE PASS: v3 backlog plus public Auto/Quick save, load, rotation and Continue succeeded.");
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string message)
    {
        WriteResult("FAIL", message);
        Debug.LogError("[SAVE BACKEND E2E] FAILURE: " + message);
        SessionState.SetString(StageKey, "ExitFailure");
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        else
        {
            FinishAndExit(1);
        }
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            || condition.StartsWith("[SAVE BACKEND E2E] FAILURE", StringComparison.Ordinal))
        {
            return;
        }

        if (condition.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stackTrace.Contains("UnityEditor.Search.SearchDatabase"))
        {
            // Unity Search can race its startup index in a freshly imported
            // graphical test copy. It is editor-only and unrelated to Save/Load.
            return;
        }

        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        if (errors.Length < 12000)
        {
            SessionState.SetString(ErrorsKey, errors + condition + "\n");
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string stage = SessionState.GetString(StageKey, string.Empty);
        FinishAndExit(stage == "ExitSuccess" ? 0 : 1);
    }

    private static void FinishAndExit(int exitCode)
    {
        CleanupTestDirectory();
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.Exit(exitCode);
    }

    private static void CleanupTestDirectory()
    {
        string directory = SessionState.GetString(DirectoryKey, string.Empty);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        string fullPath = Path.GetFullPath(directory);
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith("HowIFall_SaveBackendV2E2E_", StringComparison.Ordinal))
        {
            Debug.LogError($"[SAVE BACKEND E2E] Refusing to delete unexpected directory '{fullPath}'.");
            return;
        }

        Directory.Delete(fullPath, true);
        Debug.Log($"[SAVE BACKEND E2E] Temporary save directory removed: '{fullPath}'.");
    }

    private static void WriteResult(string status, string details)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), ResultPath);
        File.WriteAllText(
            path,
            $"status={status}\n"
                + $"timeUtc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}\n"
                + $"temporaryDirectory={SessionState.GetString(DirectoryKey, string.Empty)}\n"
                + $"details={details}\n");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
