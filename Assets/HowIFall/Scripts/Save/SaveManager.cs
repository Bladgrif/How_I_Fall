using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ManualSaveSlotInfo
{
    public int SlotIndex { get; internal set; }
    public bool IsOccupied { get; internal set; }
    public bool IsLoadable { get; internal set; }
    public DateTime CreatedAtUtc { get; internal set; }
    public string DisplayDate { get; internal set; }
    public string JsonPath { get; internal set; }
    public string PreviewPath { get; internal set; }
    public string Error { get; internal set; }
    public SaveData Data { get; internal set; }
}

public sealed class SaveManager : MonoBehaviour
{
    public const int SlotCount = 6;
    public const string GameplaySceneName = "VNPrototype";

    public static SaveManager Instance { get; private set; }

    [SerializeField] private DialogueSceneRegistry dialogueRegistry;

    private bool pendingSceneRestore;

    public string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, "Saves");

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
            Directory.CreateDirectory(SaveDirectoryPath);
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

    public bool SaveSlot(int slotIndex, Texture2D previewTexture)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SAVE] Slot index {slotIndex} is outside 1..{SlotCount}.", this);
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

        string previewFileName = GetSlotFileStem(slotIndex) + ".png";
        var data = new SaveData
        {
            version = SaveData.CurrentVersion,
            slotIndex = slotIndex,
            createdAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            sceneId = sceneId,
            lineId = lineId,
            lineIndex = resolvedLineIndex,
            selectedChoiceIndex = gameState.selectedChoiceIndex,
            choiceResultActive = gameState.choiceResultActive,
            pendingNextSceneId = gameState.pendingNextSceneId ?? string.Empty,
            previewFileName = previewFileName,
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

        try
        {
            byte[] previewBytes = previewTexture.EncodeToPNG();
            if (previewBytes == null || previewBytes.Length == 0)
            {
                Debug.LogError("[SAVE] Screenshot could not be encoded as PNG.", this);
                return false;
            }

            Directory.CreateDirectory(SaveDirectoryPath);
            string jsonPath = GetSlotJsonPath(slotIndex);
            string previewPath = GetSlotPreviewPath(slotIndex);
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
                $"[SAVE] Slot {slotIndex} saved. json='{jsonPath}', preview='{previewPath}', sceneId='{sceneId}', lineId='{lineId}', lineIndex={resolvedLineIndex}, choiceIndex={data.selectedChoiceIndex}.",
                this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SAVE] Slot {slotIndex} write failed. {exception.Message}", this);
            return false;
        }
    }

    public bool LoadSlot(int slotIndex)
    {
        ManualSaveSlotInfo slot = ReadSlot(slotIndex);
        if (!slot.IsLoadable || slot.Data == null)
        {
            Debug.LogError($"[LOAD] Slot {slotIndex} is not loadable. {slot.Error}", this);
            return false;
        }

        return ApplyAndRoute(slot);
    }

    public bool LoadLatest()
    {
        ManualSaveSlotInfo latest = GetAllSlots()
            .Where(slot => slot.IsLoadable)
            .OrderByDescending(slot => slot.CreatedAtUtc)
            .ThenBy(slot => slot.SlotIndex)
            .FirstOrDefault();

        if (latest == null)
        {
            Debug.LogWarning("[LOAD] Continue found no valid manual save.", this);
            return false;
        }

        Debug.Log($"[LOAD] Continue selected slot {latest.SlotIndex} created at {latest.Data.createdAtUtc}.", this);
        return ApplyAndRoute(latest);
    }

    public bool HasAnyValidSave()
    {
        return GetAllSlots().Any(slot => slot.IsLoadable);
    }

    public ManualSaveSlotInfo GetSlot(int slotIndex)
    {
        return ReadSlot(slotIndex);
    }

    public IReadOnlyList<ManualSaveSlotInfo> GetAllSlots()
    {
        var slots = new List<ManualSaveSlotInfo>(SlotCount);
        for (int slotIndex = 1; slotIndex <= SlotCount; slotIndex++)
        {
            slots.Add(ReadSlot(slotIndex));
        }

        return slots;
    }

    public string GetSlotJsonPath(int slotIndex)
    {
        return Path.Combine(SaveDirectoryPath, GetSlotFileStem(slotIndex) + ".json");
    }

    public string GetSlotPreviewPath(int slotIndex)
    {
        return Path.Combine(SaveDirectoryPath, GetSlotFileStem(slotIndex) + ".png");
    }

    public bool TryConsumePendingSceneRestore()
    {
        if (!pendingSceneRestore)
        {
            return false;
        }

        pendingSceneRestore = false;
        return true;
    }

    public void ClearPendingLoad()
    {
        pendingSceneRestore = false;
    }

    private ManualSaveSlotInfo ReadSlot(int slotIndex)
    {
        string jsonPath = IsValidSlotIndex(slotIndex) ? GetSlotJsonPath(slotIndex) : string.Empty;
        var result = new ManualSaveSlotInfo
        {
            SlotIndex = slotIndex,
            JsonPath = jsonPath,
            PreviewPath = string.Empty,
            DisplayDate = string.Empty,
            Error = string.Empty,
            IsOccupied = !string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath)
        };

        if (!IsValidSlotIndex(slotIndex))
        {
            result.Error = $"Slot index must be between 1 and {SlotCount}.";
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

        if (data.version != SaveData.CurrentVersion)
        {
            result.Error = $"Unsupported save version {data.version}; expected {SaveData.CurrentVersion}.";
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

        string expectedPreviewFileName = GetSlotFileStem(slotIndex) + ".png";
        if (Path.IsPathRooted(data.previewFileName)
            || !string.Equals(Path.GetFileName(data.previewFileName), data.previewFileName, StringComparison.Ordinal)
            || !string.Equals(data.previewFileName, expectedPreviewFileName, StringComparison.Ordinal))
        {
            result.Error = $"previewFileName must be '{expectedPreviewFileName}'.";
            return result;
        }

        string previewPath = GetSlotPreviewPath(slotIndex);
        result.Data = data;
        result.CreatedAtUtc = createdAt.UtcDateTime;
        result.DisplayDate = createdAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        result.PreviewPath = File.Exists(previewPath) ? previewPath : string.Empty;
        result.IsLoadable = true;
        return result;
    }

    private bool ApplyAndRoute(ManualSaveSlotInfo slot)
    {
        SaveData data = slot.Data;
        GameState gameState = GameState.EnsureInstance();

        gameState.currentSceneId = data.sceneId;
        gameState.currentLineId = data.lineId;
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

        pendingSceneRestore = true;
        Debug.Log(
            $"[LOAD] Slot {slot.SlotIndex} validated and applied. sceneId='{data.sceneId}', lineId='{data.lineId}', lineIndex={data.lineIndex}, choiceIndex={data.selectedChoiceIndex}, suspicion={data.suspicion}.",
            this);

        VNDialogueController dialogueController = VNDialogueController.Instance;
        if (SceneManager.GetActiveScene().name == GameplaySceneName && dialogueController != null)
        {
            bool restored = dialogueController.RestoreFromGameState();
            if (restored)
            {
                pendingSceneRestore = false;
            }

            return restored;
        }

        SceneFlowManager.EnsureInstance().OpenLoadedGame();
        return true;
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 1 && slotIndex <= SlotCount;
    }

    private static string GetSlotFileStem(int slotIndex)
    {
        return $"slot_{slotIndex:D2}";
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
