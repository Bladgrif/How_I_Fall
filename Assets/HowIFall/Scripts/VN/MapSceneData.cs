using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/Map Scene", fileName = "MapScene")]
public sealed class MapSceneData : ScriptableObject
{
    public string mapId;
    public string displayName;
    public Sprite background;
    public List<MapLocationData> locations = new List<MapLocationData>();

    public bool TryValidate(VNDialogueController dialogueController, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (string.IsNullOrWhiteSpace(mapId) || string.IsNullOrWhiteSpace(displayName) || locations == null || locations.Count == 0)
        { diagnostic = "mapId, displayName, and at least one location are required."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MapLocationData location in locations)
        {
            if (!MapLocationData.TryValidate(location, ids, out diagnostic)) return false;
            if (dialogueController == null || !dialogueController.IsRegisteredDialogueScene(location.destinationScene))
            { diagnostic = "location destinationScene is missing or unregistered."; return false; }
        }
        return true;
    }
}

[Serializable]
public sealed class MapLocationData
{
    public string locationId;
    public string displayName;
    public Rect normalizedRect;
    public List<ChoiceCondition> availabilityConditions = new List<ChoiceCondition>();
    public DialogueSceneData destinationScene;

    public bool IsAvailable(GameState state) => ConditionalChoiceEvaluator.AreConditionsAvailable(availabilityConditions, state, displayName);

    public static bool TryValidate(MapLocationData location, ISet<string> ids, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (location == null || string.IsNullOrWhiteSpace(location.locationId) || string.IsNullOrWhiteSpace(location.displayName))
        { diagnostic = "location id and display name are required."; return false; }
        if (ids == null || !ids.Add(location.locationId)) { diagnostic = "location IDs must be unique."; return false; }
        if (location.normalizedRect.width <= 0f || location.normalizedRect.height <= 0f || location.normalizedRect.xMin < 0f || location.normalizedRect.yMin < 0f || location.normalizedRect.xMax > 1f || location.normalizedRect.yMax > 1f)
        { diagnostic = "normalized rect must stay inside 0..1 and have positive size."; return false; }
        if (location.availabilityConditions == null) { diagnostic = "availability conditions are required."; return false; }
        foreach (ChoiceCondition condition in location.availabilityConditions)
            if (condition == null || !Enum.IsDefined(typeof(ChoiceStateValue), condition.stateValue) || !Enum.IsDefined(typeof(ChoiceComparisonOperator), condition.comparison))
            { diagnostic = "conditions contain an unsupported or null value."; return false; }
        if (location.destinationScene == null) { diagnostic = "destination scene is required."; return false; }
        return true;
    }
}
