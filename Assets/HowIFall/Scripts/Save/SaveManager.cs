using System;
using System.Collections.Generic;
using System.IO;
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
        GameState gameState = GameState.Instance;

        if (gameState == null)
        {
            Debug.LogWarning("SaveManager: GameState.Instance is missing. Save skipped.");
            return;
        }

        SaveData saveData = new SaveData
        {
            currentSceneId = gameState.currentSceneId,
            currentLineIndex = gameState.currentLineIndex,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneTitle = string.IsNullOrEmpty(gameState.currentSceneId) ? "Unknown Scene" : gameState.currentSceneId,
            linePreview = NormalizeLinePreview(linePreview),
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

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"SaveManager: game saved to '{SavePath}'.");
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

        string json = File.ReadAllText(SavePath);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

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
        return !isAuto && slotIndex == 1 && HasSave();
    }

    public bool LoadSlot(int slotIndex, bool isAuto)
    {
        if (!HasSaveSlot(slotIndex, isAuto))
        {
            return false;
        }

        return Load();
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
                SaveData info = GetSaveInfo();
                slot.SaveDateText = info == null ? string.Empty : info.savedAt;
            }

            slots.Add(slot);
        }

        return slots;
    }
}
