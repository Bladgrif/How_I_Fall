using System;

[Serializable]
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
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
