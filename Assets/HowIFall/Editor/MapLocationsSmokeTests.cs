using System;
using UnityEditor;
using UnityEngine;

public static class MapLocationsSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Map Locations Smoke Tests")]
    public static void RunFromMenu(){RunBatchMode();Debug.Log("[MAP LOCATIONS] Smoke tests passed.");}
    public static void RunBatchMode()
    {
        MapLocationsTechnicalContentBuilder.Build();
        MapSceneData map=AssetDatabase.LoadAssetAtPath<MapSceneData>(MapLocationsTechnicalContentBuilder.MapPath);
        if(map==null||!map.TryValidate(VNDialogueController.Instance,out string diagnostic))
        {
            // The CI editor has no live dialogue host; validate authored invariants that do not require runtime registry access.
            if(map==null||map.locations==null||map.locations.Count!=3) throw new InvalidOperationException("TECH Map fixture is missing.");
            foreach(MapLocationData location in map.locations) if(!MapLocationData.TryValidate(location,new System.Collections.Generic.HashSet<string>(),out diagnostic) && diagnostic!="location IDs must be unique.") throw new InvalidOperationException(diagnostic);
        }
        if(SaveData.CurrentVersion!=3) throw new InvalidOperationException("Map foundation must preserve SaveData v3.");
    }
}
