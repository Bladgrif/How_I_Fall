using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local, runtime-only owner of one transient authored chat.</summary>
public sealed class ChatController : MonoBehaviour
{
    private const float TerminalReplyPresentationSeconds = 0.35f;
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.82f);
    private static readonly Color PhoneColor = new Color(0.045f, 0.065f, 0.09f, 1f);
    private static readonly Color IncomingColor = new Color(0.14f, 0.20f, 0.27f, 1f);
    private static readonly Color PlayerColor = new Color(0.11f, 0.38f, 0.48f, 1f);

    private VNDialogueController dialogueController;
    private GameObject root;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI transcriptText;
    private Image imagePreview;
    private Button[] replyButtons;
    private Text[] replyLabels;
    private readonly List<ChatTranscriptEntry> transcript = new List<ChatTranscriptEntry>();
    private ChatSceneData activeChat;
    private SpecialModeLease activeLease;
    private int currentEntryIndex;
    private ChatRuntimeState runtimeState = ChatRuntimeState.Idle;
    private bool completionPending;
    private float terminalPresentationRemaining;
    private int completionCount;
    private int returnRouteAttemptCount;
    private DialogueSceneData lastReturnScene;

    public bool IsRunning => activeChat != null && activeLease != null && runtimeState != ChatRuntimeState.Resolved;
    public bool IsRuntimeUiActive => root != null && root.activeInHierarchy;
    public bool IsCompletionPending => completionPending;
    public int CompletionCount => completionCount;
    public int ReturnRouteAttemptCount => returnRouteAttemptCount;
    public DialogueSceneData LastReturnScene => lastReturnScene;
    public ChatRuntimeState RuntimeState => runtimeState;
    public IReadOnlyList<ChatTranscriptEntry> Transcript => transcript;
    public SpecialModeLease ActiveLease => activeLease;
    public int CurrentEntryIndex => currentEntryIndex;

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
        if (root == null || headerText == null || transcriptText == null || replyButtons == null || replyButtons.Length != 2)
        { failureReason = "Canvas/UI unavailable"; return false; }
        if (!chat.TryValidate(dialogueController.sceneRegistry, out string diagnostic))
        {
            failureReason = diagnostic.Contains("returnScene") ? "returnScene invalid/unregistered" : "chat asset invalid: " + diagnostic;
            return false;
        }
        if (!dialogueController.TryEnterSpecialMode(this, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease))
        { failureReason = dialogueController.HasActiveSpecialMode ? "another special mode active" : "lease rejected"; return false; }

        activeChat = chat;
        activeLease = lease;
        currentEntryIndex = 0;
        runtimeState = ChatRuntimeState.Active;
        completionPending = false;
        completionCount = 0;
        returnRouteAttemptCount = 0;
        lastReturnScene = null;
        transcript.Clear();
        headerText.text = chat.contactDisplayName;
        imagePreview.gameObject.SetActive(false);
        root.SetActive(true);
        ShowCurrentEntry();
        return true;
    }

    public bool TryChoose(int visibleOptionIndex)
    {
        if (!IsRunning || runtimeState != ChatRuntimeState.Active || completionPending || visibleOptionIndex < 0 || visibleOptionIndex >= 2) return false;
        ChatEntry entry = GetCurrentEntry();
        if (entry == null || entry.kind != ChatEntryKind.Choice || entry.options == null || entry.options.Count != 2) { Complete("Invalid runtime choice entry."); return false; }
        ChatChoiceOption option = entry.options[visibleOptionIndex];
        if (option == null || !ConditionalChoiceEvaluator.AreConditionsAvailable(option.conditions, GameState.Instance, option.text)) return false;
        Debug.Log("[ChatPhone] reply clicked: " + (visibleOptionIndex == 0 ? "A" : "B"), this);
        int target = string.IsNullOrEmpty(option.nextEntryId) ? -1 : activeChat.FindEntryIndex(option.nextEntryId);
        if (!string.IsNullOrEmpty(option.nextEntryId) && target < 0) { Complete("Choice target is invalid."); return false; }
        transcript.Add(new ChatTranscriptEntry(ChatSenderSide.Player, option.text));
        option.effects?.ApplyTo(GameState.Instance);
        RefreshTranscript();
        Debug.Log("[ChatPhone] outgoing appended", this);
        SetReplyButtons(false, null);
        if (target < 0) BeginTerminalCompletion(); else { currentEntryIndex = target; ShowCurrentEntry(); }
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
        SetReplyButtons(false, null);
        Debug.Log("[ChatPhone] terminal presentation started", this);
    }

    private void Update()
    {
        AdvanceTerminalPresentation(Time.unscaledDeltaTime);
    }

    /// <summary>Advances only the local terminal-reply presentation with unscaled time.</summary>
    public void AdvanceTerminalPresentation(float unscaledDeltaTime)
    {
        if (runtimeState != ChatRuntimeState.ResolvingTerminalChoice || unscaledDeltaTime < 0f)
        {
            return;
        }

        terminalPresentationRemaining = Mathf.Max(0f, terminalPresentationRemaining - unscaledDeltaTime);
        if (terminalPresentationRemaining > 0f)
        {
            return;
        }

        Debug.Log("[ChatPhone] terminal presentation finished", this);
        Complete(null);
    }

    private void ShowCurrentEntry()
    {
        if (!IsRunning) return;
        ChatEntry entry = GetCurrentEntry();
        if (entry == null) { Complete("Current entry is invalid."); return; }
        SetReplyButtons(false, null);
        switch (entry.kind)
        {
            case ChatEntryKind.Text:
                if (string.IsNullOrWhiteSpace(entry.text)) { Complete("Text payload is invalid."); return; }
                imagePreview.gameObject.SetActive(false);
                transcript.Add(new ChatTranscriptEntry(entry.sender, entry.text)); RefreshTranscript(); AdvanceOrderedEntry(); break;
            case ChatEntryKind.Image:
                if (entry.image == null) { Complete("Image payload is invalid."); return; }
                transcript.Add(new ChatTranscriptEntry(entry.sender, "[IMAGE]", entry.image)); imagePreview.sprite = entry.image; imagePreview.gameObject.SetActive(true); RefreshTranscript(); AdvanceOrderedEntry(); break;
            case ChatEntryKind.Choice:
                ShowChoices(entry); break;
            default: Complete("Unsupported entry kind."); break;
        }
    }

    private void AdvanceOrderedEntry()
    {
        if (currentEntryIndex + 1 >= activeChat.entries.Count) { Complete(null); return; }
        currentEntryIndex++;
        ShowCurrentEntry();
    }

    private void ShowChoices(ChatEntry entry)
    {
        var visible = new List<ChatChoiceOption>();
        foreach (ChatChoiceOption option in entry.options)
            if (option != null && ConditionalChoiceEvaluator.AreConditionsAvailable(option.conditions, GameState.Instance, option.text)) visible.Add(option);
        if (visible.Count == 0)
        {
            int fallback = string.IsNullOrEmpty(entry.fallbackEntryId) ? -1 : activeChat.FindEntryIndex(entry.fallbackEntryId);
            if (fallback >= 0) { currentEntryIndex = fallback; ShowCurrentEntry(); }
            else Complete("All responses were hidden and no valid fallback exists.");
            return;
        }
        // V1 always authors two options. Tests may hide one; disabled slots retain stable source indexes.
        for (int i = 0; i < 2; i++)
        {
            ChatChoiceOption option = entry.options[i];
            bool available = option != null && ConditionalChoiceEvaluator.AreConditionsAvailable(option.conditions, GameState.Instance, option.text);
            replyButtons[i].gameObject.SetActive(available);
            if (available) replyLabels[i].text = option.text;
        }
    }

    private void Complete(string diagnostic)
    {
        if (runtimeState == ChatRuntimeState.Resolved) return;
        Debug.Log("[ChatPhone] Complete begin", this);
        runtimeState = ChatRuntimeState.Resolved;
        completionPending = false;
        terminalPresentationRemaining = 0f;
        completionCount++;
        if (!string.IsNullOrEmpty(diagnostic)) Debug.LogWarning("[CHAT] " + diagnostic, this);
        DialogueSceneData returnScene = activeChat != null ? activeChat.returnScene : null;
        lastReturnScene = returnScene;
        ReleaseLease();
        ClearTransientState();
        if (returnScene != null && dialogueController != null)
        {
            returnRouteAttemptCount++;
            try
            {
                Debug.Log("[ChatPhone] return route requested", this);
                if (!dialogueController.TryRouteToScene(returnScene))
                {
                    Debug.Log("[ChatPhone] return route result: false", this);
                    Debug.LogWarning("[CHAT] Return route failed after controlled cleanup.", this);
                }
                else
                {
                    Debug.Log("[ChatPhone] return route result: true", this);
                }
            }
            catch (System.Exception exception)
            {
                Debug.Log("[ChatPhone] return route result: false", this);
                Debug.LogWarning("[CHAT] Return route threw after controlled cleanup: " + exception.Message, this);
            }
        }
    }

    private void ReleaseLease()
    {
        if (activeLease != null && dialogueController != null) dialogueController.ExitSpecialMode(activeLease);
        activeLease = null;
        Debug.Log("[ChatPhone] lease released", this);
    }

    private void ClearTransientState()
    {
        if (root != null) root.SetActive(false);
        transcript.Clear(); activeChat = null; currentEntryIndex = 0; terminalPresentationRemaining = 0f;
        Debug.Log("[ChatPhone] cleanup complete", this);
    }

    private ChatEntry GetCurrentEntry()
    {
        return activeChat != null && activeChat.entries != null && currentEntryIndex >= 0 && currentEntryIndex < activeChat.entries.Count ? activeChat.entries[currentEntryIndex] : null;
    }

    private void OnDisable() { CancelPendingPresentation(); ReleaseLease(); ClearTransientState(); }
    private void OnDestroy() { CancelPendingPresentation(); ReleaseLease(); transcript.Clear(); }

    private void CancelPendingPresentation()
    {
        completionPending = false;
        terminalPresentationRemaining = 0f;
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

    private void RefreshTranscript()
    {
        var lines = new List<string>();
        foreach (ChatTranscriptEntry entry in transcript)
        {
            string side = entry.sender == ChatSenderSide.Incoming ? "<align=left>" : "<align=right>";
            string body = entry.image != null ? "[IMAGE: TECHNICAL PLACEHOLDER]" : entry.text;
            lines.Add(side + body + "</align>");
        }
        transcriptText.text = string.Join("\n\n", lines);
    }

    private void SetReplyButtons(bool active, string ignored) { foreach (Button button in replyButtons) if (button != null) button.gameObject.SetActive(active); }

    private void BuildRuntimeUi(Canvas canvas)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root = CreatePanel(canvas.transform, "Chat Phone Runtime", OverlayColor, Vector2.zero, Vector2.one, Vector2.zero);
        GameObject phone = CreatePanel(root.transform, "Technical Phone", PhoneColor, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(620, 720));
        headerText = CreateTmp(phone.transform, "TEST CONTACT", 28, new Vector2(0, 310), new Vector2(540, 46), TextAlignmentOptions.Center, FontStyles.Bold);
        TextMeshProUGUI tag = CreateTmp(phone.transform, "TECH DEMO ONLY / NOT CANON", 13, new Vector2(0, 278), new Vector2(540, 30), TextAlignmentOptions.Center);
        tag.color = new Color(.6f,.76f,.86f,1f);
        GameObject transcriptArea = CreatePanel(phone.transform, "Transcript Scroll Area", new Color(.025f,.04f,.06f,1f), new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(540, 410));
        transcriptArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 45);
        transcriptText = CreateTmp(transcriptArea.transform, string.Empty, 20, Vector2.zero, new Vector2(500, 375), TextAlignmentOptions.TopLeft);
        transcriptText.enableWordWrapping = true;
        GameObject imageObject = new GameObject("Image Message", typeof(RectTransform), typeof(Image)); imageObject.transform.SetParent(phone.transform, false);
        imagePreview = imageObject.GetComponent<Image>(); imagePreview.preserveAspect = true; RectTransform imageRect = imageObject.GetComponent<RectTransform>(); imageRect.anchorMin=imageRect.anchorMax=new Vector2(.5f,.5f); imageRect.anchoredPosition=new Vector2(0,30); imageRect.sizeDelta=new Vector2(180,110);
        replyButtons = new Button[2]; replyLabels = new Text[2];
        for (int i=0;i<2;i++) { int index=i; replyButtons[i]=CreateButton(phone.transform, "Reply " + i, new Vector2(0, -205 - i*66), font, out replyLabels[i]); replyButtons[i].onClick.AddListener(()=>TryChoose(index)); }
    }

    private static GameObject CreatePanel(Transform parent,string name,Color color,Vector2 min,Vector2 max,Vector2 size) { var o=new GameObject(name,typeof(RectTransform),typeof(Image)); o.transform.SetParent(parent,false); var r=o.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;o.GetComponent<Image>().color=color;return o; }
    private static TextMeshProUGUI CreateTmp(Transform parent,string value,float size,Vector2 pos,Vector2 dimensions,TextAlignmentOptions align,FontStyles style=FontStyles.Normal) { var o=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=dimensions;var t=o.GetComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.text=value;t.fontSize=size;t.fontStyle=style;t.alignment=align;t.color=Color.white;t.overflowMode=TextOverflowModes.Ellipsis;return t; }
    private static Button CreateButton(Transform parent,string name,Vector2 pos,Font font,out Text label) { var o=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=new Vector2(500,52);var im=o.GetComponent<Image>();im.color=PlayerColor;var lo=new GameObject("Label",typeof(RectTransform),typeof(Text));lo.transform.SetParent(o.transform,false);var lr=lo.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;label=lo.GetComponent<Text>();label.font=font;label.fontSize=18;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;return o.GetComponent<Button>(); }
}
