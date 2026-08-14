using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InteractiveHotspotQaLauncher
{
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string ActiveKey = "HowIFall.InteractiveHotspotQa.Active";
    private static double startedAt;

    static InteractiveHotspotQaLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("How I Fall/QA/Interactive Hotspot")]
    public static void OpenInitial() => Open();

    private static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) return;
        InteractiveHotspotTechnicalContentBuilder.Build();
        SessionState.SetBool(ActiveKey, true);
        if (!EditorApplication.isPlaying) { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); EditorApplication.isPlaying = true; return; }
        if (SceneManager.GetActiveScene().path != ScenePath) SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
        QueueOpen();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false)) QueueOpen();
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) StopWaiting();
    }

    private static void QueueOpen() { startedAt = EditorApplication.timeSinceStartup; EditorApplication.update -= TryOpen; EditorApplication.update += TryOpen; }
    private static void TryOpen()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(ActiveKey, false)) { StopWaiting(); return; }
        VNDialogueController dialogue = VNDialogueController.Instance;
        InteractiveSceneData room = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveHotspotTechnicalContentBuilder.InteractiveScenePath);
        if (dialogue != null && dialogue.IsRuntimeReady && room != null)
        {
            if (dialogue.TryStartInteractiveScene(room, out string failure)) { Debug.Log("[INTERACTIVE HOTSPOT QA] TECH Interactive Room is ready."); StopWaiting(); return; }
            if (!string.IsNullOrEmpty(failure) && failure != "another special mode active") { Debug.LogError("[INTERACTIVE HOTSPOT QA] " + failure); StopWaiting(); return; }
        }
        if (EditorApplication.timeSinceStartup - startedAt > 10d) { Debug.LogError("[INTERACTIVE HOTSPOT QA] Timed out while starting the technical room."); StopWaiting(); }
    }

    private static void StopWaiting() { EditorApplication.update -= TryOpen; SessionState.EraseBool(ActiveKey); }
}
