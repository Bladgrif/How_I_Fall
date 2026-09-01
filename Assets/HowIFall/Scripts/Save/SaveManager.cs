using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveSlotInfo
{
    public SaveSlotType SlotType { get; internal set; }
    public int SlotIndex { get; internal set; }
    public bool IsOccupied { get; internal set; }
    public bool IsLoadable { get; internal set; }
    public DateTime CreatedAtUtc { get; internal set; }
    public string DisplayDate { get; internal set; }
    public string DisplayName { get; internal set; }
    public string JsonPath { get; internal set; }
    public string PreviewPath { get; internal set; }
    public string Error { get; internal set; }
    public SaveData Data { get; internal set; }
}

public sealed class SaveManager : MonoBehaviour
{
    public const int SlotsPerPage = 6;
    public const int ManualPageCount = 10;
    public const int ManualSlotCount = SlotsPerPage * ManualPageCount;
    public const int AutoSlotCount = SlotsPerPage;
    public const int QuickSlotCount = SlotsPerPage;

    // Kept for existing six-card UI callers; use the type-specific capacities for save addresses.
    public const int SlotCount = SlotsPerPage;
    public const string GameplaySceneName = "VNPrototype";
    public const int PreviewWidth = 384;
    public const int PreviewHeight = 216;

    public static SaveManager Instance { get; private set; }

    [SerializeField] private DialogueSceneRegistry dialogueRegistry;

    private bool pendingSceneRestore;
    private int pendingSlotIndex;
    private List<DialogueBacklogEntry> pendingBacklogSnapshot;
    private bool pendingBacklogSnapshotAvailable;
    private string saveDirectoryOverride;

    [Serializable]
    private sealed class BacklogEntriesJson
    {
        public List<BacklogEntryData> backlogEntries;
    }

    public string SaveDirectoryPath => string.IsNullOrEmpty(saveDirectoryOverride)
        ? Path.Combine(Application.persistentDataPath, "Saves")
        : saveDirectoryOverride;
    public string AutoSaveDirectoryPath => Path.Combine(SaveDirectoryPath, "Auto");
    public string QuickSaveDirectoryPath => Path.Combine(SaveDirectoryPath, "Quick");
    public bool HasPendingSceneRestore => pendingSceneRestore;
    public int PendingSlotIndex => pendingSlotIndex;

    /// <summary>Narrow PlayMode seam; null in normal runtime so player captures still use ScreenCapture.</summary>
    public static Func<Texture2D> ScreenshotCaptureOverrideForTests { get; set; }

    internal static Texture2D CaptureScreenshotForSave()
    {
        return ScreenshotCaptureOverrideForTests != null
            ? ScreenshotCaptureOverrideForTests()
            : ScreenCapture.CaptureScreenshotAsTexture();
    }

    public static SaveManager EnsureInstance(DialogueSceneRegistry registry = null)
    {
        if (Instance == null)
        {
            SaveManager existing = FindAnyObjectByType<SaveManager>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject managerObject = new GameObject("SaveManager");
                Instance = managerObject.AddComponent<SaveManager>();
            }
        }

