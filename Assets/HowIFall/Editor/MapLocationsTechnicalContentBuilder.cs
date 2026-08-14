using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Creates isolated TECH DEMO ONLY / NOT CANON map data and neutral destinations.</summary>
public static class MapLocationsTechnicalContentBuilder
{
    public const string MapPath = "Assets/HowIFall/Resources/MapLocations/TechnicalMap.asset";
    public const string DormPath = "Assets/HowIFall/Data/Dialogues/map_test_dorm.asset";
    public const string UniversityPath = "Assets/HowIFall/Data/Dialogues/map_test_university.asset";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";

    public static void Build()
    {
        EnsureFolder("Assets/HowIFall", "Resources"); EnsureFolder("Assets/HowIFall/Resources", "MapLocations");
        DialogueSceneData dorm = GetOrCreateScene(DormPath, "map_test_dorm", "TEST:Dorm destination");
        DialogueSceneData university = GetOrCreateScene(UniversityPath, "map_test_university", "TEST:University destination");
        MapSceneData map = AssetDatabase.LoadAssetAtPath<MapSceneData>(MapPath);
        if (map == null) { map = ScriptableObject.CreateInstance<MapSceneData>(); AssetDatabase.CreateAsset(map, MapPath); }
        map.mapId = "technical_map"; map.displayName = "TECH DEMO ONLY / NOT CANON"; map.background = null;
        map.locations = new List<MapLocationData>
        {
            Location("test_dorm", "TEST:Dorm", new Rect(.12f,.22f,.22f,.24f), dorm),
            Location("test_university", "TEST:University", new Rect(.56f,.46f,.25f,.25f), university),
            new MapLocationData { locationId="test_cafe", displayName="TEST:Cafe", normalizedRect=new Rect(.36f,.16f,.18f,.18f), destinationScene=dorm, availabilityConditions=new List<ChoiceCondition> { new ChoiceCondition { stateValue=ChoiceStateValue.Suspicion, comparison=ChoiceComparisonOperator.GreaterOrEqual, threshold=999 } } }
        };
        EditorUtility.SetDirty(map); Register(dorm); Register(university); AssetDatabase.SaveAssets();
    }
    private static MapLocationData Location(string id, string label, Rect rect, DialogueSceneData destination) => new MapLocationData { locationId=id, displayName=label, normalizedRect=rect, destinationScene=destination, availabilityConditions=new List<ChoiceCondition>() };
    private static DialogueSceneData GetOrCreateScene(string path, string id, string text)
    {
        DialogueSceneData scene=AssetDatabase.LoadAssetAtPath<DialogueSceneData>(path); if(scene==null){scene=ScriptableObject.CreateInstance<DialogueSceneData>();AssetDatabase.CreateAsset(scene,path);}
        scene.sceneId=id; scene.displayName="TECH DEMO ONLY / NOT CANON"; scene.backgroundMusic=null; scene.stopMusicOnStart=false; scene.defaultNextScene=null; scene.choices=new List<DialogueChoice>(); scene.lines=new List<DialogueLine>{new DialogueLine{lineId=id+"_line",speaker=string.Empty,text="TECH DEMO ONLY / NOT CANON — "+text}}; EditorUtility.SetDirty(scene); return scene;
    }
    private static void Register(DialogueSceneData scene) { var registry=AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath); if(registry==null)throw new System.InvalidOperationException("Map fixture requires DialogueSceneRegistry."); if(!registry.scenes.Contains(scene)){registry.scenes.Add(scene);EditorUtility.SetDirty(registry);} }
    private static void EnsureFolder(string parent,string child){if(!AssetDatabase.IsValidFolder(parent+"/"+child))AssetDatabase.CreateFolder(parent,child);}
}
