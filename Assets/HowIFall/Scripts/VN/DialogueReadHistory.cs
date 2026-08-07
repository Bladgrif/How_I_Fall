using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent, save-slot-independent history for dialogue lines that were fully shown.
/// Keys are stable sceneId + lineId pairs; SaveData intentionally does not participate.
/// </summary>
public sealed class DialogueReadHistory
{
    private const string DefaultPlayerPrefsKey = "hif_dialogue_read_history_v1";
    private readonly string playerPrefsKey;
    private readonly HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);

    [Serializable]
    private class StoredKeys
    {
        public List<string> keys = new List<string>();
    }

    public DialogueReadHistory(string playerPrefsKey = DefaultPlayerPrefsKey)
    {
        this.playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey) ? DefaultPlayerPrefsKey : playerPrefsKey;
        Load();
    }

    public static string CreateKey(string sceneId, string lineId)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(lineId))
        {
            return string.Empty;
        }

        return sceneId + "::" + lineId;
    }

    public bool IsSeen(string sceneId, string lineId)
    {
        string key = CreateKey(sceneId, lineId);
        return !string.IsNullOrEmpty(key) && seenKeys.Contains(key);
    }

    public void MarkSeen(string sceneId, string lineId)
    {
        string key = CreateKey(sceneId, lineId);
        if (string.IsNullOrEmpty(key) || !seenKeys.Add(key))
        {
            return;
        }

        Save();
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(playerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            StoredKeys stored = JsonUtility.FromJson<StoredKeys>(json);
            if (stored?.keys == null)
            {
                return;
            }

            foreach (string key in stored.keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    seenKeys.Add(key);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[VN] Could not load dialogue read history: {exception.Message}");
        }
    }

    private void Save()
    {
        PlayerPrefs.SetString(playerPrefsKey, JsonUtility.ToJson(new StoredKeys { keys = new List<string>(seenKeys) }));
        PlayerPrefs.Save();
    }
}