        if (registry != null)
        {
            Instance.ConfigureRegistry(registry);
        }

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[SAVE] Duplicate SaveManager component removed from '{gameObject.name}'.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            EnsureSaveDirectories();
            Debug.Log($"[SAVE] SaveManager ready. directory='{SaveDirectoryPath}'.", this);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SAVE] Cannot create save directory '{SaveDirectoryPath}'. {exception.Message}", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ConfigureRegistry(DialogueSceneRegistry registry)
    {
        if (registry != null)
        {
            dialogueRegistry = registry;
        }
    }

    public void ConfigureSaveDirectoryForTests(string absolutePath)
    {
        saveDirectoryOverride = absolutePath;
        EnsureSaveDirectories();
    }

    public bool SaveSlot(int slotIndex, Texture2D previewTexture)
    {
        return SaveSlot(SaveSlotType.Manual, slotIndex, previewTexture);
    }

    public bool SaveSlot(SaveSlotType type, int slotIndex, Texture2D previewTexture)
    {
        if (IsSpecialModeOperationBlocked("SAVE"))
        {
            return false;
        }

        if (IsReplayOperationBlocked("SAVE"))
        {
            return false;
        }

        if (!TryValidateSlotAddress(type, slotIndex, out string addressError))
        {
            Debug.LogError($"[SAVE] {addressError}", this);
            return false;
        }

        if (dialogueRegistry == null)
        {
            Debug.LogError("[SAVE] DialogueSceneRegistry is not configured.", this);
            return false;
        }

        VNDialogueController dialogueController = VNDialogueController.Instance;
        if (dialogueController == null)
        {
            Debug.LogError("[SAVE] Saving is available only while VNDialogueController is active.", this);
            return false;
        }

        if (!dialogueController.TryGetSavePosition(
                out string sceneId,
                out string lineId,
                out int lineIndex,
                out string positionError))
        {
            Debug.LogError($"[SAVE] Cannot obtain the current dialogue position. {positionError}", dialogueController);
            return false;
        }

        DialogueSceneData scene = dialogueRegistry.FindById(sceneId);
        if (scene == null)
        {
            Debug.LogError($"[SAVE] Scene '{sceneId}' is absent from DialogueSceneRegistry.", this);
            return false;
        }

        int resolvedLineIndex = scene.FindLineIndexById(lineId);
        if (resolvedLineIndex < 0)
        {
            Debug.LogError($"[SAVE] Line '{lineId}' is absent from scene '{sceneId}'.", this);
            return false;
        }

        if (previewTexture == null)
        {
            Debug.LogError("[SAVE] Screenshot capture returned no texture. Save was not written.", this);
            return false;
        }

        GameState gameState = GameState.Instance;
        if (gameState == null)
        {
            Debug.LogError("[SAVE] GameState is unavailable.", this);
            return false;
        }

        string previewFileName = GetSlotFileStem(type, slotIndex) + ".png";
        SaveData data = CreateSaveData(
            gameState,
            type,
            slotIndex,
            sceneId,
            lineId,
            resolvedLineIndex,
            previewFileName);
        data.backlogEntries = ToSaveBacklogEntries(dialogueController.CaptureBacklogSnapshot());
        data.backlogSnapshotAvailable = true;

        if (!TryValidateChoiceState(data, scene, out string choiceError))
        {
            Debug.LogError($"[SAVE] Current choice state is invalid. {choiceError}", this);
            return false;
        }

        try
        {
            if (!TryEncodePreviewPng(previewTexture, out byte[] previewBytes, out string previewError))
            {
                Debug.LogError($"[SAVE] Screenshot could not be converted to {PreviewWidth}x{PreviewHeight} PNG. {previewError}", this);
                return false;
            }

            Directory.CreateDirectory(GetSlotDirectoryPath(type));
            string jsonPath = GetSlotJsonPath(type, slotIndex);
            string previewPath = GetSlotPreviewPath(type, slotIndex);
            string jsonTemporaryPath = jsonPath + ".tmp";
            string previewTemporaryPath = previewPath + ".tmp";

            try
            {
                File.WriteAllBytes(previewTemporaryPath, previewBytes);
                File.WriteAllText(
                    jsonTemporaryPath,
                    JsonUtility.ToJson(data, true),
                    new UTF8Encoding(false));

                File.Copy(previewTemporaryPath, previewPath, true);
                File.Copy(jsonTemporaryPath, jsonPath, true);
            }
            finally
            {
                DeleteTemporaryFile(jsonTemporaryPath);
                DeleteTemporaryFile(previewTemporaryPath);
            }

            Debug.Log(
                $"[SAVE] {type} slot {slotIndex} saved. json='{jsonPath}', preview='{previewPath}', sceneId='{sceneId}', lineId='{lineId}', lineIndex={resolvedLineIndex}, choiceIndex={data.selectedChoiceIndex}.",
                this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SAVE] {type} slot {slotIndex} write failed. {exception.Message}", this);
            return false;
        }
    }

    public bool SaveAuto(Texture2D previewTexture)
    {
        if (IsReplayOperationBlocked("AUTO SAVE"))
        {
            return false;
        }

        return SaveRotatingSlot(SaveSlotType.Auto, previewTexture);
    }

    public bool SaveQuick(Texture2D previewTexture)
    {
        if (IsSpecialModeOperationBlocked("QUICK SAVE"))
        {
            return false;
        }

        if (IsReplayOperationBlocked("QUICK SAVE"))
        {
            return false;
        }

        return SaveRotatingSlot(SaveSlotType.Quick, previewTexture);
    }

    private bool SaveRotatingSlot(SaveSlotType type, Texture2D previewTexture)
    {
        int targetSlotIndex = SelectRotationTargetSlot(type);
        if (targetSlotIndex <= 0)
        {
            Debug.LogError($"[SAVE] Could not choose a rotation target for {type}.", this);
            return false;
        }

        Debug.Log($"[SAVE] {type} rotation selected slot {targetSlotIndex}.", this);
        return SaveSlot(type, targetSlotIndex, previewTexture);
    }

    public bool LoadSlot(int slotIndex)
    {
        return LoadSlot(SaveSlotType.Manual, slotIndex);
    }

    public bool LoadSlot(SaveSlotType type, int slotIndex)
    {
        if (IsSpecialModeOperationBlocked("LOAD"))
        {
            return false;
        }

        if (IsReplayOperationBlocked("LOAD"))
        {
            return false;
        }

        SaveSlotInfo slot = ReadSlot(type, slotIndex);
        if (!slot.IsLoadable || slot.Data == null)
        {
            Debug.LogError($"[LOAD] {type} slot {slotIndex} is not loadable. {slot.Error}", this);
            return false;
        }

        return ApplyAndRoute(slot);
    }

    public bool LoadLatest()
    {
        if (IsSpecialModeOperationBlocked("CONTINUE"))
        {
            return false;
        }

        if (IsReplayOperationBlocked("CONTINUE"))
        {
            return false;
        }

        SaveSlotInfo latest = FindLatestLoadableSlot();

        if (latest == null)
        {
            Debug.LogWarning("[LOAD] Continue found no valid Manual, Auto or Quick save.", this);
            return false;
        }

        Debug.Log($"[LOAD] Continue selected {latest.SlotType} slot {latest.SlotIndex} created at {latest.Data.createdAtUtc}.", this);
        return ApplyAndRoute(latest);
    }

    public bool HasAnyValidSave()
    {
        return FindLatestLoadableSlot() != null;
    }

    public bool IsReplayOperationBlocked(string operation = "SAVE/LOAD")
    {
        if (!SceneFlowManager.IsReplayModeActive)
        {
            return false;
        }

        Debug.LogWarning($"[REPLAY] {operation} operation denied while replay mode is active.", this);
        return true;
    }

    /// <summary>Backend defense for any VN BlockingExclusive owner; not chat-specific.</summary>
    public bool IsSpecialModeOperationBlocked(string operation = "SAVE/LOAD")
    {
        VNDialogueController controller = VNDialogueController.Instance;
        if (controller == null || !controller.IsSpecialModeSaveLoadBlocked)
        {
            return false;
        }

        Debug.LogWarning($"[SPECIAL MODE] {operation} operation denied while exclusive VN interaction is active.", this);
        return true;
    }

    public bool DeleteSlot(int slotIndex)
    {
        return DeleteSlot(SaveSlotType.Manual, slotIndex);
    }

    public bool DeleteSlot(SaveSlotType type, int slotIndex)
    {
        if (!TryValidateSlotAddress(type, slotIndex, out string addressError))
        {
            Debug.LogError($"[SAVE DELETE] {addressError}", this);
            return false;
        }

        string jsonPath = GetSlotJsonPath(type, slotIndex);
        string previewPath = GetSlotPreviewPath(type, slotIndex);
        string[] paths =
        {
            jsonPath,
            previewPath,
            jsonPath + ".tmp",
            previewPath + ".tmp"
        };

        bool succeeded = true;
        foreach (string path in paths)
        {
            bool existed = File.Exists(path);
            try
            {
                File.Delete(path);
                if (existed)
                {
                    Debug.Log($"[SAVE DELETE] Deleted '{path}' for {type} slot {slotIndex}.", this);
                }
            }
            catch (Exception exception)
            {
                succeeded = false;
                Debug.LogError($"[SAVE DELETE] Failed to delete '{path}' for {type} slot {slotIndex}. {exception.Message}", this);
            }
        }

        if (!succeeded)
        {
            Debug.LogError($"[SAVE DELETE] {type} slot {slotIndex} was only partially deleted.", this);
            return false;
        }

        Debug.Log($"[SAVE DELETE] {type} slot {slotIndex} deleted successfully.", this);
        return true;
    }

    public SaveSlotInfo GetSlot(int slotIndex)
    {
        return GetSlot(SaveSlotType.Manual, slotIndex);
    }

    public SaveSlotInfo GetSlot(SaveSlotType type, int index)
    {
        return ReadSlot(type, index);
    }

    public IReadOnlyList<SaveSlotInfo> GetAllSlots()
    {
        return GetAllSlots(SaveSlotType.Manual);
    }

    public IReadOnlyList<SaveSlotInfo> GetAllSlots(SaveSlotType type)
    {
        int capacity = GetSlotCapacity(type);
        var slots = new List<SaveSlotInfo>(capacity);
        for (int slotIndex = 1; slotIndex <= capacity; slotIndex++)
        {
            slots.Add(ReadSlot(type, slotIndex));
        }

        return slots;
    }

    public string GetSlotJsonPath(int slotIndex)
    {
        return GetSlotJsonPath(SaveSlotType.Manual, slotIndex);
    }

    public string GetSlotJsonPath(SaveSlotType type, int slotIndex)
    {
        return ResolveSlotPath(type, slotIndex, ".json");
    }

    public string GetSlotPreviewPath(int slotIndex)
    {
        return GetSlotPreviewPath(SaveSlotType.Manual, slotIndex);
    }

    public string GetSlotPreviewPath(SaveSlotType type, int slotIndex)
    {
        return ResolveSlotPath(type, slotIndex, ".png");
    }

    public void CompletePendingSceneRestore()
    {
        ClearPendingLoad();
    }

    public void FailPendingSceneRestoreAndReset()
    {
        ClearPendingLoad();
        GameState.EnsureInstance().ResetState();
    }

    public void ClearPendingLoad()
    {
        pendingSceneRestore = false;
        pendingSlotIndex = 0;
        pendingBacklogSnapshot = null;
        pendingBacklogSnapshotAvailable = false;
    }

    public void GetPendingBacklogRestore(
        out List<DialogueBacklogEntry> snapshot,
        out bool snapshotAvailable)
    {
        snapshotAvailable = pendingSceneRestore && pendingBacklogSnapshotAvailable;
        snapshot = CopyRuntimeBacklogEntries(pendingBacklogSnapshot);
    }

    private SaveSlotInfo ReadSlot(SaveSlotType type, int slotIndex)
    {
        bool validAddress = TryValidateSlotAddress(type, slotIndex, out string addressError);
        string jsonPath = validAddress ? GetSlotJsonPath(type, slotIndex) : string.Empty;
        string previewPath = validAddress ? GetSlotPreviewPath(type, slotIndex) : string.Empty;
        var result = new SaveSlotInfo
        {
            SlotType = type,
            SlotIndex = slotIndex,
            JsonPath = jsonPath,
            PreviewPath = string.Empty,
            DisplayDate = string.Empty,
            DisplayName = string.Empty,
            Error = string.Empty,
            IsOccupied = !string.IsNullOrEmpty(jsonPath)
                && (File.Exists(jsonPath)
                    || File.Exists(previewPath)
                    || File.Exists(jsonPath + ".tmp")
                    || File.Exists(previewPath + ".tmp"))
        };

        if (!validAddress)
        {
            result.Error = addressError;
            return result;
        }

        if (!result.IsOccupied)
        {
            return result;
        }

        if (dialogueRegistry == null)
        {
            result.Error = "DialogueSceneRegistry is not configured.";
            return result;
        }

        SaveData data;
        try
        {
            string json = File.ReadAllText(jsonPath, Encoding.UTF8);
            if (!TryDeserializeSaveData(json, out data, out string readError, out string backlogWarning))
            {
                result.Error = readError;
                return result;
            }

            if (!string.IsNullOrEmpty(backlogWarning))
            {
                Debug.LogWarning($"[LOAD] {type} slot {slotIndex}: {backlogWarning}", this);
            }
        }
        catch (Exception exception)
        {
            result.Error = $"JSON read failed: {exception.Message}";
            return result;
        }

        if (data == null)
        {
            result.Error = "JSON does not contain SaveData.";
            return result;
        }

        int serializedVersion = data.version;
        data.sourceVersion = serializedVersion;

        if (serializedVersion == 1)
        {
            if (type != SaveSlotType.Manual)
            {
                result.Error = $"Save version 1 is accepted only from Manual paths, not {type}.";
                return result;
            }

            // Controlled in-memory compatibility only. The source v1 JSON is never rewritten.
            data.version = SaveData.CurrentVersion;
            data.slotType = SaveSlotType.Manual;
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
        }
        else if (serializedVersion == 2)
        {
            // Existing v2 Manual, Auto and Quick records stay loadable. The source
            // JSON is not rewritten; the next save creates a v3 snapshot.
            data.version = SaveData.CurrentVersion;
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
        }
        else if (serializedVersion != SaveData.CurrentVersion)
        {
            result.Error = $"Unsupported save version {serializedVersion}; expected 1, 2 or {SaveData.CurrentVersion}.";
            return result;
        }

        if (data.slotType != type)
        {
            result.Error = $"Slot type mismatch: path is {type}, JSON contains {data.slotType}.";
            return result;
        }

        if (data.slotIndex != slotIndex)
        {
            result.Error = $"Slot mismatch: file is slot {slotIndex}, JSON contains {data.slotIndex}.";
            return result;
        }

        if (!DateTimeOffset.TryParse(
                data.createdAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset createdAt))
        {
            result.Error = "createdAtUtc is empty or invalid.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(data.sceneId))
        {
            result.Error = "sceneId is empty.";
            return result;
        }

        DialogueSceneData scene = dialogueRegistry.FindById(data.sceneId);
        if (scene == null)
        {
            result.Error = $"Scene '{data.sceneId}' is absent from DialogueSceneRegistry.";
            return result;
        }

        int resolvedLineIndex = scene.FindLineIndexById(data.lineId);
        if (resolvedLineIndex < 0 && string.IsNullOrEmpty(data.lineId))
        {
            bool validFallback = scene.lines != null
                && data.lineIndex >= 0
                && data.lineIndex < scene.lines.Count
                && scene.lines[data.lineIndex] != null;

            if (validFallback)
            {
                resolvedLineIndex = data.lineIndex;
                data.lineId = scene.lines[resolvedLineIndex].lineId ?? string.Empty;
            }
        }

        if (resolvedLineIndex < 0)
        {
            result.Error = $"Line '{data.lineId}' is absent from scene '{data.sceneId}', and fallback index {data.lineIndex} is not allowed.";
            return result;
        }

        data.lineIndex = resolvedLineIndex;

        if (!TryValidateChoiceState(data, scene, out string choiceError))
        {
            result.Error = choiceError;
            return result;
        }

        string expectedPreviewFileName = GetSlotFileStem(type, slotIndex) + ".png";
        if (Path.IsPathRooted(data.previewFileName)
            || !string.Equals(Path.GetFileName(data.previewFileName), data.previewFileName, StringComparison.Ordinal)
            || !string.Equals(data.previewFileName, expectedPreviewFileName, StringComparison.Ordinal))
        {
            result.Error = $"previewFileName must be '{expectedPreviewFileName}'.";
            return result;
        }

        result.Data = data;
        result.CreatedAtUtc = createdAt.UtcDateTime;
        result.DisplayDate = createdAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        result.DisplayName = string.IsNullOrWhiteSpace(scene.displayName)
            ? "Без названия"
            : scene.displayName.Trim();
        result.PreviewPath = File.Exists(previewPath) ? previewPath : string.Empty;
        result.IsLoadable = true;
        return result;
    }

    private bool ApplyAndRoute(SaveSlotInfo slot)
    {
        SaveData data = slot.Data;
        GameState gameState = GameState.EnsureInstance();

        VNDialogueController dialogueController = VNDialogueController.Instance;
        // ReadSlot/FindLatestLoadableSlot already accepted this request. From this
        // point onward stale rollback history must not survive even if restore fails.
        dialogueController?.ClearRollbackHistory();
        if (SceneManager.GetActiveScene().name == GameplaySceneName && dialogueController != null)
        {
            if (!TryApplyInPlace(
                    data,
                    slot.SlotIndex,
                    gameState,
                    () => dialogueController.RestoreFromGameState(data.backlogSnapshotAvailable),
                    out string restoreError))
            {
                dialogueController.ClearRollbackHistory();
                Debug.LogError($"[LOAD] Slot {slot.SlotIndex} was not restored in-place. Previous GameState and dialogue position were preserved. {restoreError}", this);
                return false;
            }

            dialogueController.ClearRollbackHistory();
            Debug.Log(
                $"[LOAD] {slot.SlotType} slot {slot.SlotIndex} restored in-place. sceneId='{data.sceneId}', lineId='{data.lineId}', lineIndex={data.lineIndex}, choiceIndex={data.selectedChoiceIndex}.",
                this);
            return true;
        }

        SaveData previousState = CaptureGameState(gameState);
        ApplyGameState(data, gameState);
        SetPendingBacklogRestore(data);
        BeginPendingSceneRestore(slot.SlotIndex);

        try
        {
            Debug.Log(
                $"[LOAD] {slot.SlotType} slot {slot.SlotIndex} validated. Opening '{GameplaySceneName}' for sceneId='{data.sceneId}', lineId='{data.lineId}', lineIndex={data.lineIndex}.",
                this);
            SceneFlowManager.EnsureInstance().OpenLoadedGame();
            return true;
        }
        catch (Exception exception)
        {
            ApplyGameState(previousState, gameState);
            ClearPendingLoad();
            Debug.LogError($"[LOAD] Could not open '{GameplaySceneName}' for slot {slot.SlotIndex}. Previous GameState was restored. {exception.Message}", this);
            return false;
        }
    }

    private SaveSlotInfo FindLatestLoadableSlot()
    {
        return GetAllSlots(SaveSlotType.Manual)
            .Concat(GetAllSlots(SaveSlotType.Quick))
            .Concat(GetAllSlots(SaveSlotType.Auto))
            .Where(slot => slot.IsLoadable)
            .OrderByDescending(slot => slot.CreatedAtUtc)
            .ThenBy(slot => GetContinueTypePriority(slot.SlotType))
            .ThenBy(slot => slot.SlotIndex)
            .FirstOrDefault();
    }

    private bool TryApplyInPlace(
        SaveData data,
        int slotIndex,
        GameState gameState,
        Func<bool> restoreDialogue,
        out string error)
    {
        error = string.Empty;
        SaveData previousState = CaptureGameState(gameState);
        VNDialogueController dialogueController = VNDialogueController.Instance;
        List<DialogueBacklogEntry> previousBacklog = dialogueController != null
            ? dialogueController.CaptureBacklogSnapshot()
            : null;

        ApplyGameState(data, gameState);
        if (dialogueController != null)
        {
            dialogueController.ReplaceBacklogFromSnapshot(ToRuntimeBacklogEntries(data));
        }

        SetPendingBacklogRestore(data);
        BeginPendingSceneRestore(slotIndex);

        bool restored;
        try
        {
            restored = restoreDialogue != null && restoreDialogue();
        }
        catch (Exception exception)
        {
            restored = false;
            error = $"VNDialogueController threw {exception.GetType().Name}: {exception.Message}";
        }

        if (restored)
        {
            CompletePendingSceneRestore();
            return true;
        }

        ApplyGameState(previousState, gameState);
        if (dialogueController != null)
        {
            dialogueController.ReplaceBacklogFromSnapshot(previousBacklog);
        }

        ClearPendingLoad();
        if (string.IsNullOrEmpty(error))
        {
            error = "VNDialogueController.RestoreFromGameState() returned false.";
        }

        return false;
    }

    private void BeginPendingSceneRestore(int slotIndex)
    {
        pendingSceneRestore = true;
        pendingSlotIndex = slotIndex;
    }

    private void SetPendingBacklogRestore(SaveData data)
    {
        pendingBacklogSnapshotAvailable = data != null && data.backlogSnapshotAvailable;
        pendingBacklogSnapshot = ToRuntimeBacklogEntries(data);
    }

    private bool TryValidateChoiceState(SaveData data, DialogueSceneData scene, out string error)
    {
        error = string.Empty;

        if (!data.choiceResultActive)
        {
            if (data.selectedChoiceIndex != -1)
            {
                error = $"Choice state is inactive, but selectedChoiceIndex is {data.selectedChoiceIndex}; expected -1.";
                return false;
            }

            if (!string.IsNullOrEmpty(data.pendingNextSceneId))
            {
                error = $"Choice state is inactive, but pendingNextSceneId is '{data.pendingNextSceneId}'.";
                return false;
            }

            return true;
        }

        if (scene.choices == null
            || data.selectedChoiceIndex < 0
            || data.selectedChoiceIndex >= scene.choices.Count)
        {
            int choiceCount = scene.choices != null ? scene.choices.Count : 0;
            error = $"selectedChoiceIndex {data.selectedChoiceIndex} is invalid for scene '{scene.sceneId}' with {choiceCount} choice(s).";
            return false;
        }

        DialogueChoice selectedChoice = scene.choices[data.selectedChoiceIndex];
        if (selectedChoice == null)
        {
            error = $"Choice {data.selectedChoiceIndex} in scene '{scene.sceneId}' is null.";
            return false;
        }

        DialogueSceneData configuredNextScene = selectedChoice.nextScene != null
            ? selectedChoice.nextScene
            : scene.defaultNextScene;

        if (configuredNextScene == null)
        {
            if (!string.IsNullOrEmpty(data.pendingNextSceneId))
            {
                error = $"Choice {data.selectedChoiceIndex} in scene '{scene.sceneId}' has no transition target, but pendingNextSceneId is '{data.pendingNextSceneId}'.";
                return false;
            }

            data.pendingNextSceneId = string.Empty;
            return true;
        }

        DialogueSceneData registeredNextScene = dialogueRegistry.FindById(configuredNextScene.sceneId);
        if (registeredNextScene != configuredNextScene)
        {
            error = $"Choice target '{configuredNextScene.sceneId}' is absent from DialogueSceneRegistry.";
            return false;
        }

        if (string.IsNullOrEmpty(data.pendingNextSceneId))
        {
            data.pendingNextSceneId = configuredNextScene.sceneId;
            return true;
        }

        if (!string.Equals(data.pendingNextSceneId, configuredNextScene.sceneId, StringComparison.Ordinal))
        {
            error = $"pendingNextSceneId '{data.pendingNextSceneId}' does not exactly match choice target '{configuredNextScene.sceneId}'.";
            return false;
        }

        return true;
    }

    private static SaveData CaptureGameState(GameState gameState)
    {
        return new SaveData
        {
            sceneId = gameState.currentSceneId ?? string.Empty,
            lineId = gameState.currentLineId ?? string.Empty,
            lineIndex = gameState.currentLineIndex,
            selectedChoiceIndex = gameState.selectedChoiceIndex,
            choiceResultActive = gameState.choiceResultActive,
            pendingNextSceneId = gameState.pendingNextSceneId ?? string.Empty,
            lust = gameState.lust,
            romance = gameState.romance,
            purity = gameState.purity,
            corruptionLevel = gameState.corruptionLevel,
            selfControl = gameState.selfControl,
            suspicion = gameState.suspicion,
            trustMasha = gameState.trustMasha,
            trustArtem = gameState.trustArtem,
            leraInterest = gameState.leraInterest
        };
    }

    private static SaveData CreateSaveData(
        GameState gameState,
        SaveSlotType type,
        int slotIndex,
        string sceneId,
        string lineId,
        int lineIndex,
        string previewFileName)
    {
        SaveData data = CaptureGameState(gameState);
        data.version = SaveData.CurrentVersion;
        data.slotType = type;
        data.slotIndex = slotIndex;
        data.createdAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        data.sceneId = sceneId ?? string.Empty;
        data.lineId = lineId ?? string.Empty;
        data.lineIndex = lineIndex;
        data.previewFileName = previewFileName ?? string.Empty;
        data.backlogEntries = new List<BacklogEntryData>();
        data.backlogSnapshotAvailable = true;
        return data;
    }

    private static void ApplyGameState(SaveData data, GameState gameState)
    {
        gameState.currentSceneId = data.sceneId ?? string.Empty;
        gameState.currentLineId = data.lineId ?? string.Empty;
        gameState.currentLineIndex = data.lineIndex;
        gameState.selectedChoiceIndex = data.selectedChoiceIndex;
        gameState.choiceResultActive = data.choiceResultActive;
        gameState.pendingNextSceneId = data.pendingNextSceneId ?? string.Empty;
        gameState.lust = data.lust;
        gameState.romance = data.romance;
        gameState.purity = data.purity;
        gameState.corruptionLevel = data.corruptionLevel;
        gameState.selfControl = data.selfControl;
        gameState.suspicion = data.suspicion;
        gameState.trustMasha = data.trustMasha;
        gameState.trustArtem = data.trustArtem;
        gameState.leraInterest = data.leraInterest;
    }

    private static bool TryDeserializeSaveData(
        string json,
        out SaveData data,
        out string error,
        out string backlogWarning)
    {
        data = null;
        error = string.Empty;
        backlogWarning = string.Empty;

        if (!TryExtractTopLevelPropertyValue(
                json,
                "backlogEntries",
                out bool propertyFound,
                out string backlogJson,
                out string coreJson,
                out string extractionError))
        {
            error = $"JSON read failed: {extractionError}";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<SaveData>(coreJson);
        }
        catch (Exception exception)
        {
            error = $"JSON read failed: {exception.Message}";
            return false;
        }

        if (data == null)
        {
            error = "JSON does not contain SaveData.";
            return false;
        }

        if (data.version != SaveData.CurrentVersion)
        {
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
            return true;
        }

        if (!propertyFound || string.Equals(backlogJson.Trim(), "null", StringComparison.Ordinal))
        {
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
            backlogWarning = "Optional v3 backlog snapshot is absent or null; using legacy empty-History fallback.";
            return true;
        }

        if (!backlogJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
            backlogWarning = "Optional v3 backlog snapshot is not an array; using legacy empty-History fallback.";
            return true;
        }

        BacklogEntriesJson wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<BacklogEntriesJson>(
                $"{{\"backlogEntries\":{backlogJson}}}");
        }
        catch (Exception exception)
        {
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
            backlogWarning = $"Optional v3 backlog snapshot is malformed and was ignored: {exception.Message}";
            return true;
        }

        if (wrapper == null || wrapper.backlogEntries == null)
        {
            data.backlogEntries = null;
            data.backlogSnapshotAvailable = false;
            backlogWarning = "Optional v3 backlog snapshot has an invalid shape; using legacy empty-History fallback.";
            return true;
        }

        data.backlogEntries = SanitizeBacklogEntries(wrapper.backlogEntries, out string sanitationWarning);
        data.backlogSnapshotAvailable = true;
        backlogWarning = sanitationWarning;
        return true;
    }

    private static List<BacklogEntryData> SanitizeBacklogEntries(
        IEnumerable<BacklogEntryData> source,
        out string warning)
    {
        var sanitized = new List<BacklogEntryData>();
        int skippedOversized = 0;

        if (source != null)
        {
            foreach (BacklogEntryData entry in source)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                {
                    continue;
                }

                if (entry.text.Length > DialogueBacklog.MaximumEntryTextLength)
                {
                    skippedOversized++;
                    continue;
                }

                sanitized.Add(new BacklogEntryData
                {
                    speaker = entry.speaker ?? string.Empty,
                    text = entry.text
                });
            }
        }

        int excessCount = sanitized.Count - DialogueBacklog.DefaultCapacity;
        if (excessCount > 0)
        {
            sanitized.RemoveRange(0, excessCount);
        }

        warning = skippedOversized > 0
            ? $"Skipped {skippedOversized} backlog entry or entries above the {DialogueBacklog.MaximumEntryTextLength}-character defensive limit."
            : string.Empty;
        return sanitized;
    }

    private static List<BacklogEntryData> ToSaveBacklogEntries(
        IEnumerable<DialogueBacklogEntry> source)
    {
        var entries = new List<BacklogEntryData>();
        if (source == null)
        {
            return entries;
        }

        foreach (DialogueBacklogEntry entry in source)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.text))
            {
                continue;
            }

            if (entry.text.Length > DialogueBacklog.MaximumEntryTextLength)
            {
                Debug.LogWarning(
                    $"[SAVE] Backlog entry with {entry.text.Length} characters exceeds the {DialogueBacklog.MaximumEntryTextLength}-character limit and was skipped.");
                continue;
            }

            entries.Add(new BacklogEntryData
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text
            });
        }

        int excessCount = entries.Count - DialogueBacklog.DefaultCapacity;
        if (excessCount > 0)
        {
            entries.RemoveRange(0, excessCount);
        }

        return entries;
    }

    private static List<DialogueBacklogEntry> ToRuntimeBacklogEntries(SaveData data)
    {
        if (data == null || !data.backlogSnapshotAvailable || data.backlogEntries == null)
        {
            return new List<DialogueBacklogEntry>();
        }

        var entries = new List<DialogueBacklogEntry>(data.backlogEntries.Count);
        foreach (BacklogEntryData entry in data.backlogEntries)
        {
            if (entry == null)
            {
                continue;
            }

            entries.Add(new DialogueBacklogEntry
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text
            });
        }

        return entries;
    }

    private static List<DialogueBacklogEntry> CopyRuntimeBacklogEntries(
        IEnumerable<DialogueBacklogEntry> source)
    {
        var copy = new List<DialogueBacklogEntry>();
        if (source == null)
        {
            return copy;
        }

        foreach (DialogueBacklogEntry entry in source)
        {
            if (entry == null)
            {
                continue;
            }

            copy.Add(new DialogueBacklogEntry
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text
            });
        }

        return copy;
    }

    private static bool TryExtractTopLevelPropertyValue(
        string json,
        string propertyName,
        out bool propertyFound,
        out string propertyJson,
        out string jsonWithoutPropertyValue,
        out string error)
    {
        propertyFound = false;
        propertyJson = string.Empty;
        jsonWithoutPropertyValue = json;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON is empty.";
            return false;
        }

        int objectDepth = 0;
        int arrayDepth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char current = json[index];
            if (current == '"')
            {
                if (!TryFindJsonStringEnd(json, index, out int stringEnd))
                {
                    error = "JSON contains an unterminated string.";
                    return false;
                }

                if (objectDepth == 1 && arrayDepth == 0)
                {
                    string token = json.Substring(index + 1, stringEnd - index - 1);
                    int colonIndex = SkipJsonWhitespace(json, stringEnd + 1);
                    if (string.Equals(token, propertyName, StringComparison.Ordinal)
                        && colonIndex < json.Length
                        && json[colonIndex] == ':')
                    {
                        int valueStart = SkipJsonWhitespace(json, colonIndex + 1);
                        if (!TryFindJsonValueEnd(json, valueStart, out int valueEnd))
                        {
                            error = $"Optional property '{propertyName}' has an invalid JSON value.";
                            return false;
                        }

                        propertyFound = true;
                        propertyJson = json.Substring(valueStart, valueEnd - valueStart);
                        jsonWithoutPropertyValue = json.Substring(0, valueStart)
                            + "null"
                            + json.Substring(valueEnd);
                        return true;
                    }
                }

                index = stringEnd;
                continue;
            }

            switch (current)
            {
                case '{':
                    objectDepth++;
                    break;
                case '}':
                    objectDepth--;
                    break;
                case '[':
                    arrayDepth++;
                    break;
                case ']':
                    arrayDepth--;
                    break;
            }
        }

        return true;
    }

    private static bool TryFindJsonValueEnd(string json, int startIndex, out int endIndex)
    {
        endIndex = startIndex;
        if (startIndex < 0 || startIndex >= json.Length)
        {
            return false;
        }

        char first = json[startIndex];
        if (first == '"')
        {
            if (!TryFindJsonStringEnd(json, startIndex, out int stringEnd))
            {
                return false;
            }

            endIndex = stringEnd + 1;
            return true;
        }

        if (first == '{' || first == '[')
        {
            char opening = first;
            char closing = first == '{' ? '}' : ']';
            int depth = 0;
            for (int index = startIndex; index < json.Length; index++)
            {
                if (json[index] == '"')
                {
                    if (!TryFindJsonStringEnd(json, index, out int stringEnd))
                    {
                        return false;
                    }

                    index = stringEnd;
                    continue;
                }

                if (json[index] == opening)
                {
                    depth++;
                }
                else if (json[index] == closing)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endIndex = index + 1;
                        return true;
                    }
                }
            }

            return false;
        }

        int primitiveEnd = startIndex;
        while (primitiveEnd < json.Length
            && json[primitiveEnd] != ','
            && json[primitiveEnd] != '}')
        {
            primitiveEnd++;
        }

        while (primitiveEnd > startIndex && char.IsWhiteSpace(json[primitiveEnd - 1]))
        {
            primitiveEnd--;
        }

        if (primitiveEnd == startIndex)
        {
            return false;
        }

        endIndex = primitiveEnd;
        return true;
    }

    private static bool TryFindJsonStringEnd(string json, int openingQuoteIndex, out int endIndex)
    {
        bool escaped = false;
        for (int index = openingQuoteIndex + 1; index < json.Length; index++)
        {
            char current = json[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                endIndex = index;
                return true;
            }
        }

        endIndex = -1;
        return false;
    }

    private static int SkipJsonWhitespace(string json, int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
        {
            index++;
        }

        return index;
    }

    private static bool TryEncodePreviewPng(Texture2D source, out byte[] pngBytes, out string error)
    {
        pngBytes = null;
        error = string.Empty;

        if (source == null || source.width <= 0 || source.height <= 0)
        {
            error = "Source texture is empty.";
            return false;
        }

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = null;
        Texture2D previewTexture = null;

        try
        {
            renderTexture = RenderTexture.GetTemporary(
                PreviewWidth,
                PreviewHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            float sourceAspect = (float)source.width / source.height;
            float targetAspect = (float)PreviewWidth / PreviewHeight;
            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;

            if (sourceAspect > targetAspect)
            {
                scale.x = targetAspect / sourceAspect;
                offset.x = (1f - scale.x) * 0.5f;
            }
            else if (sourceAspect < targetAspect)
            {
                scale.y = sourceAspect / targetAspect;
                offset.y = (1f - scale.y) * 0.5f;
            }

            Graphics.Blit(source, renderTexture, scale, offset);
            RenderTexture.active = renderTexture;

            previewTexture = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGB24, false);
            previewTexture.ReadPixels(new Rect(0f, 0f, PreviewWidth, PreviewHeight), 0, 0, false);
            previewTexture.Apply(false, false);
            pngBytes = previewTexture.EncodeToPNG();

            if (pngBytes == null || pngBytes.Length == 0)
            {
                error = "Texture2D.EncodeToPNG() returned no data.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (previewTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(previewTexture);
                }
                else
                {
                    DestroyImmediate(previewTexture);
                }
            }

            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }

    private int SelectRotationTargetSlot(SaveSlotType type)
    {
        if (type != SaveSlotType.Auto && type != SaveSlotType.Quick)
        {
            return 0;
        }

        IReadOnlyList<SaveSlotInfo> slots = GetAllSlots(type);
        SaveSlotInfo empty = slots
            .Where(slot => !slot.IsOccupied)
            .OrderBy(slot => slot.SlotIndex)
            .FirstOrDefault();
        if (empty != null)
        {
            return empty.SlotIndex;
        }

        SaveSlotInfo invalid = slots
            .Where(slot => slot.IsOccupied && !slot.IsLoadable)
            .OrderBy(slot => slot.SlotIndex)
            .FirstOrDefault();
        if (invalid != null)
        {
            return invalid.SlotIndex;
        }

        SaveSlotInfo oldest = slots
            .Where(slot => slot.IsLoadable)
            .OrderBy(slot => slot.CreatedAtUtc)
            .ThenBy(slot => slot.SlotIndex)
            .FirstOrDefault();
        return oldest != null ? oldest.SlotIndex : 0;
    }

    private static int GetContinueTypePriority(SaveSlotType type)
    {
        return type switch
        {
            SaveSlotType.Manual => 0,
            SaveSlotType.Quick => 1,
            SaveSlotType.Auto => 2,
            _ => int.MaxValue
        };
    }

    private void EnsureSaveDirectories()
    {
        Directory.CreateDirectory(SaveDirectoryPath);
        Directory.CreateDirectory(AutoSaveDirectoryPath);
        Directory.CreateDirectory(QuickSaveDirectoryPath);
    }

    private string ResolveSlotPath(SaveSlotType type, int slotIndex, string extension)
    {
        if (!TryValidateSlotAddress(type, slotIndex, out string error))
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), error);
        }

        return Path.Combine(GetSlotDirectoryPath(type), GetSlotFileStem(type, slotIndex) + extension);
    }

    private string GetSlotDirectoryPath(SaveSlotType type)
    {
        return type switch
        {
            SaveSlotType.Manual => SaveDirectoryPath,
            SaveSlotType.Auto => AutoSaveDirectoryPath,
            SaveSlotType.Quick => QuickSaveDirectoryPath,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported save slot type.")
        };
    }

    private static bool TryValidateSlotAddress(SaveSlotType type, int slotIndex, out string error)
    {
        if (type != SaveSlotType.Manual && type != SaveSlotType.Auto && type != SaveSlotType.Quick)
        {
            error = $"Save slot type value {(int)type} is unsupported.";
            return false;
        }

        int capacity = GetSlotCapacity(type);
        if (slotIndex < 1 || slotIndex > capacity)
        {
            error = $"Slot index {slotIndex} is outside 1..{capacity}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static int GetSlotCapacity(SaveSlotType type)
    {
        return type switch
        {
            SaveSlotType.Manual => ManualSlotCount,
            SaveSlotType.Auto => AutoSlotCount,
            SaveSlotType.Quick => QuickSlotCount,
            _ => 0
        };
    }

    private static string GetSlotFileStem(SaveSlotType type, int slotIndex)
    {
        return type switch
        {
            SaveSlotType.Manual => $"slot_{slotIndex:D2}",
            SaveSlotType.Auto => $"auto_{slotIndex:D2}",
            SaveSlotType.Quick => $"quick_{slotIndex:D2}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported save slot type.")
        };
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SAVE] Temporary file '{path}' could not be removed. {exception.Message}");
        }
    }
}
