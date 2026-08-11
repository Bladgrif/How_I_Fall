using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local, runtime-only owner of one transient authored chat.</summary>
public sealed class ChatController : MonoBehaviour
{
    private const float TerminalReplyPresentationSeconds = 0.45f;
    private const float PhoneWidth = 640f;
    private const float BubbleWidthFraction = 0.72f;
    private const float MinimumBubbleWidth = 260f;

    private static readonly Color ScrimColor = new Color(0.015f, 0.03f, 0.055f, 0.70f);
    private static readonly Color PhoneColor = new Color(0.035f, 0.065f, 0.105f, 0.96f);
    private static readonly Color PhoneBorderColor = new Color(0.20f, 0.66f, 0.86f, 0.58f);
    private static readonly Color HeaderColor = new Color(0.055f, 0.11f, 0.17f, 0.94f);
    private static readonly Color TranscriptColor = new Color(0.02f, 0.045f, 0.075f, 0.76f);
    private static readonly Color MediaViewerScrimColor = new Color(0.005f, 0.012f, 0.025f, 0.86f);
    private static readonly Color IncomingColor = new Color(0.12f, 0.19f, 0.27f, 0.96f);
    private static readonly Color PlayerColor = new Color(0.075f, 0.36f, 0.52f, 0.98f);
    private static readonly Color ReplyColor = new Color(0.08f, 0.30f, 0.43f, 0.98f);
    private static readonly Color MutedTextColor = new Color(0.61f, 0.78f, 0.88f, 1f);
    private static Sprite runtimeSurfaceSprite;
    private static Sprite runtimeCircleSprite;

    private VNDialogueController dialogueController;
    private GameObject root;
    private GameObject phoneShell;
    private GameObject transcriptViewport;
    private GameObject replyArea;
    private GameObject mediaViewerOverlay;
    private Image viewedMediaImage;
    private Sprite viewedMediaSprite;
    private RectTransform transcriptContent;
    private ScrollRect transcriptScroll;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI replyStatusText;
    private Button[] replyButtons;
    private TextMeshProUGUI[] replyLabels;
    private readonly List<GameObject> transcriptViews = new List<GameObject>();
    private readonly List<ChatTranscriptEntry> transcript = new List<ChatTranscriptEntry>();
    private ChatSceneData activeChat;
    private SpecialModeLease activeLease;
    private ChatEntry pendingEntry;
    private GameObject typingIndicatorView;
    private int currentEntryIndex;
    private ChatRuntimeState runtimeState = ChatRuntimeState.Idle;
    private bool completionPending;
    private bool dialogueShellSuppressed;
    private float pacingRemaining;
    private float terminalPresentationRemaining;
    private int completionCount;
    private int returnRouteAttemptCount;
    private int openSfxRequestCount;
    private int incomingSfxRequestCount;
    private int mediaViewerOpenCount;
    private DialogueSceneData lastReturnScene;
    private readonly HashSet<ChatEntry> incomingSfxRevealedEntries = new HashSet<ChatEntry>();

    public bool IsRunning => activeChat != null && activeLease != null && runtimeState != ChatRuntimeState.Resolved;
    public bool IsRuntimeUiActive => root != null && root.activeInHierarchy;
    public bool IsCompletionPending => completionPending;
    public int CompletionCount => completionCount;
    public int ReturnRouteAttemptCount => returnRouteAttemptCount;
    public int OpenSfxRequestCount => openSfxRequestCount;
    public int IncomingSfxRequestCount => incomingSfxRequestCount;
    public DialogueSceneData LastReturnScene => lastReturnScene;
    public ChatRuntimeState RuntimeState => runtimeState;
    public IReadOnlyList<ChatTranscriptEntry> Transcript => transcript;
    public SpecialModeLease ActiveLease => activeLease;
    public int CurrentEntryIndex => currentEntryIndex;
    public bool IsDialogueShellSuppressed => dialogueShellSuppressed;
    public bool HasSinglePhoneRoot => root != null && phoneShell != null && root.transform.Find("PhoneShell") == phoneShell.transform;
    public bool HasDistinctReplyArea => replyArea != null && replyArea.transform.parent == phoneShell.transform;
    public bool HasImageCard => transcriptViews.Exists(view => view != null && view.name == "Image Card");
    public bool IsTypingIndicatorVisible => typingIndicatorView != null && typingIndicatorView.activeInHierarchy;
    public bool HasPendingEntryReveal => runtimeState == ChatRuntimeState.WaitingForEntryReveal && pendingEntry != null;
    public bool HasIncomingLeftPresentation => HasBubbleAlignment("Incoming Bubble", TextAnchor.UpperLeft);
    public bool HasPlayerRightPresentation => HasBubbleAlignment("Player Bubble", TextAnchor.UpperRight);
    public bool IsMediaViewerOpen => mediaViewerOverlay != null && mediaViewerOverlay.activeInHierarchy;
    public Sprite ViewedMediaSprite => viewedMediaSprite;
    public int MediaViewerOpenCount => mediaViewerOpenCount;
    public bool IsLocalPresentationTimerPaused => IsMediaViewerOpen;
    public float EntryPacingRemaining => pacingRemaining;
    public float TerminalPresentationRemaining => terminalPresentationRemaining;
    public bool AreReplyCardsInteractable => !IsMediaViewerOpen && replyButtons != null && replyButtons.Length == 2
        && replyButtons[0] != null && replyButtons[1] != null
        && replyButtons[0].gameObject.activeInHierarchy && replyButtons[1].gameObject.activeInHierarchy
        && replyButtons[0].interactable && replyButtons[1].interactable;

