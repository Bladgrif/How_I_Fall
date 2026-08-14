using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MapLocationsQaLauncher
{
    private const string ScenePath="Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string ActiveKey="HowIFall.MapLocationsQa.Active";
    private static double startedAt;
    static MapLocationsQaLauncher(){EditorApplication.playModeStateChanged-=OnPlayModeStateChanged;EditorApplication.playModeStateChanged+=OnPlayModeStateChanged;}
    [MenuItem("How I Fall/QA/Map Locations")] public static void OpenInitial(){MapLocationsTechnicalContentBuilder.Build();SessionState.SetBool(ActiveKey,true);if(!EditorApplication.isPlaying){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);EditorApplication.isPlaying=true;}else QueueOpen();}
    private static void OnPlayModeStateChanged(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(ActiveKey,false))QueueOpen();if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.EnteredEditMode)StopWaiting();}
    private static void QueueOpen(){startedAt=EditorApplication.timeSinceStartup;EditorApplication.update-=TryOpen;EditorApplication.update+=TryOpen;}
    private static void TryOpen(){if(!EditorApplication.isPlaying){StopWaiting();return;}var dialogue=VNDialogueController.Instance;var map=AssetDatabase.LoadAssetAtPath<MapSceneData>(MapLocationsTechnicalContentBuilder.MapPath);if(dialogue!=null&&dialogue.IsRuntimeReady&&map!=null){if(dialogue.TryStartMapScene(map,out string failure)){Debug.Log("[MAP QA] TECH Map is ready.");StopWaiting();return;}if(failure!="another special mode active"){Debug.LogError("[MAP QA] "+failure);StopWaiting();return;}}if(EditorApplication.timeSinceStartup-startedAt>10d){Debug.LogError("[MAP QA] Timed out.");StopWaiting();}}
    private static void StopWaiting(){EditorApplication.update-=TryOpen;SessionState.EraseBool(ActiveKey);}
}
