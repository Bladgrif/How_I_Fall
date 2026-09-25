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
    /// Regression for the approved root Game Menu interaction semantics: the
    /// visible cyan highlight is owned by the view's visual-focus state, never
    /// by the raw EventSystem selection alone. Pointer hover transfers both the
    /// logical selection and the highlight; pointer exit into empty space must
    /// fade the highlight out completely while the logical selection remains;
    /// keyboard/controller navigation immediately restores exactly one visible
    /// highlight; reopening the root restores Save as the deterministic focus.
    /// The highlight transition itself is a short unscaled-time fade, not a
    /// hard SetActive switch.
    /// </summary>
    public sealed class GameMenuFocusInteractionPlayModeTests : InputTestFixture
    {
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> playerPrefsKeys = new List<string>();
        private string saveDirectory;

        public override void Setup()
        {
            // InputTestFixture.Setup() resets the whole input runtime; the tests
            // drive pointer/keyboard semantics through ExecuteEvents, so no
            // virtual devices are required here.
            base.Setup();
            DestroyExistingSingletons();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
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
        public IEnumerator Open_SelectsSave_AsSoleVisibleHighlight()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-open", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);

            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton(VNGameMenuAction.Save).gameObject),
                "Root Game Menu must keep Save as the deterministic initial logical focus.");
            AssertFocusState(view, VNGameMenuAction.Save, expectVisible: true);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                "Root open must present exactly one visible interaction highlight.");
        }

        [UnityTest]
        public IEnumerator PointerEnterQuit_TransfersSelection_AndSoleVisibleHighlight()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-hover", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            Button quit = view.GetButton(VNGameMenuAction.Quit);
            ExecuteEvents.Execute<IPointerEnterHandler>(quit.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quit.gameObject),
                "Pointer hover must transfer the logical EventSystem selection to the hovered row.");

            CanvasGroup quitMarker = GetFocusMarkerGroup(view, VNGameMenuAction.Quit);
            Assert.That(quitMarker.gameObject.activeSelf, Is.True, "Hovered row must own the visible highlight immediately.");
            view.AdvanceFocusFade(0.03f);
            Assert.That(quitMarker.alpha, Is.GreaterThan(0f).And.LessThan(1f),
                "Focus highlight must fade in over the short transition instead of snapping in.");

            SettleFocus(view);
            AssertFocusState(view, VNGameMenuAction.Quit, expectVisible: true);
            AssertFocusState(view, VNGameMenuAction.Save, expectVisible: false);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                "Pointer hover must leave exactly one visible Game Menu highlight.");
        }

        [UnityTest]
        public IEnumerator PointerExitQuitToEmptySpace_ClearsVisibleHighlight_KeepsLogicalSelection()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-exit", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            Button quit = view.GetButton(VNGameMenuAction.Quit);
            ExecuteEvents.Execute<IPointerEnterHandler>(quit.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            SettleFocus(view);

            ExecuteEvents.Execute<IPointerExitHandler>(quit.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quit.gameObject),
                "Pointer exit may keep the logical selection so Submit/navigation stays deterministic.");

            SettleFocus(view);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(0),
                "Pointer exit into empty space must not leave any visibly highlighted root row.");
            AssertFocusState(view, VNGameMenuAction.Quit, expectVisible: false);
            AssertFocusState(view, VNGameMenuAction.Save, expectVisible: false);
        }

        [UnityTest]
        public IEnumerator PointerEnterBack_BecomesSoleVisibleHighlight()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-back", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            Button back = view.GetButton(VNGameMenuAction.Back);
            ExecuteEvents.Execute<IPointerEnterHandler>(back.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(back.gameObject),
                "Pointer hover must make Back the logical selection.");

            SettleFocus(view);
            AssertFocusState(view, VNGameMenuAction.Back, expectVisible: true);
            AssertFocusState(view, VNGameMenuAction.Save, expectVisible: false);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                "Exactly one root row may stay visibly highlighted after moving between rows.");
        }

        [UnityTest]
        public IEnumerator KeyboardNavigationAfterPointerExit_RestoresSingleVisibleHighlight()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-keyboard", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            Button quit = view.GetButton(VNGameMenuAction.Quit);
            ExecuteEvents.Execute<IPointerEnterHandler>(quit.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute<IPointerExitHandler>(quit.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            SettleFocus(view);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(0), "Precondition: pointer exit cleared the visible highlight.");

            ExecuteEvents.Execute<IMoveHandler>(EventSystem.current.currentSelectedGameObject,
                new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down, moveVector = Vector2.down },
                ExecuteEvents.moveHandler);
            GameObject navigated = EventSystem.current.currentSelectedGameObject;
            Assert.That(navigated, Is.Not.EqualTo(quit.gameObject),
                "Keyboard navigation after pointer exit must move the logical selection.");
            Assert.That(navigated.GetComponent<Button>(), Is.Not.Null,
                "Keyboard navigation must land on a Game Menu root row.");

            SettleFocus(view);
            Assert.That(navigated.transform.Find("Focus Marker").gameObject.activeSelf, Is.True,
                "The navigated row must become the sole visible keyboard/controller highlight.");
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                "Keyboard navigation after pointer exit must restore exactly one visible highlight.");
        }

        [UnityTest]
        public IEnumerator RootCloseReopen_RestoresSaveAsDeterministicFocus()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-reopen", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            ExecuteEvents.Execute<IPointerEnterHandler>(view.GetButton(VNGameMenuAction.Quit).gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            SettleFocus(view);

            Assert.That(context.Controller.GameMenuController.Close(), Is.True, "Game Menu must close for the reopen proof.");
            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(0),
                "Closed Game Menu must not keep any visible focus highlight.");

            Assert.That(context.Controller.GameMenuController.Open(), Is.True, "Game Menu must reopen for the reopen proof.");
            SettleFocus(view);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton(VNGameMenuAction.Save).gameObject),
                "Reopening the root must restore Save as the deterministic logical focus.");
            AssertFocusState(view, VNGameMenuAction.Save, expectVisible: true);
            Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                "Reopening the root must present exactly one visible highlight on Save.");
        }

        [UnityTest]
        public IEnumerator FocusFade_ProgressesOverUnscaledTime_AndSettlesAtFullAlpha()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-fade", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            ExecuteEvents.Execute<IPointerEnterHandler>(view.GetButton(VNGameMenuAction.Back).gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            CanvasGroup backMarker = GetFocusMarkerGroup(view, VNGameMenuAction.Back);
            Assert.That(backMarker.alpha, Is.LessThan(1f),
                "A just-activated highlight must start below full alpha: the transition is a fade, not SetActive-only.");

            Time.timeScale = 0f;
            try
            {
                // The Game Menu freezes the Reading frame; the highlight fade
                // must still complete because it is driven by unscaled time.
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(backMarker.alpha, Is.EqualTo(1f),
                    "Focus fade must settle at full alpha even while the game is paused (unscaled time).");
                Assert.That(view.VisibleFocusMarkerCount, Is.EqualTo(1),
                    "The settled fade must leave exactly one visible highlight.");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator ButtonSelectionTint_NeverPresentsAStaleSelectedPlate()
        {
            RuntimeContext context = CreateContext(CreateLinearScene("gm-focus-tint", "А", "B"));
            yield return null;
            CompleteCurrentLine(context.Controller);
            VNGameMenuView view = OpenGameMenu(context);
            SettleFocus(view);

            foreach (VNGameMenuAction action in new[]
                     {
                         VNGameMenuAction.Save, VNGameMenuAction.Load, VNGameMenuAction.Preferences,
                         VNGameMenuAction.MainMenu, VNGameMenuAction.Quit, VNGameMenuAction.Back, VNGameMenuAction.Return
                     })
            {
                Button button = view.GetButton(action);
                Assert.That(button, Is.Not.Null, $"{action} row is missing.");
                if (action == VNGameMenuAction.Return)
                {
                    Assert.That(button.colors.selectedColor, Is.EqualTo(button.colors.normalColor),
                        "Return must keep its normal base styling with no extra selected tint.");
                }
                else
                {
                    Assert.That(button.colors.selectedColor.a, Is.EqualTo(0f),
                        $"{action} selected tint must stay silent: keyboard focus is expressed by the fading Game Menu visuals only.");
                }
            }
        }

        private static VNGameMenuView OpenGameMenu(RuntimeContext context)
        {
            Assert.That(context.Controller.OpenGameMenu(), Is.True, "Root Game Menu did not open for the focus proof.");
            return context.Controller.GameMenuController.View;
        }

        private static void SettleFocus(VNGameMenuView view)
        {
            view.AdvanceFocusFade(VNGameMenuView.FocusFadeDuration * 2f);
        }

        private static void AssertFocusState(VNGameMenuView view, VNGameMenuAction action, bool expectVisible)
        {
            Button button = view.GetButton(action);
            GameObject marker = button.transform.Find("Focus Marker").gameObject;
            GameObject plate = button.transform.Find("Focus Plate").gameObject;
            Assert.That(marker.activeSelf, Is.EqualTo(expectVisible), $"{action} Focus Marker active state.");
            Assert.That(plate.activeSelf, Is.EqualTo(expectVisible), $"{action} Focus Plate active state.");
            CanvasGroup markerGroup = marker.GetComponent<CanvasGroup>();
            Assert.That(markerGroup.alpha, Is.EqualTo(expectVisible ? 1f : 0f), $"{action} focus fade final alpha.");
        }

        private static CanvasGroup GetFocusMarkerGroup(VNGameMenuView view, VNGameMenuAction action)
        {
            Transform marker = view.GetButton(action).transform.Find("Focus Marker");
            Assert.That(marker, Is.Not.Null, $"{action} Focus Marker is missing.");
            return marker.GetComponent<CanvasGroup>();
        }

        private static void CompleteCurrentLine(VNDialogueController controller)
        {
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.True);
            controller.AdvanceDialogue();
            Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.False);
        }

        private RuntimeContext CreateContext(DialogueSceneData startScene, params DialogueSceneData[] additionalScenes)
        {
            SettingsManager settings = new GameObject("GameMenuFocus SettingsManager").AddComponent<SettingsManager>();
            createdObjects.Add(settings.gameObject);
            settings.settings.autoSave = false;
            settings.settings.autoForward = false;
            settings.settings.textSpeed = 20f;

            AudioManager audio = new GameObject("GameMenuFocus AudioManager").AddComponent<AudioManager>();
            createdObjects.Add(audio.gameObject);

            GameState gameState = new GameObject("GameMenuFocus GameState").AddComponent<GameState>();
            createdObjects.Add(gameState.gameObject);

            GameObject canvasObject = new GameObject("GameMenuFocus Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            createdObjects.Add(canvasObject);

            GameObject eventSystemObject = new GameObject("GameMenuFocus EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            createdObjects.Add(eventSystemObject);

            GameObject controllerObject = new GameObject("GameMenuFocus VNDialogueController");
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

            string readKey = "hif_gm_focus_test_seen_" + Path.GetRandomFileName().Replace(".", string.Empty);
            playerPrefsKeys.Add(readKey);
            SetPrivate(controller, "readHistory", new DialogueReadHistory(readKey));

            return new RuntimeContext(controller, gameState, settings);
        }

        private ManualSaveLoadPanel CreateSaveLoadPanel(Transform canvasTransform)
        {
            GameObject panelObject = new GameObject("GameMenuFocus ManualSaveLoadPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            createdObjects.Add(panelObject);
            ManualSaveLoadPanel panel = panelObject.AddComponent<ManualSaveLoadPanel>();

            RectTransform window = new GameObject("GameMenuFocus SaveLoad Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.SetParent(panelObject.transform, false);
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(1680f, 960f);
            panel.windowRect = window;
            panel.canvasGroup = panelObject.AddComponent<CanvasGroup>();

            panel.confirmationRoot = CreateGameObject("GameMenuFocus SaveLoad Confirmation", window);
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