    public static ChatController TryCreateRuntime(VNDialogueController controller)
    {
        return TryCreateRuntime(controller, out ChatController controllerResult, out _) ? controllerResult : null;
    }

    public static bool TryCreateRuntime(VNDialogueController controller, out ChatController controllerResult, out string failureReason)
    {
        controllerResult = null;
        failureReason = string.Empty;
        if (controller == null) { failureReason = "controller not ready"; return false; }
        ChatController existing = controller.GetComponent<ChatController>();
        if (existing != null) { controllerResult = existing; return true; }
        Canvas canvas = controller.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null || !canvas.gameObject.activeInHierarchy) { failureReason = "Canvas/UI unavailable"; return false; }

        ChatController chat = controller.gameObject.AddComponent<ChatController>();
        chat.InitializeRuntime(controller, canvas);
        if (chat.root == null) { failureReason = "Canvas/UI unavailable"; Destroy(chat); return false; }
        controllerResult = chat;
        return true;
    }

    private void InitializeRuntime(VNDialogueController controller, Canvas canvas)
    {
        dialogueController = controller;
        BuildRuntimeUi(canvas);
        root.SetActive(false);
    }

    public bool StartChat(ChatSceneData chat)
    {
        return TryStartChat(chat, out _);
    }

    public bool TryStartChat(ChatSceneData chat, out string failureReason)
    {
        failureReason = string.Empty;
        if (dialogueController == null) { failureReason = "controller not ready"; return false; }
        if (chat == null) { failureReason = "null chat data"; return false; }
        if (SceneFlowManager.IsReplayModeActive) { failureReason = "Replay active"; return false; }
        if (IsRunning) { failureReason = "Chat already active"; return false; }
        if (root == null || headerText == null || transcriptContent == null || replyButtons == null || replyButtons.Length != 2)
        { failureReason = "Canvas/UI unavailable"; return false; }
        if (!chat.TryValidate(dialogueController.sceneRegistry, out string diagnostic))
        {
            failureReason = diagnostic.Contains("returnScene") ? "returnScene invalid/unregistered" : "chat asset invalid: " + diagnostic;
            return false;
        }
        if (!dialogueController.TryEnterSpecialMode(this, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease))
        { failureReason = dialogueController.HasActiveSpecialMode ? "another special mode active" : "lease rejected"; return false; }
        if (!dialogueController.TrySuppressDialogueShell(this))
        {
            dialogueController.ExitSpecialMode(lease);
            failureReason = "dialogue shell unavailable";
            return false;
        }

        activeChat = chat;
        activeLease = lease;
        dialogueShellSuppressed = true;
        currentEntryIndex = 0;
        runtimeState = ChatRuntimeState.Active;
        ClearPendingPacing();
        completionPending = false;
        completionCount = 0;
        returnRouteAttemptCount = 0;
        openSfxRequestCount = 0;
        incomingSfxRequestCount = 0;
        incomingSfxRevealedEntries.Clear();
        mediaViewerOpenCount = 0;
        CloseMediaViewer();
        lastReturnScene = null;
        transcript.Clear();
        ClearTranscriptViews();
        headerText.text = chat.contactDisplayName;
        replyStatusText.text = "Choose a reply";
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        RequestOpenSfx();
        ShowCurrentEntry();
        return true;
    }

    public bool TryChoose(int visibleOptionIndex)
    {
        if (!IsRunning || IsMediaViewerOpen || runtimeState != ChatRuntimeState.Active || completionPending || visibleOptionIndex < 0 || visibleOptionIndex >= 2) return false;
        ChatEntry entry = GetCurrentEntry();
        if (entry == null || entry.kind != ChatEntryKind.Choice || entry.options == null || entry.options.Count != 2) { Complete("Invalid runtime choice entry."); return false; }
        ChatChoiceOption option = entry.options[visibleOptionIndex];
        if (option == null || !ConditionalChoiceEvaluator.AreConditionsAvailable(option.conditions, GameState.Instance, option.text)) return false;

        int target = string.IsNullOrEmpty(option.nextEntryId) ? -1 : activeChat.FindEntryIndex(option.nextEntryId);
        if (!string.IsNullOrEmpty(option.nextEntryId) && target < 0) { Complete("Choice target is invalid."); return false; }

        transcript.Add(new ChatTranscriptEntry(ChatSenderSide.Player, option.text));
        option.effects?.ApplyTo(GameState.Instance);
        RefreshTranscript();
        SetReplyCardsInteractable(false);
        replyStatusText.text = target < 0 ? "Sending reply…" : string.Empty;

        if (target < 0)
        {
            BeginTerminalCompletion();
        }
        else
        {
            currentEntryIndex = target;
            ShowCurrentEntry();
        }

        return true;
    }

    private void BeginTerminalCompletion()
    {
        if (completionPending || runtimeState == ChatRuntimeState.Resolved)
        {
            return;
        }

        completionPending = true;
        runtimeState = ChatRuntimeState.ResolvingTerminalChoice;
        terminalPresentationRemaining = TerminalReplyPresentationSeconds;
        SetReplyCardsInteractable(false);
    }

    private void Update()
    {
        AdvanceEntryPacing(Time.unscaledDeltaTime);
        AdvanceTerminalPresentation(Time.unscaledDeltaTime);
    }

    /// <summary>Advances one authored entry reveal with unscaled time.</summary>
    public void AdvanceEntryPacing(float unscaledDeltaTime)
    {
        if (IsLocalPresentationTimerPaused || runtimeState != ChatRuntimeState.WaitingForEntryReveal || pendingEntry == null || unscaledDeltaTime < 0f)
        {
            return;
        }

        pacingRemaining = Mathf.Max(0f, pacingRemaining - unscaledDeltaTime);
        if (pacingRemaining > 0f)
        {
            return;
        }

        ChatEntry entryToReveal = pendingEntry;
        ClearPendingPacing();
        runtimeState = ChatRuntimeState.Active;
        RevealEntry(entryToReveal);
    }

    /// <summary>Advances only the local terminal-reply presentation with unscaled time.</summary>
    public void AdvanceTerminalPresentation(float unscaledDeltaTime)
    {
        if (IsLocalPresentationTimerPaused || runtimeState != ChatRuntimeState.ResolvingTerminalChoice || unscaledDeltaTime < 0f)
        {
            return;
        }

        terminalPresentationRemaining = Mathf.Max(0f, terminalPresentationRemaining - unscaledDeltaTime);
        if (terminalPresentationRemaining > 0f)
        {
            return;
        }

        Complete(null);
    }

    private void ShowCurrentEntry()
    {
        if (!IsRunning) return;
        ChatEntry entry = GetCurrentEntry();
        if (entry == null) { Complete("Current entry is invalid."); return; }

        HideReplyCards();
        if (!ChatSceneData.TryValidatePacing(entry, out string pacingDiagnostic))
        {
            Complete("Invalid runtime entry pacing: " + pacingDiagnostic);
            return;
        }

        if (entry.pacing == ChatEntryPacing.Immediate)
        {
            RevealEntry(entry);
            return;
        }

        BeginEntryPacing(entry);
    }

    private void BeginEntryPacing(ChatEntry entry)
    {
        if (entry == null || runtimeState != ChatRuntimeState.Active)
        {
            Complete("Unable to begin entry pacing.");
            return;
        }

        pendingEntry = entry;
        pacingRemaining = entry.pacingSeconds;
        runtimeState = ChatRuntimeState.WaitingForEntryReveal;
        if (entry.pacing == ChatEntryPacing.IncomingTyping)
        {
            ShowTypingIndicator();
        }
    }

    private void RevealEntry(ChatEntry entry)
    {
        if (!IsRunning || runtimeState != ChatRuntimeState.Active || entry == null)
        {
            return;
        }

        if (!ChatSceneData.TryValidatePacing(entry, out string pacingDiagnostic))
        {
            Complete("Invalid runtime entry pacing: " + pacingDiagnostic);
            return;
        }

        switch (entry.kind)
        {
            case ChatEntryKind.Text:
                if (string.IsNullOrWhiteSpace(entry.text)) { Complete("Text payload is invalid."); return; }
                transcript.Add(new ChatTranscriptEntry(entry.sender, entry.text));
                RefreshTranscript();
                RequestIncomingSfxOnReveal(entry);
                AdvanceOrderedEntry();
                break;
            case ChatEntryKind.Image:
                if (entry.image == null) { Complete("Image payload is invalid."); return; }
                transcript.Add(new ChatTranscriptEntry(entry.sender, string.Empty, entry.image));
                RefreshTranscript();
                RequestIncomingSfxOnReveal(entry);
                AdvanceOrderedEntry();
                break;
            case ChatEntryKind.Choice:
                ShowChoices(entry);
                break;
            default:
                Complete("Unsupported entry kind.");
                break;
        }
    }

    private void AdvanceOrderedEntry()
    {
        if (currentEntryIndex + 1 >= activeChat.entries.Count) { Complete(null); return; }
        currentEntryIndex++;
        ShowCurrentEntry();
    }

    private void RequestOpenSfx()
    {
        if (activeChat == null || activeChat.openSfx == null)
        {
            return;
        }

        openSfxRequestCount++;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(activeChat.openSfx);
        }
    }

    private void RequestIncomingSfxOnReveal(ChatEntry entry)
    {
        if (activeChat == null || activeChat.incomingSfx == null || entry == null
            || entry.sender != ChatSenderSide.Incoming
            || (entry.kind != ChatEntryKind.Text && entry.kind != ChatEntryKind.Image)
            || !incomingSfxRevealedEntries.Add(entry))
        {
            return;
        }

        incomingSfxRequestCount++;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(activeChat.incomingSfx);
        }
    }

    private void ShowChoices(ChatEntry entry)
    {
        int availableCount = 0;
        for (int i = 0; i < 2; i++)
        {
            ChatChoiceOption option = entry.options[i];
            bool available = option != null && ConditionalChoiceEvaluator.AreConditionsAvailable(option.conditions, GameState.Instance, option.text);
            replyButtons[i].gameObject.SetActive(available);
            replyButtons[i].interactable = available;
            if (available)
            {
                replyLabels[i].text = option.text;
                availableCount++;
            }
        }

        if (availableCount > 0)
        {
            replyStatusText.text = "Choose a reply";
            return;
        }

        int fallback = string.IsNullOrEmpty(entry.fallbackEntryId) ? -1 : activeChat.FindEntryIndex(entry.fallbackEntryId);
        if (fallback >= 0)
        {
            currentEntryIndex = fallback;
            ShowCurrentEntry();
        }
        else
        {
            Complete("All responses were hidden and no valid fallback exists.");
        }
    }

    private void Complete(string diagnostic)
    {
        if (runtimeState == ChatRuntimeState.Resolved) return;

        runtimeState = ChatRuntimeState.Resolved;
        completionPending = false;
        terminalPresentationRemaining = 0f;
        completionCount++;
        if (!string.IsNullOrEmpty(diagnostic)) Debug.LogWarning("[CHAT] " + diagnostic, this);

        DialogueSceneData returnScene = activeChat != null ? activeChat.returnScene : null;
        lastReturnScene = returnScene;
        ReleaseLease();
        ClearTransientState();

        if (returnScene == null || dialogueController == null)
        {
            return;
        }

        returnRouteAttemptCount++;
        try
        {
            if (!dialogueController.TryRouteToScene(returnScene))
            {
                Debug.LogWarning("[CHAT] Return route failed after controlled cleanup.", this);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[CHAT] Return route threw after controlled cleanup: " + exception.Message, this);
        }
    }

    private void ReleaseLease()
    {
        if (activeLease != null && dialogueController != null)
        {
            dialogueController.ExitSpecialMode(activeLease);
        }

        activeLease = null;
    }

    private void ClearTransientState()
    {
        ClearPendingPacing();
        CloseMediaViewer();
        if (root != null)
        {
            root.SetActive(false);
        }

        if (dialogueShellSuppressed && dialogueController != null)
        {
            dialogueController.ReleaseDialogueShellSuppression(this);
        }

        dialogueShellSuppressed = false;
        transcript.Clear();
        ClearTranscriptViews();
        incomingSfxRevealedEntries.Clear();
        activeChat = null;
        currentEntryIndex = 0;
        terminalPresentationRemaining = 0f;
    }

    private ChatEntry GetCurrentEntry()
    {
        return activeChat != null && activeChat.entries != null && currentEntryIndex >= 0 && currentEntryIndex < activeChat.entries.Count ? activeChat.entries[currentEntryIndex] : null;
    }

    private void OnDisable()
    {
        CancelPendingPresentation();
        ReleaseLease();
        ClearTransientState();
    }

    private void OnDestroy()
    {
        CancelPendingPresentation();
        ReleaseLease();
        if (dialogueShellSuppressed && dialogueController != null)
        {
            dialogueController.ReleaseDialogueShellSuppression(this);
        }

        dialogueShellSuppressed = false;
        ClearPendingPacing();
        transcript.Clear();
    }

    private void CancelPendingPresentation()
    {
        completionPending = false;
        terminalPresentationRemaining = 0f;
        ClearPendingPacing();
    }

    private void ClearPendingPacing()
    {
        pendingEntry = null;
        pacingRemaining = 0f;
        HideTypingIndicator();
    }

    /// <summary>Completes an already-pending terminal presentation. Runtime input never calls this directly.</summary>
    public bool TryCompletePendingTerminalPresentation()
    {
        if (!completionPending || runtimeState == ChatRuntimeState.Resolved)
        {
            return false;
        }

        AdvanceTerminalPresentation(terminalPresentationRemaining);
        return true;
    }

#if UNITY_EDITOR
    /// <summary>Exercises the same no-route cleanup path used by OnDisable for an awaiting entry reveal.</summary>
    public bool TryCleanupPendingPacingForTests()
    {
        if (!HasPendingEntryReveal)
        {
            return false;
        }

        CancelPendingPresentation();
        ReleaseLease();
        ClearTransientState();
        return true;
    }

    /// <summary>Verifies that a transcript layout rebuild does not replay entry cues.</summary>
    public void RefreshTranscriptForTests()
    {
        RefreshTranscript();
    }

    public bool TryOpenRevealedMediaForTests(int transcriptIndex)
    {
        return transcriptIndex >= 0 && transcriptIndex < transcript.Count && TryOpenMediaViewer(transcript[transcriptIndex]);
    }

    public bool TryCloseMediaViewerFromScrimForTests()
    {
        if (!IsMediaViewerOpen) return false;
        CloseMediaViewer();
        return true;
    }

    public bool TryCloseMediaViewerFromButtonForTests()
    {
        return TryCloseMediaViewerFromScrimForTests();
    }
#endif

    private void RefreshTranscript()
    {
        Canvas.ForceUpdateCanvases();
        ClearTranscriptViews();
        foreach (ChatTranscriptEntry entry in transcript)
        {
            float bubbleWidth = GetTranscriptRelativeBubbleWidth();
            transcriptViews.Add(entry.image != null ? CreateImageCard(entry, bubbleWidth) : CreateTextBubble(entry, bubbleWidth));
        }

        SizeTranscriptContentToViewport();
        Canvas.ForceUpdateCanvases();
        if (transcriptScroll != null)
        {
            transcriptScroll.verticalNormalizedPosition = 0f;
        }
    }

    private bool HasBubbleAlignment(string viewName, TextAnchor alignment)
    {
        foreach (GameObject view in transcriptViews)
        {
            if (view != null && view.name == viewName)
            {
                HorizontalLayoutGroup layout = view.GetComponent<HorizontalLayoutGroup>();
                if (layout != null && layout.childAlignment == alignment)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SizeTranscriptContentToViewport()
    {
        if (transcriptContent == null || transcriptViewport == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float preferredHeight = LayoutUtility.GetPreferredHeight(transcriptContent);
        float viewportHeight = transcriptViewport.GetComponent<RectTransform>().rect.height;
        transcriptContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(preferredHeight, viewportHeight));
    }

    private void ClearTranscriptViews()
    {
        foreach (GameObject view in transcriptViews)
        {
            if (view == null) continue;
            view.SetActive(false);
            if (Application.isPlaying) Destroy(view); else DestroyImmediate(view);
        }

        transcriptViews.Clear();
    }

    private void ShowTypingIndicator()
    {
        HideTypingIndicator();
        if (transcriptContent == null)
        {
            return;
        }

        float bubbleWidth = GetTranscriptRelativeBubbleWidth();
        typingIndicatorView = CreateLayoutObject(transcriptContent, "Incoming Typing Indicator");
        ConfigureBubbleRow(typingIndicatorView, ChatSenderSide.Incoming);
        GameObject bubble = CreateImageObject(typingIndicatorView.transform, "Typing Bubble", IncomingColor);
        ConfigureBubbleSurface(bubble, bubbleWidth);
        VerticalLayoutGroup layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        bubble.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = CreateTmp(bubble.transform, "Typing Dots", "...", 24f, TextAlignmentOptions.TopLeft, Color.white, FontStyles.Bold);
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = bubbleWidth - 32f;
        SizeTranscriptContentToViewport();
        Canvas.ForceUpdateCanvases();
        if (transcriptScroll != null)
        {
            transcriptScroll.verticalNormalizedPosition = 0f;
        }
    }

    private void HideTypingIndicator()
    {
        if (typingIndicatorView == null)
        {
            return;
        }

        typingIndicatorView.SetActive(false);
        if (Application.isPlaying) Destroy(typingIndicatorView); else DestroyImmediate(typingIndicatorView);
        typingIndicatorView = null;
        SizeTranscriptContentToViewport();
    }

    private GameObject CreateTextBubble(ChatTranscriptEntry entry, float bubbleWidth)
    {
        GameObject row = CreateLayoutObject(transcriptContent, entry.sender == ChatSenderSide.Incoming ? "Incoming Bubble" : "Player Bubble");
        ConfigureBubbleRow(row, entry.sender);

        GameObject bubble = CreateImageObject(row.transform, "Bubble", entry.sender == ChatSenderSide.Incoming ? IncomingColor : PlayerColor);
        ConfigureBubbleSurface(bubble, bubbleWidth);
        VerticalLayoutGroup layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 11, 11);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        bubble.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = CreateTmp(bubble.transform, "Message Text", entry.text, 21f, TextAlignmentOptions.TopLeft, Color.white, FontStyles.Normal);
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = bubbleWidth - 32f;
        return row;
    }

    private GameObject CreateImageCard(ChatTranscriptEntry entry, float bubbleWidth)
    {
        GameObject row = CreateLayoutObject(transcriptContent, "Image Card");
        ConfigureBubbleRow(row, entry.sender);

        GameObject card = CreateImageObject(row.transform, "Media Surface", entry.sender == ChatSenderSide.Incoming ? IncomingColor : PlayerColor);
        ConfigureBubbleSurface(card, bubbleWidth);
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button cardButton = card.AddComponent<Button>();
        cardButton.onClick.AddListener(() => TryOpenMediaViewer(entry));

        GameObject imageObject = CreateImageObject(card.transform, "Technical Image", Color.white);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = entry.image;
        image.preserveAspect = true;
        LayoutElement imageLayout = imageObject.AddComponent<LayoutElement>();
        imageLayout.preferredWidth = bubbleWidth - 24f;
        imageLayout.preferredHeight = GetBoundedImageHeight(entry.image, bubbleWidth - 24f);
        return row;
    }

    private static void ConfigureBubbleRow(GameObject row, ChatSenderSide sender)
    {
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = sender == ChatSenderSide.Incoming ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
        row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement element = row.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
    }

    private float GetTranscriptRelativeBubbleWidth()
    {
        float viewportWidth = transcriptViewport != null ? transcriptViewport.GetComponent<RectTransform>().rect.width : 0f;
        if (viewportWidth <= 0f)
        {
            // Same layout-derived fallback used before the first Canvas rebuild:
            // shell padding + transcript content padding, not text-dependent width.
            viewportWidth = PhoneWidth - 36f;
        }

        float contentWidth = Mathf.Max(0f, viewportWidth - 28f);
        return Mathf.Max(MinimumBubbleWidth, contentWidth * BubbleWidthFraction);
    }

    private static float GetBoundedImageHeight(Sprite image, float imageWidth)
    {
        float aspect = image != null && image.rect.height > 0f ? image.rect.width / image.rect.height : 1.6f;
        float naturalHeight = imageWidth / Mathf.Max(0.1f, aspect);
        return Mathf.Clamp(naturalHeight, 150f, 245f);
    }

    private static void ConfigureBubbleSurface(GameObject bubble, float preferredWidth)
    {
        Image image = bubble.GetComponent<Image>();
        image.sprite = GetUiSprite();
        image.type = Image.Type.Sliced;
        LayoutElement element = bubble.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = 0f;
    }

    private void HideReplyCards()
    {
        if (replyButtons == null) return;
        foreach (Button button in replyButtons)
        {
            if (button != null) button.gameObject.SetActive(false);
        }
    }

    private void SetReplyCardsInteractable(bool interactable)
    {
        if (replyButtons == null) return;
        foreach (Button button in replyButtons)
        {
            if (button != null && button.gameObject.activeSelf) button.interactable = interactable;
        }
    }

    private void BuildRuntimeUi(Canvas canvas)
    {
        root = CreateLayoutObject(canvas.transform, "PhoneRoot");
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsLastSibling();

        GameObject scrim = CreateImageObject(root.transform, "Scrim", ScrimColor);
        Stretch(scrim.GetComponent<RectTransform>());

        phoneShell = CreateImageObject(root.transform, "PhoneShell", PhoneColor);
        RectTransform shellRect = phoneShell.GetComponent<RectTransform>();
        shellRect.anchorMin = new Vector2(0.5f, 0.10f);
        shellRect.anchorMax = new Vector2(0.5f, 0.90f);
        shellRect.pivot = new Vector2(0.5f, 0.5f);
        shellRect.sizeDelta = new Vector2(PhoneWidth, 0f);
        Image shellImage = phoneShell.GetComponent<Image>();
        shellImage.sprite = GetUiSprite();
        shellImage.type = Image.Type.Sliced;

        Outline border = phoneShell.AddComponent<Outline>();
        border.effectColor = PhoneBorderColor;
        border.effectDistance = new Vector2(1f, -1f);
        Shadow shadow = phoneShell.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -8f);

        VerticalLayoutGroup shellLayout = phoneShell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(18, 18, 18, 18);
        shellLayout.spacing = 14f;
        shellLayout.childControlWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childForceExpandHeight = false;

        BuildHeader(phoneShell.transform);
        BuildTranscriptViewport(phoneShell.transform);
        BuildReplyArea(phoneShell.transform);
        BuildMediaViewerOverlay();
    }

    private void BuildMediaViewerOverlay()
    {
        mediaViewerOverlay = CreateLayoutObject(root.transform, "MediaViewerOverlay");
        Stretch(mediaViewerOverlay.GetComponent<RectTransform>());

        GameObject scrim = CreateImageObject(mediaViewerOverlay.transform, "Scrim", MediaViewerScrimColor);
        Stretch(scrim.GetComponent<RectTransform>());
        Button scrimButton = scrim.AddComponent<Button>();
        scrimButton.onClick.AddListener(CloseMediaViewer);

        GameObject imageContainer = CreateLayoutObject(mediaViewerOverlay.transform, "ImageContainer");
        RectTransform containerRect = imageContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.08f, 0.08f);
        containerRect.anchorMax = new Vector2(0.92f, 0.92f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        GameObject imageObject = CreateImageObject(imageContainer.transform, "Image", Color.white);
        viewedMediaImage = imageObject.GetComponent<Image>();
        viewedMediaImage.preserveAspect = true;
        Stretch(imageObject.GetComponent<RectTransform>());
        AspectRatioFitter aspectFitter = imageObject.AddComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        Button imageButton = imageObject.AddComponent<Button>();
        imageButton.onClick.AddListener(() => { });

        GameObject closeObject = CreateImageObject(mediaViewerOverlay.transform, "CloseButton", new Color(0.11f, 0.19f, 0.27f, 0.98f));
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(54f, 54f);
        closeRect.anchoredPosition = new Vector2(-28f, -28f);
        Button closeButton = closeObject.AddComponent<Button>();
        closeButton.onClick.AddListener(CloseMediaViewer);
        TextMeshProUGUI closeLabel = CreateTmp(closeObject.transform, "Close Label", "X", 30f, TextAlignmentOptions.Center, Color.white, FontStyles.Normal);
        Stretch(closeLabel.rectTransform);

        mediaViewerOverlay.SetActive(false);
    }

    private bool TryOpenMediaViewer(ChatTranscriptEntry entry)
    {
        if (!IsRunning || IsMediaViewerOpen || entry == null || entry.image == null || !transcript.Contains(entry))
        {
            return false;
        }

        viewedMediaSprite = entry.image;
        if (viewedMediaImage == null || mediaViewerOverlay == null)
        {
            viewedMediaSprite = null;
            return false;
        }

        viewedMediaImage.sprite = viewedMediaSprite;
        AspectRatioFitter aspectFitter = viewedMediaImage.GetComponent<AspectRatioFitter>();
        if (aspectFitter != null)
        {
            aspectFitter.aspectRatio = viewedMediaSprite.rect.height > 0f
                ? viewedMediaSprite.rect.width / viewedMediaSprite.rect.height
                : 1f;
        }

        mediaViewerOverlay.SetActive(true);
        mediaViewerOverlay.transform.SetAsLastSibling();
        mediaViewerOpenCount++;
        return true;
    }

    private void CloseMediaViewer()
    {
        if (mediaViewerOverlay != null)
        {
            mediaViewerOverlay.SetActive(false);
        }

        if (viewedMediaImage != null)
        {
            viewedMediaImage.sprite = null;
        }

        viewedMediaSprite = null;
    }

    /// <summary>Gives the local viewer precedence over Chat's otherwise blocked Escape input.</summary>
    public bool TryCloseMediaViewerOnEscape()
    {
        if (!IsMediaViewerOpen)
        {
            return false;
        }

        CloseMediaViewer();
        return true;
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreateImageObject(parent, "Header", HeaderColor);
        ConfigureSlicedSurface(header);
        header.AddComponent<LayoutElement>().preferredHeight = 98f;

        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(16, 16, 12, 12);
        headerLayout.spacing = 13f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;

        GameObject avatar = CreateImageObject(header.transform, "Avatar Placeholder", new Color(0.14f, 0.58f, 0.73f, 1f));
        ConfigureCircleSurface(avatar);
        LayoutElement avatarLayout = avatar.AddComponent<LayoutElement>();
        avatarLayout.preferredWidth = 54f;
        avatarLayout.preferredHeight = 54f;
        TextMeshProUGUI avatarLabel = CreateTmp(avatar.transform, "Avatar Initial", "T", 25f, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        Stretch(avatarLabel.rectTransform);

        GameObject identity = CreateLayoutObject(header.transform, "Contact Identity");
        identity.AddComponent<LayoutElement>().flexibleWidth = 1f;
        VerticalLayoutGroup identityLayout = identity.AddComponent<VerticalLayoutGroup>();
        identityLayout.childAlignment = TextAnchor.MiddleLeft;
        identityLayout.childControlWidth = true;
        identityLayout.childControlHeight = true;
        identityLayout.childForceExpandHeight = false;
        identityLayout.spacing = 2f;

        headerText = CreateTmp(identity.transform, "Contact Name", "TEST CONTACT", 26f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
        headerText.gameObject.AddComponent<LayoutElement>().preferredHeight = 35f;
        TextMeshProUGUI tag = CreateTmp(identity.transform, "Technical Demo Tag", "TECH DEMO ONLY / NOT CANON", 12f, TextAlignmentOptions.Left, MutedTextColor, FontStyles.Bold);
        tag.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

        GameObject status = CreateImageObject(header.transform, "Status Indicator", new Color(0.30f, 0.93f, 0.72f, 1f));
        ConfigureCircleSurface(status);
        LayoutElement statusLayout = status.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 10f;
        statusLayout.preferredHeight = 10f;
    }

    private void BuildTranscriptViewport(Transform parent)
    {
        transcriptViewport = CreateImageObject(parent, "TranscriptViewport", TranscriptColor);
        ConfigureSlicedSurface(transcriptViewport);
        LayoutElement viewportElement = transcriptViewport.AddComponent<LayoutElement>();
        viewportElement.minHeight = 220f;
        viewportElement.flexibleHeight = 1f;

        RectTransform viewportRect = transcriptViewport.GetComponent<RectTransform>();
        Mask mask = transcriptViewport.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        transcriptScroll = transcriptViewport.AddComponent<ScrollRect>();
        transcriptScroll.horizontal = false;
        transcriptScroll.vertical = true;
        transcriptScroll.movementType = ScrollRect.MovementType.Clamped;
        transcriptScroll.scrollSensitivity = 28f;
        transcriptScroll.viewport = viewportRect;

        GameObject content = CreateLayoutObject(transcriptViewport.transform, "TranscriptContent");
        transcriptContent = content.GetComponent<RectTransform>();
        transcriptContent.anchorMin = new Vector2(0f, 1f);
        transcriptContent.anchorMax = new Vector2(1f, 1f);
        transcriptContent.pivot = new Vector2(0.5f, 1f);
        transcriptContent.anchoredPosition = Vector2.zero;
        transcriptContent.sizeDelta = Vector2.zero;
        transcriptScroll.content = transcriptContent;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(14, 14, 16, 16);
        contentLayout.spacing = 12f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childAlignment = TextAnchor.LowerCenter;
    }

    private void BuildReplyArea(Transform parent)
    {
        replyArea = CreateImageObject(parent, "ReplyArea", new Color(0.045f, 0.12f, 0.18f, 0.96f));
        ConfigureSlicedSurface(replyArea);
        replyArea.AddComponent<LayoutElement>().preferredHeight = 164f;

        VerticalLayoutGroup layout = replyArea.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 11, 11);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        replyStatusText = CreateTmp(replyArea.transform, "Reply Status", "SELECT A REPLY", 12f, TextAlignmentOptions.Left, MutedTextColor, FontStyles.Bold);
        replyStatusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

        replyButtons = new Button[2];
        replyLabels = new TextMeshProUGUI[2];
        for (int i = 0; i < replyButtons.Length; i++)
        {
            int index = i;
            replyButtons[i] = CreateReplyCard(replyArea.transform, "Reply Card " + (i + 1), out replyLabels[i]);
            replyButtons[i].onClick.AddListener(() => TryChoose(index));
        }
    }

    private static Button CreateReplyCard(Transform parent, string name, out TextMeshProUGUI label)
    {
        GameObject card = CreateImageObject(parent, name, ReplyColor);
        ConfigureSlicedSurface(card);
        LayoutElement cardLayout = card.AddComponent<LayoutElement>();
        cardLayout.preferredHeight = 54f;
        Button button = card.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.45f, 1.45f, 1.35f, 1f);
        colors.pressedColor = new Color(0.70f, 0.88f, 0.96f, 1f);
        colors.selectedColor = new Color(1.45f, 1.45f, 1.35f, 1f);
        colors.disabledColor = new Color(0.42f, 0.50f, 0.58f, 0.80f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        label = CreateTmp(card.transform, "Reply Text", string.Empty, 18f, TextAlignmentOptions.Left, Color.white, FontStyles.Normal);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(15f, 0f);
        labelRect.offsetMax = new Vector2(-40f, 0f);
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI chevron = CreateTmp(card.transform, "Reply Chevron", "›", 28f, TextAlignmentOptions.Center, MutedTextColor, FontStyles.Bold);
        RectTransform chevronRect = chevron.rectTransform;
        chevronRect.anchorMin = new Vector2(1f, 0f);
        chevronRect.anchorMax = new Vector2(1f, 1f);
        chevronRect.pivot = new Vector2(1f, 0.5f);
        chevronRect.sizeDelta = new Vector2(32f, 0f);
        chevronRect.anchoredPosition = new Vector2(-7f, 0f);
        return button;
    }

    private static GameObject CreateLayoutObject(Transform parent, string name)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject CreateImageObject(Transform parent, string name, Color color)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().color = color;
        return result;
    }

    private static TextMeshProUGUI CreateTmp(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, Color color, FontStyles style)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        result.transform.SetParent(parent, false);
        TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void ConfigureSlicedSurface(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        image.sprite = GetUiSprite();
        image.type = Image.Type.Sliced;
    }

    private static void ConfigureCircleSurface(GameObject target)
    {
        Image image = target.GetComponent<Image>();
        image.sprite = GetCircleSprite();
        image.type = Image.Type.Simple;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetUiSprite()
    {
        if (runtimeSurfaceSprite != null)
        {
            return runtimeSurfaceSprite;
        }

        const int size = 32;
        const int radius = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ChatPhoneRuntimeSurface",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cornerX = Mathf.Max(radius - x, x - (size - radius - 1), 0f);
                float cornerY = Mathf.Max(radius - y, y - (size - radius - 1), 0f);
                float alpha = cornerX * cornerX + cornerY * cornerY <= radius * radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        runtimeSurfaceSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        runtimeSurfaceSprite.name = "ChatPhoneRuntimeSurface";
        runtimeSurfaceSprite.hideFlags = HideFlags.HideAndDontSave;
        return runtimeSurfaceSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (runtimeCircleSprite != null)
        {
            return runtimeCircleSprite;
        }

        const int size = 32;
        float radius = (size - 1) * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ChatPhoneRuntimeCircle",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius;
                float dy = y - radius;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, dx * dx + dy * dy <= radius * radius ? 1f : 0f));
            }
        }

        texture.Apply(false, true);
        runtimeCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        runtimeCircleSprite.name = "ChatPhoneRuntimeCircle";
        runtimeCircleSprite.hideFlags = HideFlags.HideAndDontSave;
        return runtimeCircleSprite;
    }
}
