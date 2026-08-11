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
        Require(config.technicalDemoChat.TryValidate(registry, out _), "Approved chat fixture must validate.");
        Require(config.technicalDemoChat.entries.Count == 3, "Fixture must include incoming text, image, and choice.");
        Require(config.technicalDemoChat.entries[0].kind == ChatEntryKind.Text && config.technicalDemoChat.entries[0].sender == ChatSenderSide.Incoming, "Incoming presentation fixture is missing.");
        Require(config.technicalDemoChat.entries[1].kind == ChatEntryKind.Image && config.technicalDemoChat.entries[1].image != null, "Image placeholder fixture is missing.");
        Require(config.technicalDemoChat.entries[2].options.Count == 2, "V1 choice must have exactly two options.");
        VerifyRuntimeChatBootstrap(config.technicalDemoChat, registry);

        ChatSceneData duplicate = CopyFixture(config.technicalDemoChat); duplicate.entries[1].entryId = duplicate.entries[0].entryId;
        Require(!duplicate.TryValidate(registry, out _), "Duplicate entry IDs must be rejected.");
        duplicate = CopyFixture(config.technicalDemoChat); duplicate.entries[1].kind = ChatEntryKind.Image; duplicate.entries[1].image = null;
        Require(!duplicate.TryValidate(registry, out _), "Null image must be rejected safely.");

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
        Require(!saveJson.Contains("chatId") && !saveJson.Contains("chatTranscript"), "SaveData must not contain chat runtime state.");
        Debug.Log("[CHAT] Smoke tests passed.");
    }

    private static void VerifyLauncherContract()
    {
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
            ChatSceneData terminalChat = CopyFixture(technicalChat);
            terminalChat.entries[2].options[0].effects = new ChatGameStateDelta { trustMashaDelta = 3 };
            Require(chat.TryStartChat(terminalChat, out string startReason),
                "Valid technical chat must start its runtime controller: " + startReason);
            Require(chat.IsRunning && chat.IsRuntimeUiActive,
                "Successful technical chat start must leave ChatController and runtime UI active.");
            Require(chat.HasSinglePhoneRoot && chat.HasDistinctReplyArea,
                "Chat must build one PhoneRoot with a distinct persistent ReplyArea.");
            Require(chat.HasImageCard,
                "Image entries must render an image card from the image payload, not debug text.");
            Require(chat.HasIncomingLeftPresentation,
                "Incoming transcript entries must use the left-aligned bubble presentation.");
            Require(chat.IsDialogueShellSuppressed && dialogue.IsDialogueShellSuppressed && !dialogue.dialogueUiRoot.activeSelf,
                "Chat must suppress the ordinary dialogue shell while its phone overlay is active.");
            Require(chat.AreReplyCardsInteractable,
                "Available reply cards must be the active chat input before selection.");
            Require(chat.TryChoose(0), "One terminal reply callback must be accepted.");
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
            Require(chat.TryChoose(0) && chat.IsRunning && !chat.IsCompletionPending && chat.CurrentEntryIndex == 3,
                "Non-terminal reply must immediately reach nextEntryId without a second input.");
            Require(!chat.TryStartChat(technicalChat, out string duplicateReason)
                && duplicateReason == "Chat already active",
                "Repeated start must reject an active chat without a duplicate UI.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(stateObject);
        }
    }

    private static ChatSceneData CopyFixture(ChatSceneData source)
    {
        ChatSceneData copy = ScriptableObject.CreateInstance<ChatSceneData>(); copy.chatId = source.chatId; copy.contactDisplayName = source.contactDisplayName; copy.returnScene = source.returnScene; copy.entries = new List<ChatEntry>();
        foreach (ChatEntry entry in source.entries)
        {
            var clone = new ChatEntry { entryId = entry.entryId, kind = entry.kind, sender = entry.sender, text = entry.text, image = entry.image, fallbackEntryId = entry.fallbackEntryId, options = new List<ChatChoiceOption>() };
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
