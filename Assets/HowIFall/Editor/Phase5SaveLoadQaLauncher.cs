using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Phase5SaveLoadQaLauncher
{
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string PendingKey = "HowIFall.PlayerUiQa.GameplayVariant";
    private const double TimeoutSeconds = 10d;
    private static double startedAt;

    static Phase5SaveLoadQaLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private enum GameplayVariant
    {
        GameMenu,
        EmbeddedSave,
        EmbeddedLoad,
        Preferences,
        History,
        CharacterHub
    }

    [MenuItem("How I Fall/QA/Player UI/Game Menu")]
    public static void OpenGameMenuQa()
    {
        OpenGameplayQa(GameplayVariant.GameMenu);
    }

    [MenuItem("How I Fall/QA/Player UI/Character Hub")]
    public static void OpenCharacterHubQa()
    {
        OpenGameplayQa(GameplayVariant.CharacterHub);
    }

    [MenuItem("How I Fall/QA/Phase 6 Player Shell")]
    public static void OpenEmbeddedSaveLoadQa()
    {
        OpenGameplayQa(GameplayVariant.EmbeddedSave);
    }

    [MenuItem("How I Fall/QA/Player UI/Game Menu - Save")]
    public static void OpenEmbeddedSaveQa()
    {
        OpenGameplayQa(GameplayVariant.EmbeddedSave);
    }

    [MenuItem("How I Fall/QA/Player UI/Game Menu - Load")]
    public static void OpenEmbeddedLoadQa()
    {
        OpenGameplayQa(GameplayVariant.EmbeddedLoad);
    }

    [MenuItem("How I Fall/QA/Player UI/Gameplay Preferences")]
    public static void OpenGameplayPreferencesQa()
    {
        OpenGameplayQa(GameplayVariant.Preferences);
    }

    [MenuItem("How I Fall/QA/Player UI/Backlog")]
    public static void OpenBacklogQa()
    {
        OpenGameplayQa(GameplayVariant.History);
    }

    private static void OpenGameplayQa(GameplayVariant variant)
    {
        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PHASE 6 QA] Wait for the current Play Mode transition and run the QA entry again.");
            return;
        }

        SessionState.SetInt(PendingKey, (int)variant);
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
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(PendingKey, -1) >= 0)
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
        if (!EditorApplication.isPlaying || SessionState.GetInt(PendingKey, -1) < 0)
        {
            StopWaiting();
            return;
        }

        VNDialogueController dialogue = VNDialogueController.Instance;
        if (dialogue != null && dialogue.IsRuntimeReady && dialogue.GameMenuController != null)
        {
            GameplayVariant variant = (GameplayVariant)SessionState.GetInt(PendingKey, (int)GameplayVariant.GameMenu);
            if (variant == GameplayVariant.CharacterHub && dialogue.OpenCharacterHub())
            {
                Debug.Log("[PLAYER UI QA] Character Hub is ready.");
                StopWaiting();
                return;
            }

            if (variant != GameplayVariant.CharacterHub && dialogue.OpenGameMenu())
            {
                if (variant == GameplayVariant.EmbeddedSave || variant == GameplayVariant.EmbeddedLoad)
                {
                    ButtonClick(dialogue.GameMenuController.View, variant == GameplayVariant.EmbeddedSave
                        ? VNGameMenuAction.Save
                        : VNGameMenuAction.Load);
                    if (dialogue.manualSaveLoadPanel == null || !dialogue.manualSaveLoadPanel.IsOpen)
                    {
                        return;
                    }
                }
                else if (variant == GameplayVariant.Preferences)
                {
                    ButtonClick(dialogue.GameMenuController.View, VNGameMenuAction.Preferences);
                    if (!dialogue.IsPreferencesOpen)
                    {
                        return;
                    }
                }
                else if (variant == GameplayVariant.History)
                {
                    ButtonClick(dialogue.GameMenuController.View, VNGameMenuAction.History);
                    if (dialogue.backlogPanel == null || !dialogue.backlogPanel.activeSelf)
                    {
                        return;
                    }
                }

                Debug.Log("[PLAYER UI QA] Gameplay state is ready: " + variant + ".");
                StopWaiting();
                return;
            }
        }

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            Debug.LogError("[PHASE 6 QA] Timed out while opening the embedded Save/Load state.");
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
        SessionState.EraseInt(PendingKey);
    }
}
