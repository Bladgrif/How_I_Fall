using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;

public class SaveSlotInfo
{
    public int SlotIndex;
    public bool IsAutoSave;
    public bool HasSave;
    public string SaveDateText;
    public string PreviewPath;
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SaveFileName = "save_01.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Save()
    {
        Save(string.Empty);
    }

    public void Save(string linePreview)
    {
        SaveData saveData = CreateSaveData(linePreview, 1, false, string.Empty);
        if (saveData == null)
        {
            return;
        }

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"SaveManager: game saved to '{SavePath}'.");
    }

    private SaveData CreateSaveData(string linePreview, int slotIndex, bool isAuto, string previewPath)
    {
        GameState gameState = GameState.Instance;
        if (gameState == null)
        {
            Debug.LogWarning("SaveManager: GameState.Instance is missing. Save skipped.");
            return null;
        }

        return new SaveData
        {
            currentSceneId = gameState.currentSceneId,
            currentLineIndex = gameState.currentLineIndex,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneTitle = string.IsNullOrEmpty(gameState.currentSceneId) ? "Unknown Scene" : gameState.currentSceneId,
            linePreview = NormalizeLinePreview(linePreview),
            slotIndex = slotIndex,
            isAutoSave = isAuto,
            previewPath = previewPath,
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

    private static string NormalizeLinePreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Replace("\n", " ").Replace("\r", " ").Trim();

        const int maxLength = 100;
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized.Substring(0, maxLength) + "...";
    }

    public bool Load()
    {
        if (!HasSave())
        {
            return false;
        }

        SaveData saveData = ReadSaveData(SavePath);

        if (saveData == null)
        {
            return false;
        }

        GameState gameState = GameState.EnsureInstance();
        gameState.currentSceneId = saveData.currentSceneId;
        gameState.currentLineIndex = saveData.currentLineIndex;
        gameState.lust = saveData.lust;
        gameState.romance = saveData.romance;
        gameState.purity = saveData.purity;
        gameState.corruptionLevel = saveData.corruptionLevel;
        gameState.selfControl = saveData.selfControl;
        gameState.suspicion = saveData.suspicion;
        gameState.trustMasha = saveData.trustMasha;
        gameState.trustArtem = saveData.trustArtem;
        gameState.leraInterest = saveData.leraInterest;
        gameState.hasLoadedSave = true;
        return true;
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public bool HasAnySave()
    {
        return HasSave() || GetSlotSavePaths().Any(File.Exists);
    }

    public bool LoadLatestSave()
    {
        string latestPath = GetLatestSavePath();
        if (string.IsNullOrEmpty(latestPath))
        {
            return false;
        }

        return LoadFromPath(latestPath);
    }

    public string GetSavePathForDebug()
    {
        return SavePath;
    }

    public SaveData GetSaveInfo()
    {
        if (!HasSave())
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    public List<SaveSlotInfo> GetManualSaveSlots(int page, int pageSize)
    {
        return GetSaveSlots(page, pageSize, false);
    }

    public List<SaveSlotInfo> GetAutoSaveSlots(int page, int pageSize)
    {
        return GetSaveSlots(page, pageSize, true);
    }

    public bool HasSaveSlot(int slotIndex, bool isAuto)
    {
        return File.Exists(GetSlotSavePath(slotIndex, isAuto));
    }

    public bool LoadSlot(int slotIndex, bool isAuto)
    {
        return LoadFromSlot(slotIndex, isAuto);
    }

    public bool LoadFromSlot(int slotIndex, bool isAuto)
    {
        if (!HasSaveSlot(slotIndex, isAuto))
        {
            return false;
        }

        return LoadFromPath(GetSlotSavePath(slotIndex, isAuto));
    }

    public bool SaveToSlot(int slotIndex, bool isAuto)
    {
        return SaveToSlot(slotIndex, isAuto, string.Empty);
    }

    public bool SaveToSlot(int slotIndex, bool isAuto, string linePreview)
    {
        return SaveToSlot(slotIndex, isAuto, linePreview, null);
    }

    public bool SaveToSlot(int slotIndex, bool isAuto, string linePreview, Texture2D previewTexture)
    {
        Directory.CreateDirectory(SavesDirectory);
        string previewPath = GetSlotPreviewPath(slotIndex, isAuto);
        SaveData saveData = CreateSaveData(linePreview, slotIndex, isAuto, previewPath);
        if (saveData == null)
        {
            return false;
        }

        string savePath = GetSlotSavePath(slotIndex, isAuto);
        File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));

        if (!isAuto && slotIndex == 1)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(saveData, true));
        }

        if (previewTexture != null)
        {
            WritePreviewTexture(previewPath, previewTexture);
        }
        else
        {
            StartCoroutine(CapturePreviewEndOfFrame(previewPath));
        }

        return true;
    }

    public void DeleteSave()
    {
        if (!HasSave())
        {
            return;
        }

        File.Delete(SavePath);
    }

    private List<SaveSlotInfo> GetSaveSlots(int page, int pageSize, bool isAuto)
    {
        int safePage = Mathf.Max(1, page);
        int safePageSize = Mathf.Max(1, pageSize);
        int startSlotIndex = (safePage - 1) * safePageSize + 1;
        var slots = new List<SaveSlotInfo>(safePageSize);

        for (int i = 0; i < safePageSize; i++)
        {
            int slotIndex = startSlotIndex + i;
            var slot = new SaveSlotInfo
            {
                SlotIndex = slotIndex,
                IsAutoSave = isAuto,
                HasSave = HasSaveSlot(slotIndex, isAuto),
                SaveDateText = string.Empty,
                PreviewPath = string.Empty
            };

            if (slot.HasSave)
            {
                SaveData info = ReadSaveData(GetSlotSavePath(slotIndex, isAuto));
                slot.SaveDateText = info == null ? string.Empty : info.savedAt;
                slot.PreviewPath = info == null ? string.Empty : info.previewPath;
            }

            slots.Add(slot);
        }

        return slots;
    }

    private bool LoadFromPath(string path)
    {
        SaveData saveData = ReadSaveData(path);
        if (saveData == null)
        {
            return false;
        }

        ApplySaveData(saveData);
        return true;
    }

    private static SaveData ReadSaveData(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void ApplySaveData(SaveData saveData)
    {
        GameState gameState = GameState.EnsureInstance();
        gameState.currentSceneId = saveData.currentSceneId;
        gameState.currentLineIndex = saveData.currentLineIndex;
        gameState.lust = saveData.lust;
        gameState.romance = saveData.romance;
        gameState.purity = saveData.purity;
        gameState.corruptionLevel = saveData.corruptionLevel;
        gameState.selfControl = saveData.selfControl;
        gameState.suspicion = saveData.suspicion;
        gameState.trustMasha = saveData.trustMasha;
        gameState.trustArtem = saveData.trustArtem;
        gameState.leraInterest = saveData.leraInterest;
        gameState.hasLoadedSave = true;
    }

    private string GetSlotSavePath(int slotIndex, bool isAuto)
    {
        string kind = isAuto ? "auto" : "manual";
        return Path.Combine(SavesDirectory, $"slot_{slotIndex}_{kind}.json");
    }

    private string GetSlotPreviewPath(int slotIndex, bool isAuto)
    {
        string kind = isAuto ? "auto" : "manual";
        return Path.Combine(SavesDirectory, $"slot_{slotIndex}_{kind}_preview.png");
    }

    private IEnumerable<string> GetSlotSavePaths()
    {
        if (!Directory.Exists(SavesDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(SavesDirectory, "slot_*.json");
    }

    private string GetLatestSavePath()
    {
        var paths = new List<string>();
        if (HasSave())
        {
            paths.Add(SavePath);
        }

        paths.AddRange(GetSlotSavePaths());
        return paths
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private IEnumerator CapturePreviewEndOfFrame(string previewPath)
    {
        yield return new WaitForEndOfFrame();

        try
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null)
            {
                yield break;
            }

            WritePreviewTexture(previewPath, texture);
            Destroy(texture);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SaveManager: preview capture failed. {exception.Message}");
        }
    }

    private void WritePreviewTexture(string previewPath, Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        string directory = Path.GetDirectoryName(previewPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(previewPath, texture.EncodeToPNG());
    }
}
