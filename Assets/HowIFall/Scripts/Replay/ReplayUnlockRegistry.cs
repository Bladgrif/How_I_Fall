using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class ReplayUnlockRegistry
{
    public const int CurrentVersion = 1;
    public const string ProfileFileName = "GalleryReplayProfile.json";

    [Serializable]
    private sealed class ProfileData
    {
        public int version = CurrentVersion;
        public List<string> unlockedReplayIds = new List<string>();
    }

    private static ReplayUnlockRegistry defaultInstance;
    private readonly string profilePath;
    private readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);

    public ReplayUnlockRegistry(string profilePathOverride = null)
    {
        profilePath = string.IsNullOrWhiteSpace(profilePathOverride)
            ? Path.Combine(Application.persistentDataPath, ProfileFileName)
            : Path.GetFullPath(profilePathOverride);
        Load();
    }

    public static ReplayUnlockRegistry Default => defaultInstance ??= new ReplayUnlockRegistry();
    public string ProfilePath => profilePath;

    public bool IsUnlocked(string replayId)
    {
        return IsValidId(replayId) && unlockedIds.Contains(replayId);
    }

    public bool Unlock(string replayId)
    {
        if (!IsValidId(replayId))
        {
            Debug.LogWarning("[REPLAY] Unlock rejected an empty replay ID.");
            return false;
        }

        if (!unlockedIds.Add(replayId))
        {
            return false;
        }

        try
        {
            Save();
            return true;
        }
        catch (Exception exception)
        {
            unlockedIds.Remove(replayId);
            Debug.LogWarning($"[REPLAY] Unlock profile write failed closed. {exception.Message}");
            return false;
        }
    }

    public void ResetForTests()
    {
        unlockedIds.Clear();
        try
        {
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[REPLAY] Test profile reset failed. {exception.Message}");
        }
    }

    public static void ResetDefaultInstanceForTests()
    {
        defaultInstance = null;
    }

    public static void ConfigureDefaultProfilePathForTests(string absolutePath)
    {
        defaultInstance = new ReplayUnlockRegistry(absolutePath);
    }

    private static bool IsValidId(string replayId)
    {
        return !string.IsNullOrWhiteSpace(replayId);
    }

    private void Load()
    {
        unlockedIds.Clear();
        if (!File.Exists(profilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(profilePath, Encoding.UTF8);
            ProfileData data = JsonUtility.FromJson<ProfileData>(json);
            if (data == null || data.version != CurrentVersion || data.unlockedReplayIds == null)
            {
                Debug.LogWarning($"[REPLAY] Unlock profile '{profilePath}' has an unsupported or invalid schema. Using an empty registry.");
                return;
            }

            foreach (string replayId in data.unlockedReplayIds)
            {
                if (IsValidId(replayId))
                {
                    unlockedIds.Add(replayId);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[REPLAY] Unlock profile '{profilePath}' is corrupt. Using an empty registry. {exception.Message}");
            unlockedIds.Clear();
        }
    }

    private void Save()
    {
        string directory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ids = new List<string>(unlockedIds);
        ids.Sort(StringComparer.Ordinal);
        var data = new ProfileData { version = CurrentVersion, unlockedReplayIds = ids };
        string temporaryPath = profilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            File.Copy(temporaryPath, profilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
