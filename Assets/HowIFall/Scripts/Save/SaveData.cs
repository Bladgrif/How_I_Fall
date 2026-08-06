using System;

public enum SaveSlotType
{
    Manual = 0,
    Auto = 1,
    Quick = 2
}

[Serializable]
public sealed class SaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public SaveSlotType slotType = SaveSlotType.Manual;
    public int slotIndex;
    public string createdAtUtc;
    public string sceneId;
    public string lineId;
    public int lineIndex;
    public int selectedChoiceIndex = -1;
    public bool choiceResultActive;
    public string pendingNextSceneId;
    public string previewFileName;

    public int lust;
    public int romance;
    public int purity;
    public int corruptionLevel;
    public int selfControl;
    public int suspicion;
    public int trustMasha;
    public int trustArtem;
    public int leraInterest;
}
