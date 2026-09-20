using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    /// <summary>
    /// Regression for the physical mouse-wheel reading contract through the real
    /// runtime Update path: a virtual Mouse scroll event must reach
    /// VNDialogueController.Update() via VNInputMap and move actual dialogue state.
    /// Physical wheel forward / away from the player (positive scrollY, the Unity
    /// InputSystem wheel convention confirmed in this harness) must behave exactly
    /// like a normal advance click; physical wheel backward / toward the player
    /// (negative scrollY) must perform exactly one guarded rollback step through
    /// the existing TryRollback backend. The tests never call
    /// VNInputMap.WasPressedThisFrame, TryHandleReadingNavigation or TryRollback
    /// to drive dialogue state; they only roll the virtual wheel and let the
    /// production Update loop react.
    /// </summary>
    public sealed class WheelReadingRuntimePlayModeTests : InputTestFixture
    {
        private Mouse mouse;
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> playerPrefsKeys = new List<string>();

        public override void Setup()
        {
            // InputTestFixture.Setup() resets the whole input runtime, so the
            // virtual mouse must be added after it, never in [UnitySetUp].
            base.Setup();
            DestroyExistingSingletons();
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (string key in playerPrefsKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object item = createdObjects[index];
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item);
                }
            }
            createdObjects.Clear();
            DestroyExistingSingletons();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForwardWheel_CompletesTypingFirst_ThenAdvancesExactlyOneLine()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-fwd", "A", "B"));
            yield return null;
            Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.True);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));

            yield return RollWheel(120f);

            Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.False,
                "First physical-forward wheel event must only complete the typewriter.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "First physical-forward wheel event must not advance the line.");
            Assert.That(context.Controller.dialogueText.text, Is.EqualTo("A"));

            yield return RollWheel(120f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "Second physical-forward wheel event must advance A -> B exactly once.");

            // The scroll control resets to zero on the next input update; one more
            // frame must not advance the dialogue again (no double advance).
            yield return null;

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "A wheel event must never advance more than one dialogue step.");
        }

        [UnityTest]
        public IEnumerator BackwardWheel_FromStableLine_RollsBackExactlyOneLevel()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-back", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            AdvanceAndComplete(context.Controller);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"));
            Assert.That(context.Controller.CanRollback, Is.True, "Setup must provide a rollbackable A -> B state.");

            yield return RollWheel(-120f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "One physical-backward wheel event must roll B -> A exactly once through TryRollback.");
            Assert.That(context.GameState.currentLineIndex, Is.EqualTo(0),
                "Backward wheel must move through the guarded backend, not mutate the line index.");
            Assert.That(context.Controller.dialogueText.text, Is.EqualTo("A"));
            Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.False);

            yield return null;

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "One backward wheel event must roll back exactly one level.");
        }

        [UnityTest]
        public IEnumerator RoundTrip_ForwardBackForward_ReexecutesOrdinarily()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-round", "A", "B"));
            yield return null;

            yield return RollWheel(120f); // completes A typing
            yield return RollWheel(120f); // A -> B
            CompleteCurrentLine(context.Controller); // B stable
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"));

            yield return RollWheel(-120f);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));

            yield return RollWheel(120f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "Forward wheel after rollback must execute ordinary progression, not redo history.");
            Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.True,
                "Re-executed B must be a fresh line with its own typewriter run.");
            CompleteCurrentLine(context.Controller);
            Assert.That(context.Controller.CanRollback, Is.True,
                "The re-executed stable B must provide an ordinary rollback checkpoint again.");
        }

        [UnityTest]
        public IEnumerator LargeForwardScrollDelta_AdvancesAtMostOneLine()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-large-fwd", "A", "B", "C"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));

            yield return RollWheel(100000f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "One large physical-forward wheel delta must advance at most one line.");

            yield return null;

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "A large wheel delta must never advance more than one dialogue step.");
        }

        [UnityTest]
        public IEnumerator LargeBackwardScrollDelta_RollsBackAtMostOneLevel()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-large-back", "A", "B", "C"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            AdvanceAndComplete(context.Controller);
            AdvanceAndComplete(context.Controller);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-2"));

            yield return RollWheel(-100000f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "One large physical-backward wheel delta must roll back at most one level.");

            yield return null;

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "A large wheel delta must never roll back more than one dialogue step.");
        }

        [UnityTest]
        public IEnumerator BackwardWheel_WithoutRollbackHistory_IsBlocked()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-noguard", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            Assert.That(context.Controller.CanRollback, Is.False,
                "The first stable line must not provide a rollback target.");

            yield return RollWheel(-120f);

            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Backward wheel must stay blocked by the existing rollback guards when no history exists.");
            Assert.That(context.Controller.dialogueText.text, Is.EqualTo("A"));
        }

        [UnityTest]
        public IEnumerator WheelEvents_RespectGameMenuSaveLoadHistoryAndPreferences()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-modals", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            // Game Menu owns input first in the production Update order.
            Assert.That(context.Controller.OpenGameMenu(), Is.True);
            yield return RollWheel(120f);
            yield return RollWheel(-120f);
            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "Wheel events must not close the Game Menu.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while the Game Menu is open.");
            context.Controller.GameMenuController.Close();
            Assert.That(context.Controller.IsGameMenuOpen, Is.False);

            // Save/Load panel is blocked by the advance guard and the rollback modal guard.
            context.Controller.manualSaveLoadPanel.OpenSave();
            Assert.That(context.Controller.manualSaveLoadPanel.IsOpen, Is.True);
            yield return RollWheel(120f);
            yield return RollWheel(-120f);
            Assert.That(context.Controller.manualSaveLoadPanel.IsOpen, Is.True,
                "Wheel events must not close the Save/Load panel.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while Save/Load is open.");
            context.Controller.manualSaveLoadPanel.Close();
            yield return WaitUntilSaveLoadPanelClosed(context.Controller.manualSaveLoadPanel);

            // History is an ordinary modal for both directions.
            context.Controller.ShowBacklog();
            Assert.That(context.Controller.backlogPanel.activeSelf, Is.True);
            yield return RollWheel(120f);
            yield return RollWheel(-120f);
            Assert.That(context.Controller.backlogPanel.activeSelf, Is.True, "Wheel events must not close History.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while History is open.");
            context.Controller.HideBacklog();
            yield return null;
            Assert.That(context.Controller.backlogPanel.activeSelf, Is.False);

            // Preferences short-circuits the whole reading branch in Update.
            context.Controller.vnSettingsPanel.SetActive(true);
            Assert.That(context.Controller.IsPreferencesOpen, Is.True);
            yield return RollWheel(120f);
            yield return RollWheel(-120f);
            Assert.That(context.Controller.IsPreferencesOpen, Is.True, "Wheel events must not close Preferences.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while Preferences are open.");
            context.Controller.vnSettingsPanel.SetActive(false);

            // After every modal round-trip ordinary wheel reading must still work.
            yield return RollWheel(120f);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "After all modals closed, the physical-forward wheel must advance again.");
        }

        [UnityTest]
        public IEnumerator WheelEvents_RespectActiveChoices()
        {
            DialogueSceneData choiceScene = CreateLinearScene("wheel-choice", "Choose");
            choiceScene.choices = new List<DialogueChoice>
            {
                CreateChoice("A", "Result A", CreateLinearScene("wheel-choice-target", "Target"))
            };
            RuntimeContext context = CreateContext(choiceScene, choiceScene.choices[0].nextScene);
            yield return null;
            CompleteCurrentLine(context.Controller);
            context.Controller.AdvanceDialogue();
            Assert.That(context.Controller.choicePanel.activeSelf, Is.True);

            yield return RollWheel(120f);
            yield return RollWheel(-120f);

            Assert.That(context.Controller.choicePanel.activeSelf, Is.True,
                "Wheel events must not close active choices.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while choices are active.");
            Assert.That(context.GameState.choiceResultActive, Is.False,
                "Wheel events must not apply any choice.");
        }

        [UnityTest]
        public IEnumerator WheelEvents_RespectSpecialModeBlockingPolicy()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-special", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            Assert.That(
                context.Controller.TryEnterSpecialMode(
                    context.Controller.gameObject,
                    SpecialModePolicy.BlockingExclusive,
                    out SpecialModeLease lease),
                Is.True, "Setup must accept the special-mode lease.");

            yield return RollWheel(120f);
            yield return RollWheel(-120f);

            Assert.That(context.Controller.HasActiveSpecialMode, Is.True, "Wheel events must not end the special mode.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue during a blocking special mode.");

            context.Controller.ExitSpecialMode(lease);
            yield return RollWheel(120f);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "After the special mode exits, the physical-forward wheel must advance again.");
        }

        [UnityTest]
        public IEnumerator WheelEvents_RespectHiddenInterface()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-hidden", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            Assert.That(context.Controller.TryHideInterface(), Is.True);
            Assert.That(context.Controller.IsInterfaceHidden, Is.True);

            yield return RollWheel(120f);
            yield return RollWheel(-120f);

            Assert.That(context.Controller.IsInterfaceHidden, Is.True,
                "Wheel events must not restore the hidden interface.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue while the interface is hidden.");

            context.Controller.RestoreInterface();
            yield return RollWheel(120f);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "After the interface is restored, the physical-forward wheel must advance again.");
        }

        [UnityTest]
        public IEnumerator WheelEvents_AreBlockedWhileReplayModeIsActive()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("wheel-replay", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            ReplayEntryDefinition definition = ScriptableObject.CreateInstance<ReplayEntryDefinition>();
            createdObjects.Add(definition);
            definition.replayId = "wheel-replay-entry";
            definition.startScene = context.StartScene;

            SceneFlowManager flow = SceneFlowManager.EnsureInstance();
            ReplaySession session = new ReplaySession(definition, context.GameState, context.Controller);
            GetPrivateField(typeof(SceneFlowManager), "replaySession").SetValue(flow, session);
            session.Activate(context.GameState);
            Assert.That(SceneFlowManager.IsReplayModeActive, Is.True, "Setup must activate the replay session.");

            yield return RollWheel(120f);
            yield return RollWheel(-120f);

            Assert.That(SceneFlowManager.IsReplayModeActive, Is.True, "Wheel events must not end the replay session.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"),
                "Wheel events must not move dialogue during replay.");

            GetPrivateField(typeof(SceneFlowManager), "replaySession").SetValue(flow, null);
            Assert.That(SceneFlowManager.IsReplayModeActive, Is.False);
            yield return RollWheel(120f);
            Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"),
                "After the replay session ends, the physical-forward wheel must advance again.");
        }

        private IEnumerator RollWheel(float scrollY)
        {
            Assert.That(mouse != null && mouse.added, Is.True, "Virtual mouse must exist for the whole wheel event.");
            Set(mouse.scroll, new Vector2(0f, scrollY));
            yield return null;
        }

        private static IEnumerator WaitUntilSaveLoadPanelClosed(ManualSaveLoadPanel panel)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (panel.IsOpen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(panel.IsOpen, Is.False, "Save/Load panel must finish its close animation.");
            yield return null;
        }

        private static DialogueChoice CreateChoice(string text, string resultText, DialogueSceneData nextScene)
        {
            return new DialogueChoice
            {
                text = text,
                resultText = resultText,
                nextScene = nextScene,
                lustDelta = 0,
                romanceDelta = 0,
                purityDelta = 0,
                corruptionDelta = 0,
                selfControlDelta = 0,
                suspicionDelta = 0,
                trustMashaDelta = 0,
                trustArtemDelta = 0,
                leraInterestDelta = 0
            };
        }

        private static void CompleteCurrentLine(VNDialogueController controller)
        {
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.True);
            controller.AdvanceDialogue();
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.False);
        }

        private static void AdvanceAndComplete(VNDialogueController controller)
        {
            controller.AdvanceDialogue();
            CompleteCurrentLine(controller);
        }

        private RuntimeContext CreateContext(DialogueSceneData startScene, params DialogueSceneData[] additionalScenes)
        {
            SettingsManager settings = new GameObject("Wheel SettingsManager").AddComponent<SettingsManager>();
            createdObjects.Add(settings.gameObject);
            settings.settings.autoSave = false;
            settings.settings.autoForward = false;
            settings.settings.textSpeed = 20f;

            AudioManager audio = new GameObject("Wheel AudioManager").AddComponent<AudioManager>();
            createdObjects.Add(audio.gameObject);

            GameState gameState = new GameObject("Wheel GameState").AddComponent<GameState>();
            createdObjects.Add(gameState.gameObject);

            GameObject canvasObject = new GameObject("Wheel Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            createdObjects.Add(canvasObject);

            GameObject eventSystemObject = new GameObject("Wheel EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            createdObjects.Add(eventSystemObject);

            GameObject controllerObject = new GameObject("Wheel VNDialogueController");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();

            controller.speakerText = CreateTmp("Speaker", controllerObject.transform);
            controller.dialogueText = CreateTmp("Dialogue", controllerObject.transform);
            controller.backgroundImage = CreateImage("Background", controllerObject.transform);
            controller.characterImage = CreateImage("Character", controllerObject.transform);
            controller.nameBox = CreateGameObject("Name Box", controllerObject.transform);
            controller.nextButton = CreateButton("Next", controllerObject.transform);
            controller.dialogueUiRoot = CreateGameObject("Dialogue UI Root", controllerObject.transform);
            controller.choicePanel = CreateGameObject("Choice Panel", controllerObject.transform);
            controller.choiceDimOverlay = CreateGameObject("Choice Overlay", controllerObject.transform);
            controller.choiceMashaButton = CreateButton("Choice 1", controller.choicePanel.transform);
            controller.choiceArtemButton = CreateButton("Choice 2", controller.choicePanel.transform);
            controller.choiceLeraButton = CreateButton("Choice 3", controller.choicePanel.transform);
            controller.vnSettingsDimOverlay = CreateGameObject("Settings Overlay", controllerObject.transform);
            controller.vnSettingsPanel = CreateGameObject("Settings Panel", controller.vnSettingsDimOverlay.transform);

            controller.backlogDimOverlay = CreateGameObject("Backlog Overlay", controllerObject.transform);
            controller.backlogPanel = CreateGameObject("Backlog Panel", controllerObject.transform);
            controller.backlogPanel.SetActive(false);
            controller.backlogText = CreateTmp("Backlog Text", controller.backlogPanel.transform);
            controller.backlogCloseButton = CreateButton("Backlog Close", controller.backlogPanel.transform);

            controller.manualSaveLoadPanel = CreateSaveLoadPanel(canvasObject.transform);

            DialogueSceneRegistry registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
            createdObjects.Add(registry);
            registry.scenes.Add(startScene);
            foreach (DialogueSceneData scene in additionalScenes)
            {
                registry.scenes.Add(scene);
            }
            controller.sceneData = startScene;
            controller.sceneRegistry = registry;

            string readKey = "hif_wheel_test_seen_" + Path.GetRandomFileName().Replace(".", string.Empty);
            playerPrefsKeys.Add(readKey);
            SetPrivate(controller, "readHistory", new DialogueReadHistory(readKey));

            return new RuntimeContext(controller, gameState, settings, startScene);
        }

        private ManualSaveLoadPanel CreateSaveLoadPanel(Transform canvasTransform)
        {
            GameObject panelObject = new GameObject("Wheel ManualSaveLoadPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            createdObjects.Add(panelObject);
            ManualSaveLoadPanel panel = panelObject.AddComponent<ManualSaveLoadPanel>();

            RectTransform window = new GameObject("Wheel SaveLoad Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.SetParent(panelObject.transform, false);
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(1680f, 960f);
            panel.windowRect = window;
            panel.canvasGroup = panelObject.AddComponent<CanvasGroup>();

            panel.confirmationRoot = CreateGameObject("Wheel SaveLoad Confirmation", window);
            panel.confirmationRoot.AddComponent<Image>();
            panel.confirmationRoot.SetActive(false);
            panel.confirmationText = CreateTmp("Confirmation Text", panel.confirmationRoot.transform);
            panel.confirmationYesButton = CreateButton("Confirmation Yes", panel.confirmationRoot.transform);
            panel.confirmationNoButton = CreateButton("Confirmation No", panel.confirmationRoot.transform);

            panel.slotViews = new ManualSaveSlotView[0];
            panel.gameObject.SetActive(false);
            return panel;
        }

        private DialogueSceneData CreateLinearScene(string sceneId, params string[] texts)
        {
            DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            createdObjects.Add(scene);
            scene.sceneId = sceneId;
            scene.displayName = sceneId;
            scene.lines = new List<DialogueLine>();
            for (int index = 0; index < texts.Length; index++)
            {
                scene.lines.Add(new DialogueLine
                {
                    lineId = $"line-{index}",
                    speaker = "Narrator",
                    text = texts[index]
                });
            }
            scene.choices = new List<DialogueChoice>();
            return scene;
        }

        private GameObject CreateGameObject(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
            }
            createdObjects.Add(obj);
            return obj;
        }

        private TextMeshProUGUI CreateTmp(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<TextMeshProUGUI>();
        }

        private Image CreateImage(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<Image>();
        }

        private Button CreateButton(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            CreateTmp("Label", obj.transform).text = name;
            return obj.GetComponent<Button>();
        }

        private static FieldInfo GetPrivateField(System.Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {type.Name}.{fieldName}.");
            return field;
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void DestroyExistingSingletons()
        {
            DestroyAll<VNDialogueController>();
            DestroyAll<GameState>();
            DestroyAll<SettingsManager>();
            DestroyAll<AudioManager>();
            DestroyAll<SaveManager>();
            DestroyAll<SceneFlowManager>();
        }

        private static void DestroyAll<T>() where T : UnityEngine.Object
        {
            foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }

        private sealed class RuntimeContext
        {
            public RuntimeContext(VNDialogueController controller, GameState gameState, SettingsManager settings, DialogueSceneData startScene)
            {
                Controller = controller;
                GameState = gameState;
                Settings = settings;
                StartScene = startScene;
            }

            public VNDialogueController Controller { get; }
            public GameState GameState { get; }
            public SettingsManager Settings { get; }
            public DialogueSceneData StartScene { get; }
        }
    }
}
