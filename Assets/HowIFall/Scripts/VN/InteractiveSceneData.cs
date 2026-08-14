using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/Interactive Scene", fileName = "InteractiveScene")]
public sealed class InteractiveSceneData : ScriptableObject
{
    public string sceneId;
    public string displayName;
    public Sprite background;
    public List<InteractiveHotspotData> hotspots = new List<InteractiveHotspotData>();
    public DialogueSceneData completionNextScene;

    public bool TryValidate(VNDialogueController dialogueController, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(displayName) || hotspots == null || hotspots.Count == 0)
        { diagnostic = "sceneId, displayName, and at least one hotspot are required."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < hotspots.Count; i++)
        {
            if (!InteractiveHotspotData.TryValidate(hotspots[i], ids, out diagnostic)) return false;
            if (hotspots[i].outcome.nextScene != null && (dialogueController == null || !dialogueController.IsRegisteredDialogueScene(hotspots[i].outcome.nextScene)))
            { diagnostic = "hotspot nextScene is missing or unregistered."; return false; }
        }
        foreach (InteractiveHotspotData hotspot in hotspots)
        {
            foreach (string requiredId in hotspot.requiredCompletedHotspotIds)
            {
                if (requiredId == hotspot.hotspotId || !ids.Contains(requiredId))
                { diagnostic = "local prerequisite hotspot is missing or self-referential."; return false; }
            }
        }
        if (completionNextScene != null && (dialogueController == null || !dialogueController.IsRegisteredDialogueScene(completionNextScene)))
        { diagnostic = "completionNextScene is missing or unregistered."; return false; }
        return true;
    }
}

[Serializable]
public sealed class InteractiveHotspotData
{
    public string hotspotId;
    public string displayName;
    public Rect normalizedRect;
    public List<ChoiceCondition> availabilityConditions = new List<ChoiceCondition>();
    public List<string> requiredCompletedHotspotIds = new List<string>();
    public bool oneShot;
    public InteractiveHotspotOutcome outcome = new InteractiveHotspotOutcome();

    public bool IsAvailable(GameState state, ISet<string> completedHotspotIds)
    {
        return ConditionalChoiceEvaluator.AreConditionsAvailable(availabilityConditions, state, displayName)
            && HasRequiredLocalCompletions(completedHotspotIds)
            && (!oneShot || !IsCompleted(completedHotspotIds));
    }

    public bool IsCompleted(ISet<string> completedHotspotIds) => completedHotspotIds != null && completedHotspotIds.Contains(hotspotId);

    public static bool TryValidate(InteractiveHotspotData hotspot, ISet<string> ids, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (hotspot == null || string.IsNullOrWhiteSpace(hotspot.hotspotId) || string.IsNullOrWhiteSpace(hotspot.displayName))
        { diagnostic = "hotspot id and display name are required."; return false; }
        if (ids == null || !ids.Add(hotspot.hotspotId)) { diagnostic = "hotspot IDs must be unique."; return false; }
        if (hotspot.normalizedRect.width <= 0f || hotspot.normalizedRect.height <= 0f || hotspot.normalizedRect.xMin < 0f || hotspot.normalizedRect.yMin < 0f || hotspot.normalizedRect.xMax > 1f || hotspot.normalizedRect.yMax > 1f)
        { diagnostic = "normalized rect must stay inside 0..1 and have positive size."; return false; }
        if (!AreConditionsValid(hotspot.availabilityConditions)) { diagnostic = "conditions contain an unsupported or null value."; return false; }
        if (hotspot.requiredCompletedHotspotIds == null) { diagnostic = "local prerequisite list is required."; return false; }
        var requiredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string requiredId in hotspot.requiredCompletedHotspotIds)
            if (string.IsNullOrWhiteSpace(requiredId) || !requiredIds.Add(requiredId)) { diagnostic = "local prerequisite IDs must be non-empty and unique."; return false; }
        return hotspot.outcome != null && hotspot.outcome.TryValidate(out diagnostic);
    }

    private bool HasRequiredLocalCompletions(ISet<string> completedHotspotIds)
    {
        if (requiredCompletedHotspotIds == null || completedHotspotIds == null) return false;
        foreach (string requiredId in requiredCompletedHotspotIds)
            if (!completedHotspotIds.Contains(requiredId)) return false;
        return true;
    }

    private static bool AreConditionsValid(List<ChoiceCondition> conditions)
    {
        if (conditions == null) return false;
        foreach (ChoiceCondition condition in conditions)
            if (condition == null || !Enum.IsDefined(typeof(ChoiceStateValue), condition.stateValue) || !Enum.IsDefined(typeof(ChoiceComparisonOperator), condition.comparison)) return false;
        return true;
    }
}

