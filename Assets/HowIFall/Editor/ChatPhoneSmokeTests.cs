#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ChatPhoneSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Chat Phone Smoke Tests")]
    public static void RunBatchMode()
    {
        VerifyLauncherContract();

        ChatPhoneTechnicalConfig config = Resources.Load<ChatPhoneTechnicalConfig>(ChatPhoneTechnicalConfig.ResourcesPath);
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>("Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset");
        Require(config != null && config.technicalDemoChat != null, "Technical chat config is missing.");
        Require(AssetDatabase.GetAssetPath(config.technicalDemoChat) == ChatPhoneTechnicalAssets.ChatPath,
            "Technical config must reference the approved test_chat_v1 asset.");
        Require(config.technicalDemoChat.TryValidate(registry, out _), "Approved chat fixture must validate.");
        Require(config.technicalDemoChat.entries.Count == 3, "Fixture must include incoming text, image, and choice.");
        Require(config.technicalDemoChat.entries[0].kind == ChatEntryKind.Text && config.technicalDemoChat.entries[0].sender == ChatSenderSide.Incoming, "Incoming presentation fixture is missing.");
        Require(config.technicalDemoChat.entries[0].pacing == ChatEntryPacing.IncomingTyping && Mathf.Approximately(config.technicalDemoChat.entries[0].pacingSeconds, 1.5f), "Incoming fixture must use the authored 1.5-second IncomingTyping pacing.");
        Require(config.technicalDemoChat.entries[1].kind == ChatEntryKind.Image && config.technicalDemoChat.entries[1].image != null, "Image placeholder fixture is missing.");
        Require(config.technicalDemoChat.entries[1].pacing == ChatEntryPacing.Delay && Mathf.Approximately(config.technicalDemoChat.entries[1].pacingSeconds, 0.6f), "Image fixture must use the authored 0.6-second Delay pacing.");
        Require(config.technicalDemoChat.entries[2].pacing == ChatEntryPacing.Immediate, "Choice fixture must remain Immediate.");
        Require(config.technicalDemoChat.entries[2].options.Count == 2, "V1 choice must have exactly two options.");
        Require(config.technicalDemoChat.openSfx != null && AssetDatabase.GetAssetPath(config.technicalDemoChat.openSfx) == ChatPhoneTechnicalAssets.OpenSfxPath,
            "Technical fixture must reference the approved phone-open SFX.");
        Require(config.technicalDemoChat.incomingSfx != null && AssetDatabase.GetAssetPath(config.technicalDemoChat.incomingSfx) == ChatPhoneTechnicalAssets.IncomingSfxPath,
            "Technical fixture must reference the approved incoming-chat SFX.");
        VerifyRuntimeChatBootstrap(config.technicalDemoChat, registry);

        ChatSceneData duplicate = CopyFixture(config.technicalDemoChat); duplicate.entries[1].entryId = duplicate.entries[0].entryId;
        Require(!duplicate.TryValidate(registry, out _), "Duplicate entry IDs must be rejected.");
        duplicate = CopyFixture(config.technicalDemoChat); duplicate.entries[1].kind = ChatEntryKind.Image; duplicate.entries[1].image = null;
        Require(!duplicate.TryValidate(registry, out _), "Null image must be rejected safely.");
        VerifyPacingValidation(config.technicalDemoChat, registry);

        GameObject stateObject = new GameObject("ChatPhoneSmokeGameState");
        try
        {
            GameState state = stateObject.AddComponent<GameState>();
            var conditions = new List<ChoiceCondition> { new ChoiceCondition { stateValue = ChoiceStateValue.TrustMasha, comparison = ChoiceComparisonOperator.GreaterOrEqual, threshold = 0 }, new ChoiceCondition { stateValue = ChoiceStateValue.SelfControl, comparison = ChoiceComparisonOperator.GreaterOrEqual, threshold = 5 } };
            Require(ConditionalChoiceEvaluator.AreConditionsAvailable(conditions, state, "test"), "Typed multiple conditions must use AND.");
            conditions[1].threshold = 6; Require(!ConditionalChoiceEvaluator.AreConditionsAvailable(conditions, state, "test"), "Unavailable response condition must be hidden.");
            var delta = new ChatGameStateDelta { trustMashaDelta = 2, corruptionDelta = -1 }; delta.ApplyTo(state);
            Require(state.trustMasha == 2 && state.corruptionLevel == -1, "Typed effects must modify the correct state fields.");
        }
        finally { UnityEngine.Object.DestroyImmediate(stateObject); }

        var coordinator = new SpecialModeCoordinator();
        GameObject owner = new GameObject("ChatPhoneSmokeOwner");
        try
        {
            Require(coordinator.TryEnter(owner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "Chat must acquire BlockingExclusive.");
            Require(coordinator.IsDialogueAdvanceBlocked && coordinator.IsAutoBlocked && coordinator.IsSkipBlocked && !coordinator.CanSave && !coordinator.CanLoad && !coordinator.CanOpenBacklog && !coordinator.CanOpenSettings && !coordinator.CanOpenQuickMenu, "BlockingExclusive policy must gate VN actions.");
            Require(!coordinator.TryRequestEscapeCancel(), "Escape must not cancel BlockingExclusive chat.");
            Require(coordinator.Exit(lease), "Lease must release before routing.");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }

        Require(SaveData.CurrentVersion == 3, "SaveData version must remain v3.");
        string saveJson = JsonUtility.ToJson(new SaveData());
        Require(!saveJson.Contains("chatId") && !saveJson.Contains("chatTranscript") && !saveJson.Contains("openSfx") && !saveJson.Contains("incomingSfx"), "SaveData must not contain chat runtime state or SFX references.");
        Debug.Log("[CHAT] Smoke tests passed.");
    }

    private static void VerifyLauncherContract()
    {
        ChatSceneData technicalChat = AssetDatabase.LoadAssetAtPath<ChatSceneData>(ChatPhoneTechnicalAssets.ChatPath);
        AudioClip openSfxBeforeRepair = technicalChat != null ? technicalChat.openSfx : null;
        AudioClip incomingSfxBeforeRepair = technicalChat != null ? technicalChat.incomingSfx : null;
        ChatPhoneTechnicalAssets.CreateOrRepair();
        Require(technicalChat != null && technicalChat.openSfx == openSfxBeforeRepair && technicalChat.incomingSfx == incomingSfxBeforeRepair
            && technicalChat.openSfx != null && technicalChat.incomingSfx != null,
            "Technical asset repair must preserve approved chat SFX references.");

        List<ChatEntry> rebuiltEntries = ChatPhoneTechnicalAssets.CreateTechnicalEntries(null);
        Require(rebuiltEntries.Count == 3
            && rebuiltEntries[0].pacing == ChatEntryPacing.IncomingTyping && Mathf.Approximately(rebuiltEntries[0].pacingSeconds, 1.5f)
            && rebuiltEntries[1].pacing == ChatEntryPacing.Delay && Mathf.Approximately(rebuiltEntries[1].pacingSeconds, 0.6f)
            && rebuiltEntries[2].pacing == ChatEntryPacing.Immediate && Mathf.Approximately(rebuiltEntries[2].pacingSeconds, 0f),
            "Technical asset repair must preserve the authored technical pacing values.");

        // Pure plan checks: the Edit Mode branch only selects the editor-scene path;
        // it cannot invoke SceneManager until Play Mode has actually started.
        Require(
            ChatPhoneTechnicalDemoLaunchPlan.GetAction(false, false)
                == ChatPhoneTechnicalDemoLaunchAction.OpenInEditModeAndEnterPlay,
            "Edit Mode launcher path must select OpenScene before Play Mode only.");
        Require(
            ChatPhoneTechnicalDemoLaunchPlan.GetAction(true, true)
                == ChatPhoneTechnicalDemoLaunchAction.WaitForControllerInCurrentPlayScene,
            "Play Mode on VNPrototype must wait for the existing controller without reloading.");
        Require(
            ChatPhoneTechnicalDemoLaunchPlan.GetAction(true, false)
                == ChatPhoneTechnicalDemoLaunchAction.LoadRuntimeScene,
            "Play Mode outside VNPrototype must select the runtime scene API path.");
        Require(
            ChatPhoneTechnicalDemoLaunchPlan.GetAction(true, true)
                == ChatPhoneTechnicalDemoLaunchPlan.GetAction(true, true),
            "Repeated launcher planning must be deterministic and cannot create duplicate starts.");

        ChatPhoneTechnicalDemoLauncher.ClearPendingStartForTests();
        Require(!ChatPhoneTechnicalDemoLauncher.HasPendingStartForTests,
            "Launcher cleanup must clear its pending one-shot state.");
        Require(ChatPhoneTechnicalDemoLauncher.TryMarkPendingStartForTests(),
            "First pending technical-demo start must be accepted.");
        Require(!ChatPhoneTechnicalDemoLauncher.TryMarkPendingStartForTests(),
            "Repeated pending technical-demo start must be rejected exactly once.");
        ChatPhoneTechnicalDemoLauncher.ClearPendingStartForTests();
        Require(!ChatPhoneTechnicalDemoLauncher.HasPendingStartForTests,
            "Cleanup must remove the pending state after the one-shot start.");
    }

    private static void VerifyPacingValidation(ChatSceneData technicalChat, DialogueSceneRegistry registry)
    {
        ChatSceneData invalid = CopyFixture(technicalChat);
        invalid.entries[0].sender = ChatSenderSide.Player;
        Require(!invalid.TryValidate(registry, out _), "IncomingTyping on a Player entry must be rejected.");

        invalid = CopyFixture(technicalChat);
        invalid.entries[2].pacing = ChatEntryPacing.IncomingTyping;
        invalid.entries[2].pacingSeconds = 0.8f;
        Require(!invalid.TryValidate(registry, out _), "IncomingTyping on a Choice entry must be rejected.");

        invalid = CopyFixture(technicalChat);
        invalid.entries[2].pacing = ChatEntryPacing.Delay;
        invalid.entries[2].pacingSeconds = 0.3f;
        Require(!invalid.TryValidate(registry, out _), "Choice pacing other than Immediate must be rejected.");

        invalid = CopyFixture(technicalChat);
        invalid.entries[1].pacingSeconds = -0.1f;
        Require(!invalid.TryValidate(registry, out _), "Negative pacing duration must be rejected.");

        invalid = CopyFixture(technicalChat);
        invalid.entries[1].pacingSeconds = 0f;
        Require(!invalid.TryValidate(registry, out _), "Delay requires a positive duration.");
    }

    private static void VerifyRuntimeChatBootstrap(ChatSceneData technicalChat, DialogueSceneRegistry registry)
    {
        GameObject canvasObject = new GameObject("ChatPhoneSmokeCanvas", typeof(Canvas));
        GameObject controllerObject = new GameObject("ChatPhoneSmokeController");
        controllerObject.transform.SetParent(canvasObject.transform, false);
        GameObject stateObject = new GameObject("ChatPhoneSmokeRuntimeState");
        try
        {
            stateObject.AddComponent<GameState>();
            GameState gameState = GameState.EnsureInstance();
            gameState.ResetState();
            VNDialogueController dialogue = controllerObject.AddComponent<VNDialogueController>();
            dialogue.sceneRegistry = registry;
            ConfigureRouteUi(dialogue, canvasObject.transform);
            Require(!dialogue.TryStartChat(technicalChat, out string notReadyReason)
                && notReadyReason == "controller not ready",
                "A found controller before Start completion must explicitly report not-ready.");
            Require(ChatController.TryCreateRuntime(dialogue, out ChatController chat, out string createReason),
                "Ready chat runtime UI must be creatable: " + createReason);
            ChatSceneData rejectedChat = CopyFixture(technicalChat);
            rejectedChat.returnScene = null;
            Require(!chat.TryStartChat(rejectedChat, out _) && chat.OpenSfxRequestCount == 0,
                "Rejected chat starts must not request the open SFX.");
            ChatSceneData terminalChat = CopyFixture(technicalChat);
            terminalChat.entries[2].options[0].effects = new ChatGameStateDelta { trustMashaDelta = 3 };
            int backlogBeforeChat = dialogue.CaptureBacklogSnapshot().Count;
            Require(chat.TryStartChat(terminalChat, out string startReason),
                "Valid technical chat must start its runtime controller: " + startReason);
            Require(chat.IsRunning && chat.IsRuntimeUiActive,
                "Successful technical chat start must leave ChatController and runtime UI active.");
            Require(chat.OpenSfxRequestCount == 1,
                "A successful chat start must request its open SFX exactly once.");
            Require(chat.HasSinglePhoneRoot && chat.HasDistinctReplyArea,
                "Chat must build one PhoneRoot with a distinct persistent ReplyArea.");
            Require(chat.IsDialogueShellSuppressed && dialogue.IsDialogueShellSuppressed && !dialogue.dialogueUiRoot.activeSelf,
                "Chat must suppress the ordinary dialogue shell while its phone overlay is active.");
            Require(chat.RuntimeState == ChatRuntimeState.WaitingForEntryReveal && chat.HasPendingEntryReveal,
                "IncomingTyping must enter a deterministic waiting state.");
            Require(chat.IsTypingIndicatorVisible && chat.Transcript.Count == 0,
                "IncomingTyping must show a transient indicator outside the transcript.");
            Require(chat.IncomingSfxRequestCount == 0,
                "Typing indicator must not request an incoming SFX before the entry reveal.");
            Require(!chat.AreReplyCardsInteractable && !chat.TryChoose(0),
                "Reply cards must remain unavailable while pacing is active.");
            Require(dialogue.HasActiveSpecialMode && !dialogue.CanAdvanceDialogue
                && !dialogue.CanOpenQuickMenu && !dialogue.CanOpenBacklog && !dialogue.CanOpenSettings,
                "BlockingExclusive must remain owned while entry pacing is active.");
            Require(dialogue.CaptureBacklogSnapshot().Count == backlogBeforeChat,
                "Typing indicator and pending chat entry must not enter the dialogue backlog.");
            chat.AdvanceEntryPacing(0.75f);
            Require(chat.Transcript.Count == 0 && chat.IsTypingIndicatorVisible,
                "IncomingTyping must not reveal before its countdown finishes.");
            chat.AdvanceEntryPacing(0.75f);
            Require(chat.Transcript.Count == 1 && !chat.IsTypingIndicatorVisible && chat.RuntimeState == ChatRuntimeState.WaitingForEntryReveal,
                "IncomingTyping must hide before revealing the entry and then begin the next authored delay.");
            Require(chat.IncomingSfxRequestCount == 1,
                "The first incoming text reveal must request its incoming SFX once.");
            Require(chat.HasIncomingLeftPresentation,
                "Incoming transcript entries must use the left-aligned bubble presentation.");
            Require(!chat.HasImageCard && !chat.AreReplyCardsInteractable,
                "The delayed image and replies must not appear in the incoming reveal frame.");
            chat.AdvanceEntryPacing(0.3f);
            Require(!chat.HasImageCard && chat.Transcript.Count == 1,
                "Delay must not reveal before enough unscaled countdown time.");
            chat.AdvanceEntryPacing(0.3f);
            Require(chat.HasImageCard && chat.Transcript.Count == 2 && chat.AreReplyCardsInteractable,
                "Delay must reveal the image automatically and then show replies.");
            Require(chat.IncomingSfxRequestCount == 2,
                "The delayed incoming image reveal must request one additional incoming SFX.");
            chat.AdvanceEntryPacing(10f);
            Require(chat.Transcript.Count == 2,
                "A completed delayed entry must reveal exactly once.");
            Require(chat.IncomingSfxRequestCount == 2,
                "Repeated reveal callbacks must not request duplicate incoming SFX.");
            chat.RefreshTranscriptForTests();
            Require(chat.IncomingSfxRequestCount == 2,
                "Transcript layout refresh must not request an incoming SFX.");
            Require(dialogue.CaptureBacklogSnapshot().Count == backlogBeforeChat,
                "Chat transcript entries must remain isolated from the dialogue backlog.");
            Require(chat.TryChoose(0), "One terminal reply callback must be accepted.");
            Require(chat.IncomingSfxRequestCount == 2,
                "Outgoing replies must not request an incoming SFX.");
            Require(chat.IsCompletionPending && chat.RuntimeState == ChatRuntimeState.ResolvingTerminalChoice,
                "Terminal reply must enter an automatic resolving state without another input.");
            Require(chat.Transcript.Count > 0 && chat.Transcript[chat.Transcript.Count - 1].sender == ChatSenderSide.Player,
                "Outgoing reply must be appended before terminal completion.");
            Require(gameState.trustMasha == 3, "Terminal reply effect must apply exactly once before completion.");
            Require(chat.HasPlayerRightPresentation,
                "Selected player replies must use the right-aligned bubble presentation.");
            Require(!chat.AreReplyCardsInteractable,
                "Selecting a reply must disable its cards before terminal completion.");
            dialogue.AdvanceDialogue();
            Require(chat.IsCompletionPending, "Generic dialogue advance must not be required or accepted during terminal presentation.");
            Require(!chat.TryChoose(0) && gameState.trustMasha == 3,
                "Second reply callback during pending completion must be a no-op.");
            chat.AdvanceTerminalPresentation(0.2f);
            Require(chat.IsCompletionPending && chat.IsRunning,
                "Terminal presentation must remain active before its unscaled countdown finishes.");
            chat.AdvanceTerminalPresentation(0.3f);
            Require(chat.CompletionCount == 1 && chat.ReturnRouteAttemptCount == 1 && chat.LastReturnScene == terminalChat.returnScene,
                "Terminal completion must attempt the authored return route exactly once.");
            Require(dialogue.sceneData == terminalChat.returnScene,
                "Successful terminal completion must make the authored returnScene current without a second chat input.");
            Require(!string.IsNullOrEmpty(dialogue.dialogueText.text),
                "Return scene must begin presenting its first line immediately after the automatic route.");
            Require(!dialogue.IsDialogueShellSuppressed && dialogue.dialogueUiRoot.activeSelf,
                "Dialogue shell restore must respect its pre-chat legitimate visible state.");
            Require(!chat.TryCompletePendingTerminalPresentation() && chat.CompletionCount == 1,
                "Second completion callback must be a no-op.");

            ChatSceneData branchingChat = CopyFixture(technicalChat);
            branchingChat.entries[2].options[0].nextEntryId = "followup";
            branchingChat.entries.Add(new ChatEntry
            {
                entryId = "followup",
                kind = ChatEntryKind.Choice,
                sender = ChatSenderSide.Player,
                options = new List<ChatChoiceOption>
                {
                    new ChatChoiceOption { text = "TEST: reply A" },
                    new ChatChoiceOption { text = "TEST: reply B" }
                }
            });
            Require(branchingChat.TryValidate(registry, out _), "Non-terminal branch fixture must validate.");
            Require(chat.TryStartChat(branchingChat, out startReason), "Chat must restart after terminal cleanup: " + startReason);
            chat.AdvanceEntryPacing(1.5f);
            chat.AdvanceEntryPacing(0.6f);
            Require(chat.TryChoose(0) && chat.IsRunning && !chat.IsCompletionPending && chat.CurrentEntryIndex == 3,
                "Non-terminal reply must immediately reach nextEntryId without a second input.");
            Require(!chat.TryStartChat(technicalChat, out string duplicateReason)
                && duplicateReason == "Chat already active",
                "Repeated start must reject an active chat without a duplicate UI.");
            Require(chat.OpenSfxRequestCount == 1,
                "Duplicate starts must not request another open SFX.");
            Require(chat.TryChoose(0), "Follow-up branch must still accept its terminal reply.");
            chat.AdvanceTerminalPresentation(1f);

            ChatSceneData immediateChat = CopyFixture(technicalChat);
            immediateChat.entries[0].pacing = ChatEntryPacing.Immediate;
            immediateChat.entries[0].pacingSeconds = 0f;
            Require(chat.TryCompletePendingTerminalPresentation() == false,
                "Completed chat cleanup must not expose a second terminal completion callback.");
            Require(chat.TryStartChat(immediateChat, out startReason), "Chat must restart after terminal cleanup: " + startReason);
            Require(chat.Transcript.Count == 1 && chat.RuntimeState == ChatRuntimeState.WaitingForEntryReveal && !chat.IsTypingIndicatorVisible,
                "Immediate entries must reveal immediately without a typing indicator.");
            chat.AdvanceEntryPacing(1f);
            Require(chat.AreReplyCardsInteractable, "The remaining delayed entry must reveal automatically after its countdown.");
            Require(chat.TryChoose(0), "Immediate fixture must still accept its terminal reply.");
            chat.AdvanceTerminalPresentation(1f);

            Require(chat.TryStartChat(terminalChat, out startReason), "Chat must start for cleanup pacing coverage: " + startReason);
            int cleanupCompletionCount = chat.CompletionCount;
            int cleanupRouteCount = chat.ReturnRouteAttemptCount;
            Require(chat.IsTypingIndicatorVisible && chat.HasPendingEntryReveal, "Cleanup fixture must be waiting on IncomingTyping.");
            Require(chat.TryCleanupPendingPacingForTests(), "Pending pacing cleanup must be accepted exactly once.");
            chat.AdvanceEntryPacing(10f);
            Require(!chat.IsTypingIndicatorVisible && !chat.HasPendingEntryReveal && !chat.IsRunning,
                "Cleanup must cancel pending pacing and remove the typing indicator.");
            Require(chat.CompletionCount == cleanupCompletionCount && chat.ReturnRouteAttemptCount == cleanupRouteCount,
                "Cleanup must not reveal or route a pending entry.");
            Require(chat.IncomingSfxRequestCount == 0,
                "Cleanup before reveal must not request the pending incoming SFX.");

            ChatSceneData silentChat = CopyFixture(technicalChat);
            silentChat.openSfx = null;
            silentChat.incomingSfx = null;
            Require(chat.TryStartChat(silentChat, out startReason), "Null SFX fixtures must remain valid and start silently: " + startReason);
            Require(chat.OpenSfxRequestCount == 0 && chat.IncomingSfxRequestCount == 0,
                "Null chat SFX fields must be intentionally silent.");
            chat.AdvanceEntryPacing(1f);
            Require(chat.IncomingSfxRequestCount == 0,
                "Incoming reveals with null SFX must remain silent.");
            Require(chat.TryCleanupPendingPacingForTests(), "Silent fixture cleanup must remain available.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(stateObject);
        }
    }

    private static ChatSceneData CopyFixture(ChatSceneData source)
    {
        ChatSceneData copy = ScriptableObject.CreateInstance<ChatSceneData>(); copy.chatId = source.chatId; copy.contactDisplayName = source.contactDisplayName; copy.returnScene = source.returnScene; copy.openSfx = source.openSfx; copy.incomingSfx = source.incomingSfx; copy.entries = new List<ChatEntry>();
        foreach (ChatEntry entry in source.entries)
        {
            var clone = new ChatEntry { entryId = entry.entryId, kind = entry.kind, sender = entry.sender, pacing = entry.pacing, pacingSeconds = entry.pacingSeconds, text = entry.text, image = entry.image, fallbackEntryId = entry.fallbackEntryId, options = new List<ChatChoiceOption>() };
            foreach (ChatChoiceOption option in entry.options ?? new List<ChatChoiceOption>()) clone.options.Add(new ChatChoiceOption { text = option.text, nextEntryId = option.nextEntryId, conditions = new List<ChoiceCondition>(option.conditions ?? new List<ChoiceCondition>()), effects = option.effects });
            copy.entries.Add(clone);
        }
        return copy;
    }

    private static void ConfigureRouteUi(VNDialogueController dialogue, Transform parent)
    {
        dialogue.dialogueUiRoot = CreateUiObject(parent, "ChatPhoneSmokeDialogueShell");
        dialogue.nameBox = CreateUiObject(dialogue.dialogueUiRoot.transform, "NameBox");
        dialogue.speakerText = CreateText(dialogue.nameBox.transform, "SpeakerText");
        dialogue.dialogueText = CreateText(dialogue.dialogueUiRoot.transform, "DialogueText");
        dialogue.nextButton = CreateUiObject(dialogue.dialogueUiRoot.transform, "NextButton").AddComponent<UnityEngine.UI.Button>();
        dialogue.choicePanel = CreateUiObject(dialogue.dialogueUiRoot.transform, "ChoicePanel");
        dialogue.choicePanel.SetActive(false);
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TMPro.TextMeshProUGUI CreateText(Transform parent, string name)
    {
        GameObject result = CreateUiObject(parent, name);
        TMPro.TextMeshProUGUI text = result.AddComponent<TMPro.TextMeshProUGUI>();
        text.font = TMPro.TMP_Settings.defaultFontAsset;
        return text;
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
