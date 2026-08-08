using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Minimal authored data for one exclusive timed VN interaction.</summary>
[Serializable]
public sealed class TimedNarrativeBeatDefinition
{
    public string promptText;
    public string actionText;
    [Min(0f)] public float durationSeconds = 5f;
    public DialogueSceneData successNextScene;
    public DialogueSceneData timeoutNextScene;
}

/// <summary>
/// Owns only the lifetime and presentation of one exclusive timed narrative beat.
/// It deliberately contains no story, relationship or save-state logic.
/// </summary>
public sealed class TimedNarrativeBeatController : MonoBehaviour
{
    public const float MinimumAuthoredDurationSeconds = 2f;

    public VNDialogueController dialogueController;
    public GameObject rootPanel;
    public TextMeshProUGUI promptText;
    public Button actionButton;
    public TextMeshProUGUI remainingTimeText;
    public Slider progressSlider;

    // Technical-demo-only serialized fixture. It is never started by normal gameplay.
    public TimedNarrativeBeatDefinition demoDefinition = new TimedNarrativeBeatDefinition();

    public TimedNarrativeBeatState State { get; private set; } = TimedNarrativeBeatState.Idle;
    public bool IsRunning => State == TimedNarrativeBeatState.Running;

    private TimedNarrativeBeatDefinition activeDefinition;
    private SpecialModeLease activeLease;
    private float remainingSeconds;

    private void Awake()
    {
        HidePanel();
    }

    private void OnEnable()
    {
        if (actionButton != null)
        {
            actionButton.onClick.AddListener(HandleActionButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(HandleActionButtonClicked);
        }

        CleanupLeaseWithoutRouting("Timed narrative beat disabled");
    }

    private void OnDestroy()
    {
        CleanupLeaseWithoutRouting("Timed narrative beat destroyed");
    }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.unscaledDeltaTime);
        UpdateTimerPresentation();

        if (remainingSeconds <= 0f)
        {
            Resolve(TimedNarrativeBeatOutcome.Timeout);
        }
    }

    /// <summary>Attempts to start one exclusive beat. A rejected start leaves no visible UI or active lease.</summary>
    public bool TryStartBeat(TimedNarrativeBeatDefinition definition)
    {
        if (IsRunning || !IsDefinitionRunnable(definition) || !HasRequiredUiReferences())
        {
            HidePanel();
            return false;
        }

        VNDialogueController controller = dialogueController != null
            ? dialogueController
            : VNDialogueController.Instance;
        if (controller == null
            || !controller.IsRegisteredDialogueScene(definition.successNextScene)
            || !controller.IsRegisteredDialogueScene(definition.timeoutNextScene)
            || !controller.TryEnterSpecialMode(this, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease))
        {
            HidePanel();
            return false;
        }

        dialogueController = controller;
        activeDefinition = definition;
        activeLease = lease;
        remainingSeconds = Mathf.Max(0f, definition.durationSeconds);
        State = TimedNarrativeBeatState.Running;

        promptText.text = definition.promptText;
        actionButton.GetComponentInChildren<TextMeshProUGUI>(true).text = definition.actionText;
        rootPanel.SetActive(true);
        UpdateTimerPresentation();

        // Invalid legacy/runtime data fails safely, while authored persistent data is rejected by validation.
        if (definition.durationSeconds <= 0f)
        {
            Resolve(TimedNarrativeBeatOutcome.Timeout);
        }

        return true;
    }

    /// <summary>Technical-demo entry only; no player-facing menu invokes this method.</summary>
    public bool TryStartTechnicalDemo()
    {
        return TryStartBeat(demoDefinition);
    }

    public bool ResolveFromManualAction()
    {
        return Resolve(TimedNarrativeBeatOutcome.Success);
    }

    private void HandleActionButtonClicked()
    {
        ResolveFromManualAction();
    }

    private bool Resolve(TimedNarrativeBeatOutcome outcome)
    {
        if (!IsRunning)
        {
            return false;
        }

        // This assignment is deliberately first: click and timeout in neighbouring frames cannot both route.
        State = TimedNarrativeBeatState.Resolved;
        TimedNarrativeBeatDefinition definition = activeDefinition;
        SpecialModeLease lease = activeLease;
        activeDefinition = null;
        activeLease = null;

        if (dialogueController != null && lease != null)
        {
            dialogueController.ExitSpecialMode(lease);
        }

        HidePanel();
        DialogueSceneData target = outcome == TimedNarrativeBeatOutcome.Success
            ? definition.successNextScene
            : definition.timeoutNextScene;
        dialogueController?.TryRouteToScene(target);
        return true;
    }

    private bool IsDefinitionRunnable(TimedNarrativeBeatDefinition definition)
    {
        return definition != null
            && !string.IsNullOrWhiteSpace(definition.promptText)
            && !string.IsNullOrWhiteSpace(definition.actionText)
            && definition.successNextScene != null
            && definition.timeoutNextScene != null;
    }

    private bool HasRequiredUiReferences()
    {
        return rootPanel != null
            && promptText != null
            && actionButton != null
            && actionButton.GetComponentInChildren<TextMeshProUGUI>(true) != null
            && remainingTimeText != null
            && progressSlider != null;
    }

    private void UpdateTimerPresentation()
    {
        float duration = activeDefinition != null ? Mathf.Max(activeDefinition.durationSeconds, 0f) : 0f;
        remainingTimeText.text = $"{remainingSeconds:0.0} s";
        progressSlider.value = duration > 0f ? remainingSeconds / duration : 0f;
    }

    private void HidePanel()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    private void CleanupLeaseWithoutRouting(string reason)
    {
        if (activeLease == null)
        {
            return;
        }

        SpecialModeLease lease = activeLease;
        activeLease = null;
        activeDefinition = null;
        if (State == TimedNarrativeBeatState.Running)
        {
            State = TimedNarrativeBeatState.Resolved;
        }

        if (dialogueController != null)
        {
            dialogueController.ExitSpecialMode(lease);
        }

        HidePanel();
    }
}

public enum TimedNarrativeBeatState
{
    Idle,
    Running,
    Resolved
}

public enum TimedNarrativeBeatOutcome
{
    Success,
    Timeout
}
