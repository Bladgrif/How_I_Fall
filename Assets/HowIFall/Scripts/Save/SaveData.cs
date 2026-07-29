using System;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public string currentSceneId;
    public int currentLineIndex;
    public string currentLineId;
    public string savedAt;
    public string sceneTitle;
    public string linePreview;
    public int slotIndex;
    public bool isAutoSave;
    public string previewPath;

    public int lust;
    public int romance;
    public int purity;
    public int corruptionLevel;
    public int selfControl;
    public int suspicion;
    public int trustMasha;
    public int trustArtem;
    public int leraInterest;

    public bool TryMigrateToCurrentVersion(out string error)
    {
        error = string.Empty;

        if (version < 0)
        {
            error = $"Invalid save version {version}.";
            return false;
        }

        if (version > CurrentVersion)
        {
            error = $"Save version {version} is newer than supported version {CurrentVersion}.";
            return false;
        }

        // Version 0 is the legacy format created before explicit versioning.
        if (version == 0)
        {
            version = 1;
        }

        // Version 1 stored only a mutable line index. Version 2 adds a stable line ID
        // while retaining the index as a fallback for legacy saves.
        if (version == 1)
        {
            currentLineId = string.Empty;
            version = 2;
        }

        currentSceneId ??= string.Empty;
        currentLineId ??= string.Empty;
        savedAt ??= string.Empty;
        sceneTitle ??= string.Empty;
        linePreview ??= string.Empty;
        previewPath ??= string.Empty;
        currentLineIndex = Math.Max(0, currentLineIndex);

        return true;
    }
}