public enum InteractiveStateChangeOperation { Set, Add }

[Serializable]
public sealed class InteractiveStateChange
{
    public ChoiceStateValue stateValue;
    public InteractiveStateChangeOperation operation;
    public int value;

    public bool TryApply(GameState state)
    {
        if (state == null || !Enum.IsDefined(typeof(ChoiceStateValue), stateValue) || !Enum.IsDefined(typeof(InteractiveStateChangeOperation), operation) || !state.TryGetChoiceStateValue(stateValue, out int current)) return false;
        int target = operation == InteractiveStateChangeOperation.Set ? value : current + value;
        int delta = target - current;
        switch (stateValue)
        {
            case ChoiceStateValue.Lust: state.ApplyChoiceStateDelta(delta, 0, 0, 0, 0, 0, 0, 0, 0); break;
            case ChoiceStateValue.Romance: state.ApplyChoiceStateDelta(0, delta, 0, 0, 0, 0, 0, 0, 0); break;
            case ChoiceStateValue.Purity: state.ApplyChoiceStateDelta(0, 0, delta, 0, 0, 0, 0, 0, 0); break;
            case ChoiceStateValue.Corruption: state.ApplyChoiceStateDelta(0, 0, 0, delta, 0, 0, 0, 0, 0); break;
            case ChoiceStateValue.SelfControl: state.ApplyChoiceStateDelta(0, 0, 0, 0, delta, 0, 0, 0, 0); break;
            case ChoiceStateValue.Suspicion: state.ApplyChoiceStateDelta(0, 0, 0, 0, 0, delta, 0, 0, 0); break;
            case ChoiceStateValue.TrustMasha: state.ApplyChoiceStateDelta(0, 0, 0, 0, 0, 0, delta, 0, 0); break;
            case ChoiceStateValue.TrustArtem: state.ApplyChoiceStateDelta(0, 0, 0, 0, 0, 0, 0, delta, 0); break;
            case ChoiceStateValue.LeraInterest: state.ApplyChoiceStateDelta(0, 0, 0, 0, 0, 0, 0, 0, delta); break;
            default: return false;
        }
        return true;
    }
}

[Serializable]
public sealed class InteractiveHotspotOutcome
{
    public List<InteractiveStateChange> stateChanges = new List<InteractiveStateChange>();
    [TextArea] public string feedbackText;
    public bool completeScene;
    public DialogueSceneData nextScene;

    public bool TryValidate(out string diagnostic)
    {
        diagnostic = string.Empty;
        if (stateChanges == null) { diagnostic = "state changes are required."; return false; }
        foreach (InteractiveStateChange change in stateChanges)
            if (change == null || !Enum.IsDefined(typeof(ChoiceStateValue), change.stateValue) || !Enum.IsDefined(typeof(InteractiveStateChangeOperation), change.operation)) { diagnostic = "state change is invalid."; return false; }
        return true;
    }

    public bool TryApply(GameState state)
    {
        if (state == null || stateChanges == null) return false;
        foreach (InteractiveStateChange change in stateChanges) if (change == null || !change.TryApply(state)) return false;
        return true;
    }
}
