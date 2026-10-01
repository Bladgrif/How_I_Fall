using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    /// <summary>
    /// Regression for the reproduced Save/Load hover-vs-focus ownership defect: on
    /// the unified strip (QS/AS/page numbers) and the delete control, mouse hover
    /// and keyboard/controller selection used to drive byte-identical ColorTint
    /// states on two different entries while Submit activated the EventSystem
    /// selection the UI never showed. The corrected contract: pointer hover
    /// transfers the EventSystem selection so exactly one control owns the strong
    /// selected treatment, Submit acts on that visible owner, pointer exit keeps a
    /// single owner without stale or double highlights, keyboard navigation
    /// restores exactly one selected control, and the selected treatment stays
    /// visible on the already-highlighted active entry while remaining distinct
    /// from the persistent active family/page styling.
    /// </summary>
    public sealed class SaveLoadFocusOwnershipPlayModeTests : InputTestFixture
    {
        private static readonly Color AuthoredPageNormal = new Color(0.045f, 0.075f, 0.11f, 0.48f);
        private static readonly Color AuthoredPageHighlighted = new Color(0.09f, 0.15f, 0.22f, 0.76f);
        private static readonly Color AuthoredDeleteHighlighted = new Color(0.34f, 0.085f, 0.115f, 0.92f);
        private static readonly Color ActivePlateColor = new Color(0.10f, 0.25f, 0.36f, 0.96f);

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> playerPrefsKeys = new List<string>();
        private string saveDirectory;

        public override void Setup()
        {
            // The tests drive pointer/selection semantics through ExecuteEvents, so
            // no virtual input devices are required beyond the reset InputTestFixture.
            base.Setup();
            DestroyExistingSingletons();
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
        public IEnumerator StripButtons_GetOneStrongSelectedLanguage_AndTransferComponent()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            foreach (Button entry in StripEntries(panel))
            {
                Assert.That(entry.GetComponent<PointerEnterSelectionTransfer>(), Is.Not.Null,
                    $"{entry.name} must own the pointer hover selection transfer.");
                Assert.That(entry.colors.selectedColor, Is.EqualTo(entry.colors.highlightedColor),
                    $"{entry.name} hover and selection must share one strong treatment.");
                Assert.That(entry.colors.selectedColor.r, Is.GreaterThan(1.2f),
                    $"{entry.name} selected tint must lift the dark plate visibly instead of multiplying it toward black.");
            }

            Button delete = panel.slotViews[0].deleteButton;
            Assert.That(delete, Is.Not.Null, "The occupied slot must expose its delete control.");
            Assert.That(delete.gameObject.activeSelf, Is.True);
            Assert.That(delete.GetComponent<PointerEnterSelectionTransfer>(), Is.Not.Null,
                "The delete control must own the pointer hover selection transfer.");
            Assert.That(delete.colors.highlightedColor, Is.EqualTo(AuthoredDeleteHighlighted),
                "The delete control keeps its authored red selected language.");
        }

        [UnityTest]
        public IEnumerator KeyboardSelectedPage_ThenPointerEnter_TransfersSelection_AsSoleStrongOwner()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            Button page2 = panel.manualPageButtons[1];
            Button page3 = panel.manualPageButtons[2];
            EventSystem.current.SetSelectedGameObject(page2.gameObject);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(page2.gameObject));

            ExecuteEvents.Execute<IPointerEnterHandler>(page3.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(page3.gameObject),
                "Pointer hover must transfer the EventSystem selection to the hovered strip entry.");

            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(page3.GetComponent<CanvasRenderer>().GetColor().r, Is.GreaterThan(1.2f),
                "The hovered/selected entry must render a visibly lifted plate tint.");
            Assert.That(MaxChannelDiff(page2.GetComponent<CanvasRenderer>().GetColor(), AuthoredPageNormal), Is.LessThan(0.01f),
                "The previously keyboard-selected entry must fall back to the normal tint: no double selected state.");
        }

        [UnityTest]
        public IEnumerator Submit_ActivatesTheVisuallySelectedStripOwner()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            Button page3 = panel.manualPageButtons[2];
            EventSystem.current.SetSelectedGameObject(panel.manualPageButtons[0].gameObject);
            ExecuteEvents.Execute<IPointerEnterHandler>(page3.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return null;

            ExecuteEvents.Execute<ISubmitHandler>(EventSystem.current.currentSelectedGameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;

            Assert.That(panel.CurrentManualPage, Is.EqualTo(3),
                "Submit must activate the strip entry the UI presented as selected.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(page3.gameObject),
                "Submit must leave the activated page as the single EventSystem selection.");
        }

        [UnityTest]
        public IEnumerator PointerExit_KeepsSingleOwner_AndKeyboardNavigationRestoresOneSelection()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            Button page3 = panel.manualPageButtons[2];
            EventSystem.current.SetSelectedGameObject(panel.manualPageButtons[0].gameObject);
            ExecuteEvents.Execute<IPointerEnterHandler>(page3.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute<IPointerExitHandler>(page3.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(page3.gameObject),
                "Pointer exit keeps the transferred selection so Submit and navigation stay deterministic.");
            Assert.That(page3.GetComponent<CanvasRenderer>().GetColor().r, Is.GreaterThan(1.2f),
                "The selected entry keeps exactly one visible selected treatment after pointer exit.");
            Assert.That(MaxChannelDiff(panel.manualPageButtons[0].GetComponent<CanvasRenderer>().GetColor(), AuthoredPageNormal),
                Is.LessThan(0.01f),
                "Pointer exit must not leave a stale highlight on the previously selected entry.");

            ExecuteEvents.Execute<IMoveHandler>(page3.gameObject,
                new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right },
                ExecuteEvents.moveHandler);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.manualPageButtons[3].gameObject),
                "Keyboard navigation after pointer interaction must move the selection.");
            Assert.That(panel.manualPageButtons[3].GetComponent<CanvasRenderer>().GetColor().r, Is.GreaterThan(1.2f),
                "The navigated entry must become the single visibly selected control.");
            Assert.That(MaxChannelDiff(page3.GetComponent<CanvasRenderer>().GetColor(), AuthoredPageNormal), Is.LessThan(0.01f),
                "Keyboard navigation must leave exactly one selected owner behind.");
        }

        [UnityTest]
        public IEnumerator ActivePage_KeepsActiveStyling_AndShowsVisibleSelection()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            Button activePage1 = panel.manualPageButtons[0];
            Assert.That(MaxChannelDiff(activePage1.targetGraphic.color, ActivePlateColor), Is.LessThan(0.01f),
                "Precondition: page 1 is the active strip entry with its persistent plate styling.");
            EventSystem.current.SetSelectedGameObject(activePage1.gameObject);
            yield return new WaitForSecondsRealtime(0.25f);

            Color rendererTint = activePage1.GetComponent<CanvasRenderer>().GetColor();
            Assert.That(rendererTint.r, Is.GreaterThan(1.2f),
                "Keyboard/controller focus on the active entry must stay visible: the plate lift must survive the active styling.");
            Color selectedPlate = activePage1.targetGraphic.color * rendererTint;
            Color restingPlate = activePage1.targetGraphic.color * AuthoredPageNormal;
            Assert.That(selectedPlate.r, Is.GreaterThan(restingPlate.r * 1.7f),
                "The selected active plate must render clearly brighter than the resting active plate.");
        }

        [UnityTest]
        public IEnumerator DeleteHover_TransfersSelection_FromCard_AndNavigationReturns()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            ManualSaveSlotView slot1 = panel.slotViews[0];

            Button card = slot1.button;
            Button delete = slot1.deleteButton;
            Assert.That(card.interactable, Is.True, "Precondition: occupied slot 1 is loadable in Load mode.");
            EventSystem.current.SetSelectedGameObject(card.gameObject);
            yield return null;
            Assert.That(slot1.HasEventSystemFocus, Is.True);

            ExecuteEvents.Execute<IPointerEnterHandler>(delete.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(delete.gameObject),
                "Hovering the delete control must transfer the selection: one strong owner, and Submit would act on it.");
            yield return null;
            yield return null;
            Assert.That(slot1.HasEventSystemFocus, Is.False,
                "The card must not keep focus treatment while the delete control owns the selection.");

            ExecuteEvents.Execute<IMoveHandler>(delete.gameObject,
                new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Up, moveVector = Vector2.up },
                ExecuteEvents.moveHandler);
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(card.gameObject),
                "Keyboard navigation from the delete control must restore the card as the single selected owner.");
        }

        [UnityTest]
        public IEnumerator DisabledCard_NeverTakesSelectionFromPointerHover()
        {
            ManualSaveLoadPanel panel = CreateDefaultLoadContext();
            yield return null;

            ManualSaveSlotView slot1 = panel.slotViews[0];
            ManualSaveSlotView emptySlot2 = panel.slotViews[1];
            Assert.That(emptySlot2.button.interactable, Is.False,
                "Precondition: an empty slot is not loadable and must stay disabled in Load mode.");
            EventSystem.current.SetSelectedGameObject(slot1.button.gameObject);
            ExecuteEvents.Execute<IPointerEnterHandler>(emptySlot2.button.gameObject,
                new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return null;

            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slot1.button.gameObject),
                "Pointer hover on a disabled strip or card control must not move the selection.");
        }

        private ManualSaveLoadPanel CreateDefaultLoadContext()
        {
            GameObject canvasObject = new GameObject("SaveLoadFocus Canvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            createdObjects.Add(canvasObject);

            GameObject eventSystemObject = new GameObject("SaveLoadFocus EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            createdObjects.Add(eventSystemObject);

            SaveManager manager = new GameObject("SaveLoadFocus SaveManager").AddComponent<SaveManager>();
            createdObjects.Add(manager.gameObject);
            saveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_SaveFocus_" + Path.GetRandomFileName().Replace(".", string.Empty));
            Directory.CreateDirectory(saveDirectory);
            manager.ConfigureSaveDirectoryForTests(saveDirectory);

            DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            createdObjects.Add(scene);
            scene.sceneId = "scene_main";
            scene.displayName = "Основная сцена";
            scene.lines.Add(new DialogueLine { lineId = "line_0", text = "First" });
            DialogueSceneRegistry registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
            createdObjects.Add(registry);
            registry.scenes.Add(scene);
            manager.ConfigureRegistry(registry);

            SaveData data = new SaveData
            {
                version = SaveData.CurrentVersion,
                slotType = SaveSlotType.Manual,
                slotIndex = 1,
                createdAtUtc = "2026-09-23T10:00:00.0000000Z",
                sceneId = "scene_main",
                lineId = "line_0",
                lineIndex = 0,
                selectedChoiceIndex = -1,
                choiceResultActive = false,
                pendingNextSceneId = string.Empty,
                previewFileName = "slot_01.png",
                backlogEntries = new List<BacklogEntryData>(),
                selfControl = 5
            };
            File.WriteAllText(manager.GetSlotJsonPath(SaveSlotType.Manual, 1), JsonUtility.ToJson(data, true));
            Assert.That(manager.GetSlot(SaveSlotType.Manual, 1).IsLoadable, "Seeded slot 1 must be loadable for the ownership proof.");

            ManualSaveLoadPanel panel = BuildPanel(canvasObject.transform);
            Assert.That(panel.gameObject.activeSelf, Is.True, "Panel must be open after the default Load context.");
            return panel;
        }

        private ManualSaveLoadPanel BuildPanel(Transform canvasTransform)
        {
            GameObject panelObject = new GameObject("SaveLoadFocus Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            createdObjects.Add(panelObject);
            panelObject.SetActive(false);
            ManualSaveLoadPanel panel = panelObject.AddComponent<ManualSaveLoadPanel>();

            RectTransform window = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            window.SetParent(panelObject.transform, false);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(1680f, 960f);
            panel.windowRect = window;
            panel.canvasGroup = panelObject.AddComponent<CanvasGroup>();
            panel.contentCanvasGroup = window.gameObject.AddComponent<CanvasGroup>();

            // Unified strip with the authored shared-prefab interaction colors.
            GameObject pagination = new GameObject("Manual Pagination", typeof(RectTransform));
            pagination.transform.SetParent(window, false);
            panel.manualPaginationRoot = pagination;
            panel.quickTabButton = CreateStripButton("Quick Tab Button", pagination.transform, true);
            panel.autoTabButton = CreateStripButton("Auto Tab Button", pagination.transform, true);
            panel.previousManualPageButton = CreateStripButton("Previous Page Button", pagination.transform, false);
            panel.nextManualPageButton = CreateStripButton("Next Page Button", pagination.transform, false);
            panel.manualPageButtons = new Button[SaveManager.ManualPageCount];
            for (int page = 1; page <= SaveManager.ManualPageCount; page++)
            {
                panel.manualPageButtons[page - 1] = CreateStripButton($"Manual Page {page}", pagination.transform, false);
            }

            panel.slotViews = new[] { CreateSlotView(window.transform, 1), CreateSlotView(window.transform, 2) };
            panel.gameObject.SetActive(true);
            panel.OpenLoad();
            return panel;
        }

        private static Button CreateStripButton(string name, Transform parent, bool slotTypeTab)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            if (slotTypeTab)
            {
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
                colors.pressedColor = new Color(0.86f, 0.9f, 0.96f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.5f);
                colors.fadeDuration = 0.1f;
            }
            else
            {
                colors.normalColor = AuthoredPageNormal;
                colors.highlightedColor = AuthoredPageHighlighted;
                colors.pressedColor = new Color(0.11f, 0.18f, 0.26f, 0.9f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.04f, 0.055f, 0.075f, 0.28f);
                colors.fadeDuration = 0.12f;
            }

            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        private static ManualSaveSlotView CreateSlotView(Transform parent, int slotIndex)
        {
            GameObject slotObject = new GameObject($"Manual Slot {slotIndex}", typeof(RectTransform));
            slotObject.transform.SetParent(parent, false);
            Image background = slotObject.AddComponent<Image>();
            background.color = new Color(0.052f, 0.071f, 0.1f, 0.97f);
            Outline outline = slotObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.29f, 0.4f, 0.48f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = slotObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;

            ManualSaveSlotView view = slotObject.AddComponent<ManualSaveSlotView>();
            view.button = button;
            view.cardRect = slotObject.GetComponent<RectTransform>();
            view.backgroundImage = background;
            view.cardOutline = outline;

            GameObject deleteObject = new GameObject("Delete Button", typeof(RectTransform), typeof(Image), typeof(Button));
            deleteObject.transform.SetParent(slotObject.transform, false);
            Image deleteImage = deleteObject.GetComponent<Image>();
            deleteImage.color = Color.white;
            Button deleteButton = deleteObject.GetComponent<Button>();
            deleteButton.targetGraphic = deleteImage;
            ColorBlock deleteColors = deleteButton.colors;
            deleteColors.normalColor = new Color(0.055f, 0.068f, 0.085f, 0.78f);
            deleteColors.highlightedColor = AuthoredDeleteHighlighted;
            deleteColors.pressedColor = new Color(0.48f, 0.09f, 0.13f, 1f);
            deleteColors.selectedColor = deleteColors.highlightedColor;
            deleteColors.disabledColor = new Color(0.04f, 0.048f, 0.06f, 0.4f);
            deleteColors.colorMultiplier = 1f;
            deleteColors.fadeDuration = 0.1f;
            deleteButton.colors = deleteColors;
            view.deleteButton = deleteButton;
            return view;
        }

        private static IEnumerable<Button> StripEntries(ManualSaveLoadPanel panel)
        {
            foreach (Button entry in new[] { panel.quickTabButton, panel.autoTabButton })
            {
                if (entry != null && entry.gameObject.activeSelf)
                {
                    yield return entry;
                }
            }

            foreach (Button page in panel.manualPageButtons)
            {
                if (page != null && page.gameObject.activeSelf)
                {
                    yield return page;
                }
            }
        }

        private static float MaxChannelDiff(Color left, Color right)
        {
            return Mathf.Max(
                Mathf.Abs(left.r - right.r),
                Mathf.Max(Mathf.Abs(left.g - right.g), Mathf.Max(Mathf.Abs(left.b - right.b), Mathf.Abs(left.a - right.a))));
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
    }
}
