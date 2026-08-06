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
    public const int SlotCount = 6;
    public const string GameplaySceneName = "VNPrototype";
    public const int PreviewWidth = 384;
    public const int PreviewHeight = 216;

    public static SaveManager Instance { get; private set; }

    [SerializeField] private DialogueSceneRegistry dialogueRegistry;

    private bool pendingSceneRestore;
    private int pendingSlotIndex;
    private string saveDirectoryOverride;

    public string SaveDirectoryPath => string.IsNullOrEmpty(saveDirectoryOverride)
        ? Path.Combine(Application.persistentDataPath, "Saves")
        : saveDirectoryOverride;
    public string AutoSaveDirectoryPath => Path.Combine(SaveDirectoryPath, "Auto");
    public string QuickSaveDirectoryPath => Path.Combine(SaveDirectoryPath, "Quick");
    public bool HasPendingSceneRestore => pendingSceneRestore;
    public int PendingSlotIndex => pendingSlotIndex;

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

#if UNITY_EDITOR
    public void ConfigureSaveDirectoryForTests(string absolutePath)
    {
        saveDirectoryOverride = absolutePath;
        EnsureSaveDirectories();
    }
#endif

    public bool SaveSlot(int slotIndex, Texture2D previewTexture)
    {
        return SaveSlot(SaveSlotType.Manual, slotIndex, previewTexture);
    }

    public bool SaveSlot(SaveSlotType type, int slotIndex, Texture2D previewTexture)
    {
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
        return SaveRotatingSlot(SaveSlotType.Auto, previewTexture);
    }

    public bool SaveQuick(Texture2D previewTexture)
    {
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
        var slots = new List<SaveSlotInfo>(SlotCount);
        for (int slotIndex = 1; slotIndex <= SlotCount; slotIndex++)
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
            data = JsonUtility.FromJson<SaveData>(json);
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

        if (data.version == 1)
        {
            if (type != SaveSlotType.Manual)
            {
                result.Error = $"Save version 1 is accepted only from Manual paths, not {type}.";
                return result;
            }

            // Controlled in-memory compatibility only. The source v1 JSON is never rewritten.
            data.version = SaveData.CurrentVersion;
            data.slotType = SaveSlotType.Manual;
        }
        else if (data.version != SaveData.CurrentVersion)
        {
            result.Error = $"Unsupported save version {data.version}; expected {SaveData.CurrentVersion}.";
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
        if (SceneManager.GetActiveScene().name == GameplaySceneName && dialogueController != null)
        {
            if (!TryApplyInPlace(
                    data,
                    slot.SlotIndex,
                    gameState,
                    dialogueController.RestoreFromGameState,
                    out string restoreError))
            {
                Debug.LogError($"[LOAD] Slot {slot.SlotIndex} was not restored in-place. Previous GameState and dialogue position were preserved. {restoreError}", this);
                return false;
            }

            Debug.Log(
                $"[LOAD] {slot.SlotType} slot {slot.SlotIndex} restored in-place. sceneId='{data.sceneId}', lineId='{data.lineId}', lineIndex={data.lineIndex}, choiceIndex={data.selectedChoiceIndex}.",
                this);
            return true;
        }

        SaveData previousState = CaptureGameState(gameState);
        ApplyGameState(data, gameState);
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
        ApplyGameState(data, gameState);
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

        if (slotIndex < 1 || slotIndex > SlotCount)
        {
            error = $"Slot index {slotIndex} is outside 1..{SlotCount}.";
            return false;
        }

        error = string.Empty;
        return true;
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
