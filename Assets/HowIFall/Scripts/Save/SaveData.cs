using System;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string currentSceneId;
    public int currentLineIndex;
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

        currentSceneId ??= string.Empty;
        savedAt ??= string.Empty;
        sceneTitle ??= string.Empty;
        linePreview ??= string.Empty;
        previewPath ??= string.Empty;
        currentLineIndex = Math.Max(0, currentLineIndex);

        return true;
    }
}
