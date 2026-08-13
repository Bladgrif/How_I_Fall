using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Phase5SaveLoadQaLauncher
{
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string PendingKey = "HowIFall.Phase5SaveLoadQa.Pending";
    private const double TimeoutSeconds = 10d;
    private static double startedAt;

    static Phase5SaveLoadQaLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("How I Fall/QA/Phase 5 Save Load")]
    public static void OpenEmbeddedSaveLoadQa()
    {
        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PHASE 5 QA] Wait for the current Play Mode transition and run the QA entry again.");
            return;
        }

        SessionState.SetBool(PendingKey, true);
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
        }

        QueueOpen();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
        {
            QueueOpen();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            StopWaiting();
        }
    }

    private static void QueueOpen()
    {
        startedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= TryOpen;
        EditorApplication.update += TryOpen;
    }

    private static void TryOpen()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(PendingKey, false))
        {
            StopWaiting();
            return;
        }

        VNDialogueController dialogue = VNDialogueController.Instance;
        if (dialogue != null && dialogue.IsRuntimeReady && dialogue.GameMenuController != null)
        {
            if (dialogue.OpenGameMenu())
            {
                ButtonClick(dialogue.GameMenuController.View, VNGameMenuAction.Save);
                if (dialogue.manualSaveLoadPanel != null && dialogue.manualSaveLoadPanel.IsOpen)
                {
                    Debug.Log("[PHASE 5 QA] Embedded Game Menu Save state is ready. Change Game View resolution manually; no GameViewSizes reflection is used.");
                    StopWaiting();
                    return;
                }
            }
        }

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Debug.LogError("[PHASE 5 QA] Timed out while opening the embedded Save/Load state.");
            StopWaiting();
        }
    }

    private static void ButtonClick(VNGameMenuView view, VNGameMenuAction action)
    {
        view?.GetButton(action)?.onClick.Invoke();
    }

    private static void StopWaiting()
    {
        EditorApplication.update -= TryOpen;
        SessionState.SetBool(PendingKey, false);
    }
}
