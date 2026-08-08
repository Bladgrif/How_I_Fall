using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneFlowManager : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string VNPrototypeSceneName = "VNPrototype";

    public static SceneFlowManager Instance { get; private set; }
    private ReplaySession replaySession;

    public static bool IsReplayModeActive => Instance != null && Instance.IsReplayMode;
    public bool IsReplayMode => replaySession != null && replaySession.IsReplayMode;
    public ReplayContext? CurrentReplayContext => IsReplayMode ? replaySession.Context : null;

    public static SceneFlowManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SceneFlowManager existing = FindFirstObjectByType<SceneFlowManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerGo = new GameObject("SceneFlowManager");
        return managerGo.AddComponent<SceneFlowManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[SCENEFLOW] Duplicate SceneFlowManager ignored on '{gameObject.name}'. Existing instance: '{Instance.gameObject.name}'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[SCENEFLOW] SceneFlowManager ready on '{gameObject.name}'.", this);
    }

    public void StartNewGame()
    {
        if (IsReplayMode)
        {
            Debug.LogWarning("[REPLAY] New Game was denied while a replay transaction is active.", this);
            return;
        }

        Debug.Log("[SCENEFLOW] Starting a new game.", this);
        SaveManager.Instance?.ClearPendingLoad();
        GameState.EnsureInstance().ResetState();
        SceneManager.LoadScene(VNPrototypeSceneName, LoadSceneMode.Single);
    }

    public void OpenLoadedGame()
    {
        if (IsReplayMode)
        {
            Debug.LogWarning("[REPLAY] Loading a campaign was denied while a replay transaction is active.", this);
            return;
        }

        Debug.Log("[SCENEFLOW] Opening VNPrototype for validated save data.", this);
        SceneManager.LoadScene(VNPrototypeSceneName, LoadSceneMode.Single);
    }

    public void ReturnToMainMenu()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
    }

    public bool TryStartReplay(
        ReplayEntryDefinition definition,
        IReadOnlyList<ReplayEntryDefinition> knownDefinitions,
        DialogueSceneRegistry dialogueRegistry,
        out string error)
    {
        error = string.Empty;
        if (IsReplayMode)
        {
            error = "Another replay is already active.";
            return false;
        }

        if (!TryValidateReplayDefinition(definition, knownDefinitions, dialogueRegistry, out error))
        {
            Debug.LogWarning($"[REPLAY] Start rejected before state mutation. {error}", this);
            return false;
        }

        if (!ReplayUnlockRegistry.Default.IsUnlocked(definition.replayId))
        {
            error = "Replay is locked.";
            Debug.LogWarning($"[REPLAY] Start rejected before state mutation. {error}", this);
            return false;
        }

        GameState gameState = GameState.EnsureInstance();
        try
        {
            replaySession = new ReplaySession(definition, gameState, VNDialogueController.Instance);
            replaySession.Activate(gameState);
            SaveManager.Instance?.ClearPendingLoad();
            SceneManager.LoadScene(VNPrototypeSceneName, LoadSceneMode.Single);
            Debug.Log($"[REPLAY] Started '{definition.replayId}'.", this);
            return IsReplayMode;
        }
        catch (Exception exception)
        {
            error = $"Replay startup failed: {exception.Message}";
            FailReplay(error);
            return false;
        }
    }

    public bool TryGetReplayStartScene(out DialogueSceneData startScene)
    {
        startScene = IsReplayMode ? replaySession.Definition.startScene : null;
        return startScene != null;
    }

    public void AttachReplayHost(VNDialogueController controller)
    {
        replaySession?.AttachReplayHost(controller);
    }

    public bool IsReplayHost(VNDialogueController controller)
    {
        return IsReplayMode && replaySession.ReplayHost == controller;
    }

    public bool IsReplayLineSeen(string sceneId, string lineId)
    {
        return IsReplayMode && replaySession.IsSeen(sceneId, lineId);
    }

    public void MarkReplayLineSeen(string sceneId, string lineId)
    {
        if (IsReplayMode)
        {
            replaySession.MarkSeen(sceneId, lineId);
        }
    }

    public void EndReplay()
    {
        CleanupReplay(null);
    }

    public void FailReplay(string reason)
    {
        CleanupReplay(string.IsNullOrWhiteSpace(reason) ? "Unknown replay failure." : reason);
    }

    private void CleanupReplay(string failureReason)
    {
        ReplaySession session = replaySession;
        if (session == null || !session.BeginEnding())
        {
            return;
        }

        if (!string.IsNullOrEmpty(failureReason))
        {
            Debug.LogError($"[REPLAY] {failureReason}", this);
        }

        try
        {
            session.ReplayHost?.StopReplayExecutionForCleanup();
            session.RestoreCampaignState(GameState.EnsureInstance());
        }
        catch (Exception exception)
        {
            Debug.LogError($"[REPLAY] Cleanup failed while restoring campaign state. {exception.Message}", this);
        }
        finally
        {
            session.MarkEnded();
            replaySession = null;
        }

        ReturnToMainMenu();
    }

    public static bool TryValidateReplayDefinition(
        ReplayEntryDefinition definition,
        IReadOnlyList<ReplayEntryDefinition> knownDefinitions,
        DialogueSceneRegistry dialogueRegistry,
        out string error)
    {
        if (definition == null)
        {
            error = "Replay definition is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.replayId))
        {
            error = "Replay ID is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.displayName))
        {
            error = $"Replay '{definition.replayId}' has no display name.";
            return false;
        }

        if (definition.startScene == null)
        {
            error = $"Replay '{definition.replayId}' has no start scene.";
            return false;
        }

        if (knownDefinitions == null)
        {
            error = "Replay definition registry is missing.";
            return false;
        }

        bool knownByReference = false;
        var knownIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < knownDefinitions.Count; index++)
        {
            ReplayEntryDefinition candidate = knownDefinitions[index];
            if (candidate == null)
            {
                error = "Replay definition registry contains a null entry.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(candidate.replayId))
            {
                error = "Replay definition registry contains an empty ID.";
                return false;
            }

            if (!knownIds.Add(candidate.replayId))
            {
                error = $"Replay ID '{candidate.replayId}' is duplicated.";
                return false;
            }

            knownByReference |= candidate == definition;
        }

        if (!knownByReference)
        {
            error = $"Replay asset '{definition.replayId}' is not registered.";
            return false;
        }

        return TryValidateReplayGraph(definition.startScene, dialogueRegistry, out error);
    }

    private static bool TryValidateReplayGraph(
        DialogueSceneData startScene,
        DialogueSceneRegistry registry,
        out string error)
    {
        if (registry == null || registry.scenes == null)
        {
            error = "Dialogue scene registry is missing.";
            return false;
        }

        var pending = new Stack<DialogueSceneData>();
        var visited = new HashSet<DialogueSceneData>();
        pending.Push(startScene);
        while (pending.Count > 0)
        {
            DialogueSceneData scene = pending.Pop();
            if (!visited.Add(scene))
            {
                continue;
            }

            if (!registry.scenes.Contains(scene) || string.IsNullOrWhiteSpace(scene.sceneId)
                || scene.lines == null || scene.lines.Count == 0)
            {
                error = $"Replay scene '{(scene != null ? scene.name : "<null>")}' is invalid or unregistered.";
                return false;
            }

            if (scene.defaultNextScene != null)
            {
                pending.Push(scene.defaultNextScene);
            }

            if (scene.choices == null)
            {
                continue;
            }

            foreach (DialogueChoice choice in scene.choices)
            {
                if (choice != null && choice.nextScene != null)
                {
                    pending.Push(choice.nextScene);
                }
            }
        }

        error = string.Empty;
        return true;
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}
