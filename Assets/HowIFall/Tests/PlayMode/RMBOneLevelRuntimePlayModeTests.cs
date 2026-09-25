using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Regression for the canonical one-level CloseOrCancel rule: one physical
    /// virtual-RMB press must move the modal stack by exactly one level through
    /// the real runtime Update ownership (VNDialogueController.Update,
    /// VNGameMenuController.Update and ManualSaveLoadPanel.Update may observe the
    /// same frame). The tests never invoke HandleEscapePressed/TryHandleEscape/
    /// VNInputMap.WasPressedThisFrame to drive the stack; they only press the
    /// virtual mouse and let the production Update loop react.
    /// </summary>
    public sealed class RMBOneLevelRuntimePlayModeTests : InputTestFixture
    {
        private Mouse mouse;
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> playerPrefsKeys = new List<string>();
        private string saveDirectory;

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

            if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, true);
            }
            saveDirectory = null;
        }

        [UnityTest]
        public IEnumerator PlainReading_RmbOpensMenu_NextRmbClosesIt_OneLevelPerClick()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("rmb-plain", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            Assert.That(context.Controller.IsGameMenuOpen, Is.False);
            DialogueProbe probe = CaptureDialogueProbe(context);
            Assert.That(context.Controller.dialogueUiRoot.activeSelf, Is.True);

            yield return ClickRightMouse();

            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "First RMB must open the Game Menu.");
            AssertSingleOpenLevel(context, expectMenu: true);
            Assert.That(context.Controller.dialogueUiRoot.activeSelf, Is.True, "Root Game Menu must preserve the Reading shell.");
            Assert.That(context.Controller.IsDialogueShellSuppressed, Is.False);
            AssertDialogueUntouched(context, probe);

            yield return ClickRightMouse();

            Assert.That(context.Controller.IsGameMenuOpen, Is.False, "Second RMB must close the Game Menu.");
            AssertSingleOpenLevel(context, expectMenu: false);
            Assert.That(context.Controller.dialogueUiRoot.activeSelf, Is.True);
            AssertDialogueUntouched(context, probe);
        }

        [UnityTest]
        public IEnumerator PartialTyping_RmbMenuFreezesExactText_ThenResumesSameLine()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("rmb-typing", new string('А', 40), "B"));
            yield return null;
            Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.True);

            yield return ClickRightMouse();
            Assert.That(context.Controller.IsGameMenuOpen, Is.True);
            string pausedText = context.Controller.dialogueText.text;
            string lineId = context.GameState.currentLineId;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(context.Controller.dialogueText.text, Is.EqualTo(pausedText),
                "Typing continued behind the frozen Game Menu frame.");

            yield return ClickRightMouse();
            Assert.That(context.Controller.IsGameMenuOpen, Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(context.Controller.dialogueText.text.Length, Is.GreaterThan(pausedText.Length),
                "Typing did not resume after closing Game Menu.");
            Assert.That(context.GameState.currentLineId, Is.EqualTo(lineId),
                "Opening and closing Game Menu advanced the dialogue line.");
        }

        [UnityTest]
        public IEnumerator GameMenuLocalQuitConfirmation_RmbClosesOnlyConfirmation_ThenMenu()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("rmb-local-confirm", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            yield return ClickRightMouse();
            Assert.That(context.Controller.IsGameMenuOpen, Is.True);
            VNGameMenuView view = context.Controller.GameMenuController.View;
            view.GetButton(VNGameMenuAction.Quit).onClick.Invoke();
            Assert.That(view.IsConfirmationVisible, Is.True, "Quit must open the Game Menu local confirmation.");
            DialogueProbe probe = CaptureDialogueProbe(context);

            yield return ClickRightMouse();

            Assert.That(view.IsConfirmationVisible, Is.False, "RMB must cancel exactly the local confirmation.");
            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "Game Menu must remain open after the confirmation is cancelled.");
            Assert.That(context.Controller.GameMenuController.IsPresentationVisible, Is.True);
            AssertSingleOpenLevel(context, expectMenu: true);
            AssertDialogueUntouched(context, probe);

            yield return ClickRightMouse();

            Assert.That(view.IsConfirmationVisible, Is.False);
            Assert.That(context.Controller.IsGameMenuOpen, Is.False, "Next RMB must close exactly the Game Menu.");
            AssertDialogueUntouched(context, probe);
        }

        [UnityTest]
        public IEnumerator EmbeddedSaveLoadSlotConfirmation_RmbUnwindsExactlyOneLevelPerClick()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("rmb-embedded", "A", "B"));
            yield return null;

            saveDirectory = Path.Combine(Application.temporaryCachePath, "hif-rmb-" + Path.GetRandomFileName());
            SaveManager manager = SaveManager.Instance;
            Assert.That(manager, Is.Not.Null);
            manager.ConfigureSaveDirectoryForTests(saveDirectory);
            CompleteCurrentLine(context.Controller);
            Texture2D preview = new Texture2D(2, 2);
            createdObjects.Add(preview);
            Assert.That(manager.SaveSlot(SaveSlotType.Manual, 1, preview), Is.True, "Test setup must occupy manual slot 1.");

            yield return ClickRightMouse();
            Assert.That(context.Controller.IsGameMenuOpen, Is.True);

            context.Controller.GameMenuController.View.GetButton(VNGameMenuAction.Save).onClick.Invoke();
            yield return null;
            ManualSaveLoadPanel panel = context.Controller.manualSaveLoadPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.IsOpen, Is.True, "Save button must open the embedded panel.");
            Assert.That(context.Controller.GameMenuController.IsOpen, Is.True);

            panel.OnSlotSelected(1);
            Assert.That(panel.IsConfirmationOpen, Is.True, "Occupied slot must open the overwrite confirmation.");
            DialogueProbe probe = CaptureDialogueProbe(context);

            yield return ClickRightMouse();

            Assert.That(panel.IsConfirmationOpen, Is.False, "First RMB must close only the slot confirmation.");
            Assert.That(panel.IsOpen, Is.True, "Save panel must remain open.");
            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "Game Menu must remain open.");
            AssertDialogueUntouched(context, probe);

            yield return ClickRightMouse();
            yield return WaitUntilPanelClosed(panel);

            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "Game Menu must remain open after the panel closes.");
            Assert.That(context.Controller.GameMenuController.IsPresentationVisible, Is.True);
            Assert.That(context.Controller.dialogueUiRoot.activeSelf, Is.True, "Closing embedded Save/Load must restore the Reading shell behind root Game Menu.");
            AssertDialogueUntouched(context, probe);

            yield return ClickRightMouse();

            Assert.That(panel.IsOpen, Is.False);
            Assert.That(context.Controller.IsGameMenuOpen, Is.False, "Third RMB must close exactly the Game Menu.");
            AssertDialogueUntouched(context, probe);
        }

        [UnityTest]
        public IEnumerator HistoryRoute_RmbClosesOnlyHistory_AndGameMenuRemains()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("rmb-history", "A", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            yield return ClickRightMouse();
            Assert.That(context.Controller.IsGameMenuOpen, Is.True);
            context.Controller.GameMenuController.View.GetButton(VNGameMenuAction.History).onClick.Invoke();
            yield return null;
            Assert.That(context.Controller.backlogPanel.activeSelf, Is.True, "History button must open the backlog.");
            DialogueProbe probe = CaptureDialogueProbe(context);

            yield return ClickRightMouse();

            Assert.That(context.Controller.backlogPanel.activeSelf, Is.False, "RMB must close exactly History.");
            Assert.That(context.Controller.IsGameMenuOpen, Is.True, "Game Menu must remain open behind History.");
            Assert.That(context.Controller.GameMenuController.IsPresentationVisible, Is.True);
            Assert.That(context.Controller.dialogueUiRoot.activeSelf, Is.True, "Closing History must restore the Reading shell behind root Game Menu.");
            AssertDialogueUntouched(context, probe);

            yield return ClickRightMouse();

            Assert.That(context.Controller.IsGameMenuOpen, Is.False, "Next RMB must close exactly the Game Menu.");
            AssertDialogueUntouched(context, probe);
        }

        private IEnumerator ClickRightMouse()
        {
            Assert.That(mouse != null && mouse.added, Is.True, "Virtual mouse must exist for the whole click.");
            Press(mouse.rightButton);
            yield return null;
            Release(mouse.rightButton);
            yield return null;
        }

        private static IEnumerator WaitUntilPanelClosed(ManualSaveLoadPanel panel)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (panel.IsOpen && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(panel.IsOpen, Is.False, "Save/Load panel must finish its close animation.");
            yield return null;
        }

        private DialogueProbe CaptureDialogueProbe(RuntimeContext context)
        {
            List<DialogueBacklogEntry> backlog = context.Controller.CaptureBacklogSnapshot();
            return new DialogueProbe(
                context.GameState.currentLineId,
                context.Controller.dialogueText.text,
                backlog.Select(entry => entry.text).ToList());
        }

        private static void AssertDialogueUntouched(RuntimeContext context, DialogueProbe probe)
        {
            Assert.That(context.GameState.currentLineId, Is.EqualTo(probe.LineId), "RMB must not advance or rewind the dialogue line.");
            Assert.That(context.Controller.dialogueText.text, Is.EqualTo(probe.DialogueText), "RMB must not rewrite the dialogue text.");
            List<DialogueBacklogEntry> backlog = context.Controller.CaptureBacklogSnapshot();
            Assert.That(backlog.Select(entry => entry.text).ToList(), Is.EqualTo(probe.BacklogTexts), "RMB must not mutate rollback/backlog state.");
        }

        private static void AssertSingleOpenLevel(RuntimeContext context, bool expectMenu)
        {
            ManualSaveLoadPanel panel = context.Controller.manualSaveLoadPanel;
            Assert.That(panel == null || panel.IsOpen, Is.False, "No save/load level may open during plain reading RMB.");
            Assert.That(context.Controller.backlogPanel.activeSelf, Is.False, "History must stay closed.");
            Assert.That(context.Controller.GameMenuController.IsLocalConfirmationOpen, Is.False);
            Assert.That(context.Controller.GameMenuController.IsPresentationVisible, Is.EqualTo(expectMenu), "Exactly the Game Menu level may be open.");
        }

        private RuntimeContext CreateContext(DialogueSceneData startScene, params DialogueSceneData[] additionalScenes)
        {
            SettingsManager settings = new GameObject("RMB SettingsManager").AddComponent<SettingsManager>();
            createdObjects.Add(settings.gameObject);
            settings.settings.autoSave = false;
            settings.settings.autoForward = false;
            settings.settings.textSpeed = 20f;

            AudioManager audio = new GameObject("RMB AudioManager").AddComponent<AudioManager>();
            createdObjects.Add(audio.gameObject);

            GameState gameState = new GameObject("RMB GameState").AddComponent<GameState>();
            createdObjects.Add(gameState.gameObject);

            GameObject canvasObject = new GameObject("RMB Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            createdObjects.Add(canvasObject);

            GameObject eventSystemObject = new GameObject("RMB EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            createdObjects.Add(eventSystemObject);

            GameObject controllerObject = new GameObject("RMB VNDialogueController");
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

            string readKey = "hif_rmb_test_seen_" + Path.GetRandomFileName().Replace(".", string.Empty);
            playerPrefsKeys.Add(readKey);
            SetPrivate(controller, "readHistory", new DialogueReadHistory(readKey));

            return new RuntimeContext(controller, gameState, settings);
        }

        private ManualSaveLoadPanel CreateSaveLoadPanel(Transform canvasTransform)
        {
            GameObject panelObject = new GameObject("RMB ManualSaveLoadPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            createdObjects.Add(panelObject);
            ManualSaveLoadPanel panel = panelObject.AddComponent<ManualSaveLoadPanel>();

            RectTransform window = new GameObject("RMB SaveLoad Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.SetParent(panelObject.transform, false);
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(1680f, 960f);
            panel.windowRect = window;
            panel.canvasGroup = panelObject.AddComponent<CanvasGroup>();

            panel.confirmationRoot = CreateGameObject("RMB SaveLoad Confirmation", window);
            panel.confirmationRoot.AddComponent<Image>();
            panel.confirmationRoot.SetActive(false);
            panel.confirmationText = CreateTmp("Confirmation Text", panel.confirmationRoot.transform);
            panel.confirmationYesButton = CreateButton("Confirmation Yes", panel.confirmationRoot.transform);
            panel.confirmationNoButton = CreateButton("Confirmation No", panel.confirmationRoot.transform);

            panel.slotViews = new ManualSaveSlotView[0];
            panel.gameObject.SetActive(false);
            return panel;
        }

        private static void CompleteCurrentLine(VNDialogueController controller)
        {
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.True);
            controller.AdvanceDialogue();
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.False);
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

        private sealed class DialogueProbe
        {
            public DialogueProbe(string lineId, string dialogueText, List<string> backlogTexts)
            {
                LineId = lineId;
                DialogueText = dialogueText;
                BacklogTexts = backlogTexts;
            }

            public string LineId { get; }
            public string DialogueText { get; }
            public List<string> BacklogTexts { get; }
        }

        private sealed class RuntimeContext
        {
            public RuntimeContext(VNDialogueController controller, GameState gameState, SettingsManager settings)
            {
                Controller = controller;
                GameState = gameState;
                Settings = settings;
            }

            public VNDialogueController Controller { get; }
            public GameState GameState { get; }
            public SettingsManager Settings { get; }
        }
    }
}
