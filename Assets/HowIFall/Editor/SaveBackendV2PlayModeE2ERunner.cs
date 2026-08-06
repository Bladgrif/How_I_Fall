using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SaveBackendV2PlayModeE2ERunner
{
    private const string ActiveKey = "HowIFall.SaveBackendV2E2E.Active";
    private const string StageKey = "HowIFall.SaveBackendV2E2E.Stage";
    private const string NextTimeKey = "HowIFall.SaveBackendV2E2E.NextTime";
    private const string CounterKey = "HowIFall.SaveBackendV2E2E.Counter";
    private const string ErrorsKey = "HowIFall.SaveBackendV2E2E.Errors";
    private const string DirectoryKey = "HowIFall.SaveBackendV2E2E.Directory";
    private const string AutoSnapshotKey = "HowIFall.SaveBackendV2E2E.AutoSnapshot";
    private const string QuickSnapshotKey = "HowIFall.SaveBackendV2E2E.QuickSnapshot";
    private const string AutoNewestSnapshotKey = "HowIFall.SaveBackendV2E2E.AutoNewestSnapshot";
    private const string ResultPath = "save_backend_v2_playmode_result.txt";
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";

    static SaveBackendV2PlayModeE2ERunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    [MenuItem("How I Fall/Tests/Run Save Backend v2 Play Mode E2E")]
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
        SessionState.SetString(AutoNewestSnapshotKey, string.Empty);
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

        SessionState.SetString(StageKey, "WaitCoreScenario");
        SessionState.SetInt(CounterKey, 0);
        controller.StartCoroutine(RunSafely(RunCoreScenario(controller)));
    }

    private static IEnumerator RunCoreScenario(VNDialogueController controller)
    {
        Texture2D screenshot = null;
        try
        {
            yield return RunSafely(AdvanceToChoice(controller));
            Require(controller.choiceMashaButton != null, "The first real VN choice button is missing.");
            controller.choiceMashaButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.15f);

            GameState state = GameState.Instance;
            Require(state != null && state.choiceResultActive && state.selectedChoiceIndex == 0, "The real VN choice was not stored before backend saves.");

            yield return new WaitForEndOfFrame();
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            Require(screenshot != null && screenshot.width > 0 && screenshot.height > 0, "ScreenCapture returned no real VN screenshot.");

            SaveManager manager = SaveManager.Instance;
            Require(manager != null, "SaveManager disappeared before backend saves.");

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

            yield return RunSafely(MutateDialogueAndState(controller, autoSnapshot));
            Require(manager.LoadSlot(SaveSlotType.Auto, 1), "Public LoadSlot(Auto, 1) returned false.");
            VerifyRestoredSnapshot(autoSnapshot, "public Auto LoadSlot");
            Pass("Public LoadSlot restored Auto GameState, choice and VN position");

            yield return RunSafely(MutateDialogueAndState(controller, quickSnapshot));
            Require(manager.LoadSlot(SaveSlotType.Quick, 1), "Public LoadSlot(Quick, 1) returned false.");
            VerifyRestoredSnapshot(quickSnapshot, "public Quick LoadSlot");
            Pass("Public LoadSlot restored Quick GameState, choice and VN position");

            UnityEngine.Object.Destroy(screenshot);
            screenshot = null;
            SessionState.SetString(StageKey, "WaitMainQuickContinue");
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

    private static IEnumerator MutateDialogueAndState(VNDialogueController controller, SaveData snapshot)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            controller.AdvanceDialogue();
            yield return new WaitForSecondsRealtime(0.05f);
        }

        GameState state = GameState.Instance;
        state.suspicion = snapshot.suspicion + 700;
        state.trustMasha = snapshot.trustMasha + 700;
        state.selectedChoiceIndex = -1;
        state.choiceResultActive = false;
        state.pendingNextSceneId = string.Empty;

        bool positionChanged = state.currentSceneId != snapshot.sceneId
            || state.currentLineId != snapshot.lineId
            || state.currentLineIndex != snapshot.lineIndex;
        Require(positionChanged, "The real VN dialogue did not move away from the saved position before LoadSlot.");
        Require(state.suspicion != snapshot.suspicion && state.trustMasha != snapshot.trustMasha, "GameState was not changed before LoadSlot.");
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
        SaveData auto = ReadSnapshot(AutoSnapshotKey);
        Require(ParseUtc(quick.createdAtUtc) > ParseUtc(auto.createdAtUtc), "Quick is not the newest save before Continue.");
        Require(manager.HasAnyValidSave(), "Continue found no valid backend saves.");

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
            GameState.Instance.suspicion = 501;
            GameState.Instance.trustMasha = 601;
            Require(manager.SaveAuto(screenshot), "Public SaveAuto failed while making Auto newest for Continue.");

            SaveSlotInfo newestAuto = VerifySlot(manager, SaveSlotType.Auto, 2);
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
        Pass("Continue selected the newest Auto save");
        Success();
    }

    private static SaveSlotInfo VerifySlot(SaveManager manager, SaveSlotType type, int index)
    {
        SaveSlotInfo slot = manager.GetSlot(type, index);
        Require(slot.IsLoadable && slot.Data != null, $"{type} slot {index} is not loadable: {slot.Error}");
        Require(File.Exists(slot.JsonPath), $"{type} slot {index} JSON is missing: '{slot.JsonPath}'.");
        Require(File.Exists(slot.PreviewPath), $"{type} slot {index} PNG is missing: '{slot.PreviewPath}'.");
        Require(slot.Data.version == 2, $"{type} slot {index} version is {slot.Data.version}; expected 2.");
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
        Debug.Log("[SAVE BACKEND E2E] COMPLETE PASS: public Auto/Quick save, load, rotation and Continue succeeded.");
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
