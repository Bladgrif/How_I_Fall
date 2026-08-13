using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class GameMenuSmokeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("How I Fall/Tests/Run Game Menu Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Game Menu smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        VerifyActionSetsAndResponsiveStructure();
        VerifyResponsiveEmbeddedSaveLoadLayout();
        VerifyConfirmationPresentationContract();
        VerifyEscapeBlockingAndQuickMenuOwnership();
        VerifyExistingPanelRoutingAndConfirmations();
        VerifySaveAndPersistentStateIsolation();
    }

    private static void VerifyResponsiveEmbeddedSaveLoadLayout()
    {
        Vector2[] resolutions =
        {
            new Vector2(1280f, 720f),
            new Vector2(1920f, 1080f),
            new Vector2(2560f, 1440f),
            new Vector2(3840f, 2160f)
        };
        GameObject canvasObject = new GameObject("Phase 5 Responsive Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject standaloneParent = new GameObject("Standalone Save Load Parent", typeof(RectTransform));
        VNGameMenuSaveLoadAdapter adapter = new VNGameMenuSaveLoadAdapter();
        GameObject panelObject = null;
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = resolutions[0];

            standaloneParent.transform.SetParent(canvasObject.transform, false);
            standaloneParent.SetActive(false);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HowIFall/Prefabs/UI/ManualSaveLoadPanel.prefab");
            Require(prefab != null, "ManualSaveLoadPanel prefab is missing for responsive Phase 5 validation.");
            panelObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, standaloneParent.transform);
            Require(panelObject != null, "ManualSaveLoadPanel prefab could not be instantiated for responsive validation.");
            panelObject.SetActive(false);
            ManualSaveLoadPanel panel = panelObject.GetComponent<ManualSaveLoadPanel>();
            Require(panel != null && panel.windowRect != null && panel.slotViews != null && panel.slotViews.Length == SaveManager.SlotCount,
                "Responsive validation did not receive the existing six-slot ManualSaveLoadPanel.");

            RectTransform originalRect = panelObject.GetComponent<RectTransform>();
            originalRect.anchorMin = new Vector2(0.13f, 0.17f);
            originalRect.anchorMax = new Vector2(0.87f, 0.83f);
            originalRect.anchoredPosition = new Vector2(17f, -23f);
            originalRect.sizeDelta = new Vector2(-31f, -47f);
            int originalSibling = originalRect.GetSiblingIndex();
            bool closeWasActive = panel.closeButton != null && panel.closeButton.gameObject.activeSelf;

            VNGameMenuView view = VNGameMenuView.Create(canvasObject.transform);
            Require(view != null && view.SaveLoadContentHost != null, "Game Menu Save/Load content host was not created.");
            view.SetReplayMode(false);
            view.SetVisible(true);
            view.SetSaveLoadSection(VNGameMenuAction.Save);
            Require(adapter.Mount(panel, view.SaveLoadContentHost), "ManualSaveLoadPanel could not be mounted for responsive validation.");
            panelObject.SetActive(true);
            Require(panel.closeButton == null || !panel.closeButton.gameObject.activeSelf,
                "Redundant standalone Save/Load Back control remained visible in embedded mode.");

            float? previewAspect = null;
            foreach (Vector2 resolution in resolutions)
            {
                canvasRect.sizeDelta = resolution;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.SaveLoadContentHost);
                adapter.RefreshLayout();
                LayoutRebuilder.ForceRebuildLayoutImmediate(adapter.EmbeddedRoot);
                Canvas.ForceUpdateCanvases();

                Rect contentBounds = GetRectInSpace(view.SaveLoadContentHost, view.SaveLoadContentHost);
                Rect embeddedBounds = GetRectInSpace(view.SaveLoadContentHost, adapter.EmbeddedRoot);
                Require(IsFinitePositive(contentBounds) && IsFinitePositive(embeddedBounds),
                    $"{resolution}: resolution change produced invalid/offscreen embedded bounds.");
                Require(Contains(contentBounds, embeddedBounds),
                    $"{resolution}: embedded Save/Load root exceeds the Game Menu content area.");

                RectTransform gameMenuWindow = view.transform.Find("Game Menu Window") as RectTransform;
                RectTransform navigation = gameMenuWindow != null ? gameMenuWindow.Find("Navigation") as RectTransform : null;
                Require(gameMenuWindow != null && navigation != null, $"{resolution}: Game Menu navigation geometry is missing.");
                Rect navigationBounds = GetRectInSpace(view.transform as RectTransform, navigation);
                Rect contentInMenu = GetRectInSpace(view.transform as RectTransform, view.SaveLoadContentHost);
                Require(!Overlaps(navigationBounds, contentInMenu),
                    $"{resolution}: embedded Save/Load content overlaps Game Menu navigation.");

                RectTransform[] slotRects = panel.slotViews
                    .Where(slot => slot != null && slot.gameObject.activeSelf)
                    .Select(slot => slot.cardRect != null ? slot.cardRect : slot.transform as RectTransform)
                    .ToArray();
                Require(slotRects.Length == 6, $"{resolution}: embedded Save/Load does not expose exactly six visible slots.");
                foreach (RectTransform slotRect in slotRects)
                {
                    Rect slotInContent = GetRectInSpace(view.SaveLoadContentHost, slotRect);
                    Require(IsFinitePositive(slotInContent) && Contains(contentBounds, slotInContent),
                        $"{resolution}: visible slot '{slotRect.name}' exceeds embedded content bounds.");
                    Require(!Overlaps(navigationBounds, GetRectInSpace(view.transform as RectTransform, slotRect)),
                        $"{resolution}: visible slot '{slotRect.name}' overlaps Game Menu navigation.");
                }

                GridLayoutGroup grid = panel.GetComponentInChildren<GridLayoutGroup>(true);
                Require(grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == 3,
                    $"{resolution}: existing Save/Load grid is not constrained to three columns.");
                Require(CountDistinctCenters(slotRects, adapter.EmbeddedRoot, horizontal: true) == 3
                    && CountDistinctCenters(slotRects, adapter.EmbeddedRoot, horizontal: false) == 2,
                    $"{resolution}: six slots do not fit as three columns by two rows.");

                var controls = new[]
                {
                    panel.titleText != null ? panel.titleText.rectTransform : null,
                    panel.manualTabButton != null ? panel.manualTabButton.transform as RectTransform : null,
                    panel.autoTabButton != null ? panel.autoTabButton.transform as RectTransform : null,
                    panel.quickTabButton != null ? panel.quickTabButton.transform as RectTransform : null
                }.Where(rect => rect != null && rect.gameObject.activeSelf);
                foreach (RectTransform control in controls)
                {
                    Rect controlBounds = GetRectInSpace(view.SaveLoadContentHost, control);
                    Require(IsFinitePositive(controlBounds) && Contains(contentBounds, controlBounds),
                        $"{resolution}: embedded header/tab control '{control.name}' exceeds content bounds.");
                }

                ManualSaveSlotView firstSlot = panel.slotViews[0];
                Rect previewBounds = GetRectInSpace(view.SaveLoadContentHost, firstSlot.previewImage.rectTransform);
                Require(IsFinitePositive(previewBounds), $"{resolution}: screenshot preview has invalid geometry.");
                float currentAspect = previewBounds.width / previewBounds.height;
                if (previewAspect.HasValue)
                {
                    Require(Mathf.Abs(currentAspect - previewAspect.Value) < 0.01f,
                        $"{resolution}: responsive embedding distorted screenshot preview aspect.");
                }
                else
                {
                    previewAspect = currentAspect;
                }
            }

            panelObject.SetActive(false);
            adapter.Unmount();
            Require(panelObject.transform.parent == standaloneParent.transform
                && originalRect.anchorMin == new Vector2(0.13f, 0.17f)
                && originalRect.anchorMax == new Vector2(0.87f, 0.83f)
                && originalRect.anchoredPosition == new Vector2(17f, -23f)
                && originalRect.sizeDelta == new Vector2(-31f, -47f)
                && originalRect.GetSiblingIndex() == originalSibling,
                "Detaching embedded Save/Load did not restore its original parent/layout.");
            Require(panel.closeButton == null || panel.closeButton.gameObject.activeSelf == closeWasActive,
                "Detaching embedded Save/Load did not restore its standalone Back control.");
        }
        finally
        {
            adapter.Unmount();
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifyConfirmationPresentationContract()
    {
        Vector2[] resolutions =
        {
            new Vector2(1280f, 720f),
            new Vector2(1920f, 1080f),
            new Vector2(2560f, 1440f),
            new Vector2(3840f, 2160f)
        };
        GameObject canvasObject = new GameObject("Phase 6 Confirmation Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject eventSystemObject = new GameObject(
            "Phase 6 Confirmation Event System",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = resolutions[0];
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
            VNGameMenuView view = VNGameMenuView.Create(canvasObject.transform);
            Require(view != null, "Phase 6 confirmation view could not be created.");
            view.SetVisible(true);
            view.ShowConfirmation("Confirm destructive action?");

            Require(view.IsConfirmationVisible, "Game Menu confirmation did not open.");
            Require(eventSystem.currentSelectedGameObject == view.ConfirmationNoButton.gameObject,
                "Destructive confirmation must default keyboard focus to Cancel.");
            Require(view.ConfirmationYesButton.targetGraphic is Image yesImage
                && view.ConfirmationNoButton.targetGraphic is Image noImage
                && yesImage.color.r > yesImage.color.b
                && noImage.color.b > noImage.color.r,
                "Confirmation actions must keep the shared red destructive / navy cancel presentation.");

            RectTransform confirmationWindow = view.transform.Find("Game Menu Confirmation/Confirmation Window") as RectTransform;
            Require(confirmationWindow != null, "Game Menu confirmation window is missing.");
            foreach (Vector2 resolution in resolutions)
            {
                canvasRect.sizeDelta = resolution;
                Canvas.ForceUpdateCanvases();
                Rect rootBounds = GetRectInSpace(view.transform as RectTransform, view.transform as RectTransform);
                Rect confirmationBounds = GetRectInSpace(view.transform as RectTransform, confirmationWindow);
                Require(IsFinitePositive(confirmationBounds) && Contains(rootBounds, confirmationBounds),
                    $"{resolution}: confirmation is not fully contained in the Game Menu safe area.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifyActionSetsAndResponsiveStructure()
    {
        GameObject canvasObject = new GameObject("Game Menu View Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            VNGameMenuView view = VNGameMenuView.Create(canvasObject.transform);
            Require(view != null, "Runtime Game Menu view was not created.");
            Require(view.GetComponentsInChildren<Canvas>(true).Length == 0, "Game Menu must reuse the VN Canvas instead of creating another Canvas.");
            Require(view.GetComponent<Image>() != null && view.GetComponent<Image>().raycastTarget,
                "Full-screen Game Menu root must block clicks to dialogue, choices, and Quick Menu underneath.");

            view.SetReplayMode(false);
            AssertVisibleActions(view, new[]
            {
                VNGameMenuAction.Save, VNGameMenuAction.Load, VNGameMenuAction.Preferences,
                VNGameMenuAction.MainMenu, VNGameMenuAction.Quit, VNGameMenuAction.Return
            });
            AssertLabel(view, VNGameMenuAction.Save, "Сохранить");
            AssertLabel(view, VNGameMenuAction.Load, "Загрузить");
            AssertLabel(view, VNGameMenuAction.Preferences, "Настройки");
            AssertLabel(view, VNGameMenuAction.MainMenu, "Главное меню");
            AssertLabel(view, VNGameMenuAction.Quit, "Выйти");
            AssertLabel(view, VNGameMenuAction.Return, "Вернуться");
            Require(!view.IsActionVisible(VNGameMenuAction.History), "History leaked into the normal Game Menu.");
            Require(!view.IsActionVisible(VNGameMenuAction.Characters), "Characters leaked into the normal Game Menu.");

            view.SetReplayMode(true);
            AssertVisibleActions(view, new[]
            {
                VNGameMenuAction.Preferences, VNGameMenuAction.History,
                VNGameMenuAction.EndReplay, VNGameMenuAction.Quit, VNGameMenuAction.Return
            });
            AssertLabel(view, VNGameMenuAction.EndReplay, "Завершить повтор");
            Require(!view.IsActionVisible(VNGameMenuAction.Save)
                && !view.IsActionVisible(VNGameMenuAction.Load)
                && !view.IsActionVisible(VNGameMenuAction.Characters)
                && !view.IsActionVisible(VNGameMenuAction.MainMenu),
                "Replay leaked campaign-only navigation actions.");

            RectTransform window = view.transform.Find("Game Menu Window") as RectTransform;
            RectTransform navigation = window != null ? window.Find("Navigation") as RectTransform : null;
            RectTransform primaryActions = navigation != null ? navigation.Find("Primary Actions") as RectTransform : null;
            RectTransform returnArea = navigation != null ? navigation.Find("Return Area") as RectTransform : null;
            Require(window != null && navigation != null && primaryActions != null && returnArea != null,
                "Responsive compact Game Menu navigation structure is missing.");
            Require(window.anchorMin.x > 0f && window.anchorMax.x < 1f && window.anchorMin.y > 0f && window.anchorMax.y < 1f,
                "Game Menu window must use proportional safe margins.");
            float navigationWidth = navigation.anchorMax.x - navigation.anchorMin.x;
            Require(navigationWidth >= 0.25f && navigationWidth <= 0.30f,
                $"Game Menu navigation width must remain compact (25-30%); actual={navigationWidth:0.00}.");
            Require(primaryActions.GetComponent<VerticalLayoutGroup>() != null,
                "Game Menu navigation must use deterministic layout instead of pixel-coordinate button placement.");
            Require(window.Find("Context Area") == null,
                "Game Menu retained the decorative empty Context Area placeholder.");
            RectTransform saveLoadHost = view.SaveLoadContentHost;
            Require(saveLoadHost != null && !view.IsSaveLoadContentVisible,
                "Save/Load content host must exist but remain hidden until a section is opened.");
            Require(saveLoadHost.anchorMin.x > navigation.anchorMax.x && saveLoadHost.anchorMax.x == 1f,
                "Save/Load content host must occupy only the right side of the shared shell.");
            Require(view.GetComponentsInChildren<TextMeshProUGUI>(true).All(text => text.text != "НАВИГАЦИЯ"
                && text.text != "Выберите раздел. Esc возвращает к игре."),
                "Game Menu retained placeholder navigation copy.");
            Require(view.GetButton(VNGameMenuAction.Return).transform.IsChildOf(returnArea),
                "Return must remain visually separated in the bottom navigation area.");
            Require(returnArea.anchorMax.y < primaryActions.anchorMin.y,
                "Return area overlaps the primary navigation block.");
            Require(view.GetButton(VNGameMenuAction.Save).colors.highlightedColor
                != view.GetButton(VNGameMenuAction.Save).colors.normalColor,
                "Game Menu hover feedback is not visually distinct.");

            view.SetReplayMode(false);
            view.SetSaveLoadSection(VNGameMenuAction.Save);
            Require(view.IsSaveLoadContentVisible && view.IsActionActive(VNGameMenuAction.Save) && !view.IsActionActive(VNGameMenuAction.Load),
                "Save section did not expose the shared content area and active navigation state.");
            Require(view.GetButton(VNGameMenuAction.Save).interactable
                && view.GetButton(VNGameMenuAction.Load).interactable
                && view.GetButton(VNGameMenuAction.Preferences).interactable
                && view.GetButton(VNGameMenuAction.MainMenu).interactable
                && view.GetButton(VNGameMenuAction.Quit).interactable
                && view.GetButton(VNGameMenuAction.Return).interactable,
                "Embedded Save/Load did not retain the persistent Game Menu navigation.");
            view.SetSaveLoadSection(VNGameMenuAction.Load, confirmationOpen: true);
            Require(!view.GetButton(VNGameMenuAction.Save).interactable
                && !view.GetButton(VNGameMenuAction.Load).interactable
                && !view.GetButton(VNGameMenuAction.Preferences).interactable
                && !view.GetButton(VNGameMenuAction.MainMenu).interactable
                && !view.GetButton(VNGameMenuAction.Quit).interactable
                && !view.GetButton(VNGameMenuAction.Return).interactable,
                "Nested confirmation did not block the underlying Game Menu navigation.");
            view.SetSaveLoadSection(null);
            Require(!view.IsSaveLoadContentVisible
                && !view.IsActionActive(VNGameMenuAction.Save)
                && view.GetButton(VNGameMenuAction.Preferences).interactable,
                "Closing Save/Load did not restore ordinary Game Menu navigation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void VerifyEscapeBlockingAndQuickMenuOwnership()
    {
        Harness harness = CreateHarness("Game Menu Escape Harness");
        try
        {
            int lineBefore = GetPrivate<int>(harness.Dialogue, "currentLineIndex");
            Require(harness.Dialogue.HandleEscapePressed(), "Stable ordinary dialogue Escape was not handled.");
            Require(harness.Menu.IsOpen && harness.Menu.IsPresentationVisible, "Stable ordinary dialogue Escape did not open Game Menu.");
            Require(!harness.Dialogue.CanAdvanceDialogue, "Game Menu did not block dialogue advance.");
            Require(harness.Dialogue.IsDialogueShellSuppressed && !harness.Dialogue.dialogueUiRoot.activeSelf,
                "Game Menu left the ordinary dialogue shell visible beneath its presentation.");
            Require(GetPrivate<bool>(harness.QuickMenu, "hiddenByGameMenuModal"), "Game Menu did not acquire its Quick Menu blocker.");
            Require(!harness.QuickRoot.activeSelf, "Quick Menu remained visible under Game Menu.");

            Require(harness.Dialogue.HandleEscapePressed(), "Second Escape was not handled.");
            Require(!harness.Menu.IsOpen, "Second Escape did not close Game Menu.");
            Require(!harness.Dialogue.IsDialogueShellSuppressed && harness.Dialogue.dialogueUiRoot.activeSelf,
                "Closing Game Menu did not restore the ordinary dialogue shell it suppressed.");
            Require(GetPrivate<int>(harness.Dialogue, "currentLineIndex") == lineBefore, "Open/Return advanced the dialogue line.");
            Require(!GetPrivate<bool>(harness.QuickMenu, "hiddenByGameMenuModal"), "Closing Game Menu did not remove its own blocker.");

            harness.QuickMenu.SetPlayerInterfaceHidden(true);
            Require(harness.Dialogue.HandleEscapePressed() && harness.Menu.IsOpen, "Game Menu did not reopen for blocker composition test.");
            Require(harness.Dialogue.HandleEscapePressed() && !harness.Menu.IsOpen, "Game Menu did not close for blocker composition test.");
            Require(!harness.QuickRoot.activeSelf, "Closing Game Menu overrode the H/clean-view Quick Menu blocker.");
            harness.QuickMenu.SetPlayerInterfaceHidden(false);

            SetPrivate(harness.Dialogue, "isInterfaceHidden", true);
            harness.QuickMenu.SetPlayerInterfaceHidden(true);
            Require(harness.Dialogue.HandleEscapePressed(), "Hidden-interface Escape was not handled.");
            Require(!harness.Dialogue.IsInterfaceHidden && !harness.Menu.IsOpen,
                "One Escape restored clean view and incorrectly opened Game Menu on the same press.");

            GameObject specialOwner = new GameObject("Blocking Special Owner");
            try
            {
                Require(harness.Dialogue.TryEnterSpecialMode(specialOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease),
                    "BlockingExclusive setup failed.");
                Require(harness.Dialogue.HandleEscapePressed() && !harness.Menu.IsOpen,
                    "BlockingExclusive allowed Game Menu to open.");
                Require(harness.Dialogue.ExitSpecialMode(lease), "BlockingExclusive cleanup failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(specialOwner);
            }

            CharacterHubController hub = harness.Dialogue.gameObject.AddComponent<CharacterHubController>();
            hub.panel = new GameObject("Open Character Hub");
            hub.panel.transform.SetParent(harness.Canvas.transform, false);
            hub.panel.SetActive(true);
            harness.Dialogue.characterHubController = hub;
            Require(harness.Dialogue.HandleEscapePressed(), "Character Hub Escape was not handled.");
            Require(!hub.IsOpen && !harness.Menu.IsOpen, "Character Hub Escape also opened Game Menu on the same press.");

            MethodInfo quickMenuRoute = typeof(VNQuickMenu).GetMethod("HandleMenuAction", PrivateInstance);
            Require(quickMenuRoute != null, "Quick Menu Menu route was not found.");
            Require(harness.QuickMenu.historyButton != null && harness.QuickMenu.historyButton.gameObject.activeSelf,
                "Normal Game Menu cleanup removed Quick Menu History access.");
            Require(harness.QuickMenu.charactersButton != null
                && !harness.QuickMenu.charactersButton.transform.IsChildOf(harness.QuickRoot.transform),
                "Characters access must use the dedicated launcher outside the Quick Menu strip.");

            quickMenuRoute.Invoke(harness.QuickMenu, null);
            Require(harness.Menu.IsOpen, "Quick Menu Menu route did not open the new Game Menu.");
            harness.Menu.Close();

        }
        finally
        {
            harness.Dispose();
        }
    }

    private static void VerifyExistingPanelRoutingAndConfirmations()
    {
        Harness harness = CreateHarness("Game Menu Routing Harness");
        SaveManager saveManagerBefore = SaveManager.Instance;
        try
        {
            GameObject legacyPanel = new GameObject("Legacy Preferences Context", typeof(RectTransform));
            legacyPanel.transform.SetParent(harness.Canvas.transform, false);
            var adapter = new VNPreferencesAdapter(null, legacyPanel, null, null, null, null, null, null, null, null, null);
            var preferences = new PreferencesController(
                new FakePreferencesService(),
                adapter,
                onClosed: () => typeof(VNDialogueController).GetMethod("ResumeAfterPreferencesClosed", PrivateInstance)?.Invoke(harness.Dialogue, null));
            preferences.Initialize();
            SetPrivate(harness.Dialogue, "preferencesController", preferences);

            Require(harness.Menu.Open(), "Game Menu did not open for Preferences routing.");
            harness.Menu.View.GetButton(VNGameMenuAction.Preferences).onClick.Invoke();
            Require(preferences.IsOpen && adapter.SharedView.IsVisible, "Game Menu did not open the existing SharedPreferencesView.");
            Require(harness.Menu.IsOpen && !harness.Menu.IsPresentationVisible, "Preferences did not become the top owner over Game Menu.");
            Require(harness.Dialogue.IsDialogueShellSuppressed && !harness.Dialogue.dialogueUiRoot.activeSelf,
                "Gameplay Preferences left the ordinary dialogue shell visible beneath its presentation.");
            adapter.SharedView.GetButton("back").onClick.Invoke();
            Require(!preferences.IsOpen && harness.Menu.IsPresentationVisible, "Preferences Back did not return to Game Menu.");

            GameObject saveLoadObject = new GameObject("Existing Manual Save Load Panel", typeof(RectTransform));
            saveLoadObject.transform.SetParent(harness.Canvas.transform, false);
            RectTransform saveLoadRect = saveLoadObject.GetComponent<RectTransform>();
            saveLoadRect.anchorMin = new Vector2(0.12f, 0.14f);
            saveLoadRect.anchorMax = new Vector2(0.88f, 0.86f);
            saveLoadObject.SetActive(false);
            ManualSaveLoadPanel saveLoad = saveLoadObject.AddComponent<ManualSaveLoadPanel>();
            GameObject saveLoadWindow = new GameObject("Existing Save Load Window", typeof(RectTransform));
            saveLoadWindow.transform.SetParent(saveLoadObject.transform, false);
            saveLoad.windowRect = saveLoadWindow.GetComponent<RectTransform>();
            saveLoad.windowRect.sizeDelta = new Vector2(1580f, 940f);
            saveLoad.closeButton = CreateButton(saveLoadWindow.transform, "Back");
            GameObject confirmationRoot = new GameObject("Existing Save Confirmation", typeof(RectTransform));
            confirmationRoot.transform.SetParent(saveLoadObject.transform, false);
            confirmationRoot.SetActive(false);
            saveLoad.confirmationRoot = confirmationRoot;
            harness.Dialogue.manualSaveLoadPanel = saveLoad;
            harness.Menu.View.GetButton(VNGameMenuAction.Save).onClick.Invoke();
            Require(saveLoad.IsOpen && saveLoad.IsSaveMode && harness.Menu.IsPresentationVisible,
                "Game Menu Save did not keep the shared Game Menu shell visible.");
            Require(saveLoad.transform.IsChildOf(harness.Menu.View.SaveLoadContentHost)
                && harness.Menu.View.IsSaveLoadContentVisible
                && harness.Menu.View.IsActionActive(VNGameMenuAction.Save),
                "Game Menu Save was not mounted into the shared content area with active Save state.");
            harness.Dialogue.dialogueUiRoot.SetActive(true);
            harness.Menu.View.GetButton(VNGameMenuAction.Load).onClick.Invoke();
            Require(saveLoad.IsOpen && !saveLoad.IsSaveMode
                && harness.Menu.View.IsActionActive(VNGameMenuAction.Load)
                && !harness.Menu.View.IsActionActive(VNGameMenuAction.Save),
                "Save and Load did not switch within the same content area.");
            Require(!harness.Dialogue.dialogueUiRoot.activeSelf,
                "Embedded Save/Load did not reassert Game Menu dialogue-shell suppression.");

            VNGameMenuAction[] embeddedNavigation =
            {
                VNGameMenuAction.Save,
                VNGameMenuAction.Load,
                VNGameMenuAction.Preferences,
                VNGameMenuAction.MainMenu,
                VNGameMenuAction.Quit,
                VNGameMenuAction.Return
            };
            Require(embeddedNavigation.All(action => harness.Menu.View.GetButton(action).interactable),
                "Embedded Save/Load incorrectly disabled persistent Game Menu navigation.");

            confirmationRoot.SetActive(true);
            typeof(VNGameMenuController).GetMethod("Update", PrivateInstance)?.Invoke(harness.Menu, null);
            harness.Menu.View.GetButton(VNGameMenuAction.Return).onClick.Invoke();
            Require(embeddedNavigation.All(action => !harness.Menu.View.GetButton(action).interactable)
                && saveLoad.IsConfirmationOpen && harness.Menu.IsOpen,
                "Save/Load confirmation did not block its underlying Game Menu navigation.");
            Require(saveLoad.HandleEscape() && !saveLoad.IsConfirmationOpen && harness.Menu.IsOpen,
                "Escape did not cancel only the nested Save/Load confirmation layer.");

            harness.Dialogue.confirmExitPanel = new GameObject("Existing Main Menu Confirmation");
            harness.Dialogue.confirmExitPanel.transform.SetParent(harness.Canvas.transform, false);
            harness.Dialogue.confirmExitPanel.SetActive(false);

            harness.Menu.View.GetButton(VNGameMenuAction.Preferences).onClick.Invoke();
            Require(preferences.IsOpen && !harness.Menu.View.IsSaveLoadContentVisible
                && saveLoad.transform.parent == harness.Canvas.transform
                && saveLoadRect.anchorMin == new Vector2(0.12f, 0.14f)
                && saveLoadRect.anchorMax == new Vector2(0.88f, 0.86f),
                "Preferences from embedded Save/Load did not leave the content section cleanly.");
            adapter.SharedView.GetButton("back").onClick.Invoke();
            Require(!preferences.IsOpen && harness.Menu.IsPresentationVisible,
                "Preferences from embedded Save/Load did not return to Game Menu.");

            harness.Menu.View.GetButton(VNGameMenuAction.Save).onClick.Invoke();
            harness.Menu.View.GetButton(VNGameMenuAction.MainMenu).onClick.Invoke();
            Require(harness.Dialogue.confirmExitPanel.activeSelf && !harness.Menu.IsPresentationVisible
                && !harness.Menu.View.IsSaveLoadContentVisible
                && saveLoad.transform.parent == harness.Canvas.transform,
                "Main Menu from embedded Save/Load bypassed or failed to use the existing confirmation.");
            harness.Dialogue.HideConfirmExit();
            Require(!harness.Dialogue.confirmExitPanel.activeSelf && harness.Menu.IsPresentationVisible,
                "Cancelling Main Menu confirmation did not return to Game Menu.");

            harness.Menu.View.GetButton(VNGameMenuAction.Load).onClick.Invoke();
            harness.Menu.View.GetButton(VNGameMenuAction.Quit).onClick.Invoke();
            Require(harness.Menu.IsLocalConfirmationOpen && !harness.Menu.View.IsSaveLoadContentVisible
                && saveLoad.transform.parent == harness.Canvas.transform,
                "Quit from embedded Save/Load did not leave the content section for its confirmation.");
            Require(harness.Menu.TryHandleEscape() && !harness.Menu.IsLocalConfirmationOpen && harness.Menu.IsOpen,
                "Escape did not cancel Quit while retaining Game Menu.");

            harness.Menu.View.GetButton(VNGameMenuAction.Save).onClick.Invoke();
            harness.Menu.View.GetButton(VNGameMenuAction.Return).onClick.Invoke();
            Require(!harness.Menu.IsOpen && !harness.Menu.View.IsSaveLoadContentVisible
                && saveLoad.transform.parent == harness.Canvas.transform,
                "Return from embedded Save/Load did not close Game Menu and restore the standalone panel parent.");
        }
        finally
        {
            if (saveManagerBefore == null && SaveManager.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(SaveManager.Instance.gameObject);
            }

            harness.Dispose();
        }
    }

    private static void VerifySaveAndPersistentStateIsolation()
    {
        Require(SaveData.CurrentVersion == 3, "Game Menu changed SaveData.CurrentVersion.");
        string json = JsonUtility.ToJson(new SaveData());
        Require(json.IndexOf("gameMenu", StringComparison.OrdinalIgnoreCase) < 0,
            "Transient Game Menu state leaked into campaign SaveData.");
        Require(typeof(VNGameMenuController).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(field => field.FieldType != typeof(SaveManager) && field.FieldType != typeof(GameState)),
            "Game Menu controller took ownership of SaveManager or GameState.");
        Require(typeof(VNGameMenuController).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .All(field => field.FieldType != typeof(VNGameMenuController)),
            "Game Menu became a singleton/global manager.");
        Require(typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.OpenSave)) != null
            && typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.OpenLoad)) != null,
            "Game Menu cannot route to the existing Save/Load panel.");
        Require(typeof(SceneFlowManager).GetMethod(nameof(SceneFlowManager.EndReplay)) != null
            && typeof(SceneFlowManager).GetMethod(nameof(SceneFlowManager.QuitGame)) != null,
            "Game Menu cannot reuse the existing replay cleanup or quit path.");
    }

    private static Harness CreateHarness(string name)
    {
        GameObject canvasObject = new GameObject(name + " Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject dialogueObject = new GameObject(name + " Dialogue", typeof(RectTransform));
        dialogueObject.transform.SetParent(canvasObject.transform, false);
        VNDialogueController dialogue = dialogueObject.AddComponent<VNDialogueController>();
        typeof(VNDialogueController).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, dialogue);
        dialogue.dialogueUiRoot = new GameObject("Dialogue UI Root");
        dialogue.dialogueUiRoot.transform.SetParent(canvasObject.transform, false);
        SetPrivate(dialogue, "isRuntimeReady", true);

        GameObject quickOwner = new GameObject(name + " Quick Owner", typeof(RectTransform));
        quickOwner.transform.SetParent(canvasObject.transform, false);
        quickOwner.SetActive(false);
        VNQuickMenu quickMenu = quickOwner.AddComponent<VNQuickMenu>();
        GameObject quickRoot = new GameObject("Quick Root", typeof(RectTransform));
        quickRoot.transform.SetParent(quickOwner.transform, false);
        quickMenu.dialogueController = dialogue;
        quickMenu.root = quickRoot;
        quickMenu.historyButton = CreateButton(quickRoot.transform, "History");
        quickMenu.settingsButton = CreateButton(quickRoot.transform, "Settings");
        quickMenu.mainMenuButton = CreateButton(quickRoot.transform, "Menu");
        Require(quickMenu.EnsureCharacterHubLauncher(), "Dedicated Character Hub launcher fixture was not created.");
        quickOwner.SetActive(true);
        quickMenu.RefreshSpecialModeVisibility();

        VNGameMenuController menu = VNGameMenuController.TryCreateRuntime(dialogue);
        Require(menu != null, "Game Menu runtime controller was not created.");
        SetPrivate(dialogue, "gameMenuController", menu);
        return new Harness(canvasObject, dialogue, menu, quickMenu, quickRoot);
    }

    private static void SetupRuntimeCharacterHub(Harness harness)
    {
        CharacterProfileDefinition profile = ScriptableObject.CreateInstance<CharacterProfileDefinition>();
        profile.characterId = "game_menu_test_character";
        profile.displayName = "TEST CHARACTER";
        profile.biography = "TEST BIO";
        CharacterHubFixture[] fixtures = { new CharacterHubFixture { definition = profile } };
        CharacterHubController hub = harness.Dialogue.gameObject.GetComponent<CharacterHubController>()
            ?? harness.Dialogue.gameObject.AddComponent<CharacterHubController>();
        MethodInfo initialize = typeof(CharacterHubController).GetMethod("InitializeRuntime", PrivateInstance);
        Require(initialize != null, "Character Hub runtime initializer was not found.");
        initialize.Invoke(hub, new object[] { harness.Dialogue, harness.Canvas.GetComponent<Canvas>(), fixtures });
        harness.Dialogue.characterHubController = hub;
        harness.OwnedProfile = profile;
    }

    private static Rect GetRectInSpace(RectTransform space, RectTransform target)
    {
        Require(space != null && target != null, "RectTransform bounds comparison received a missing transform.");
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = space.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        const float tolerance = 0.75f;
        return inner.xMin >= outer.xMin - tolerance
            && inner.xMax <= outer.xMax + tolerance
            && inner.yMin >= outer.yMin - tolerance
            && inner.yMax <= outer.yMax + tolerance;
    }

    private static bool Overlaps(Rect first, Rect second)
    {
        const float tolerance = 0.5f;
        return first.xMin < second.xMax - tolerance
            && first.xMax > second.xMin + tolerance
            && first.yMin < second.yMax - tolerance
            && first.yMax > second.yMin + tolerance;
    }

    private static bool IsFinitePositive(Rect rect)
    {
        return float.IsFinite(rect.xMin)
            && float.IsFinite(rect.xMax)
            && float.IsFinite(rect.yMin)
            && float.IsFinite(rect.yMax)
            && rect.width > 0f
            && rect.height > 0f;
    }

    private static int CountDistinctCenters(RectTransform[] rects, RectTransform space, bool horizontal)
    {
        var distinct = new System.Collections.Generic.List<float>();
        foreach (RectTransform rect in rects)
        {
            Vector3 localCenter = space.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            float value = horizontal ? localCenter.x : localCenter.y;
            if (distinct.All(existing => Mathf.Abs(existing - value) > 1f))
            {
                distinct.Add(value);
            }
        }

        return distinct.Count;
    }

    private static void AssertVisibleActions(VNGameMenuView view, VNGameMenuAction[] expected)
    {
        VNGameMenuAction[] actual = Enum.GetValues(typeof(VNGameMenuAction)).Cast<VNGameMenuAction>()
            .Where(view.IsActionVisible)
            .ToArray();
        Require(actual.SequenceEqual(expected),
            "Unexpected Game Menu actions. Expected: " + string.Join(", ", expected) + "; actual: " + string.Join(", ", actual));
    }

    private static void AssertLabel(VNGameMenuView view, VNGameMenuAction action, string expected)
    {
        TextMeshProUGUI label = view.GetButton(action)?.GetComponentInChildren<TextMeshProUGUI>(true);
        Require(label != null && label.text == expected, $"Unexpected label for {action}: '{label?.text}'.");
    }

    private static Button CreateButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        CreateTmp(buttonObject.transform, label).text = label;
        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateTmp(Transform parent, string name)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static T GetPrivate<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, $"Private field '{name}' was not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, $"Private field '{name}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Harness : IDisposable
    {
        public Harness(GameObject canvas, VNDialogueController dialogue, VNGameMenuController menu, VNQuickMenu quickMenu, GameObject quickRoot)
        {
            Canvas = canvas;
            Dialogue = dialogue;
            Menu = menu;
            QuickMenu = quickMenu;
            QuickRoot = quickRoot;
        }

        public GameObject Canvas { get; }
        public VNDialogueController Dialogue { get; }
        public VNGameMenuController Menu { get; }
        public VNQuickMenu QuickMenu { get; }
        public GameObject QuickRoot { get; }
        public CharacterProfileDefinition OwnedProfile { get; set; }

        public void Dispose()
        {
            if (OwnedProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(OwnedProfile);
            }

            UnityEngine.Object.DestroyImmediate(Canvas);
        }
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        private GameSettings settings = new GameSettings();
        public PreferencesState Current => new PreferencesState(settings);
        public bool IsAvailable => true;
        public void Reset() => settings = new GameSettings();
        public void SetMasterVolume(float value) => settings.masterVolume = value;
        public void SetMusicVolume(float value) => settings.musicVolume = value;
        public void SetSfxVolume(float value) => settings.sfxVolume = value;
        public void SetMuteAll(bool value) => settings.muteAll = value;
        public void SetScreenMode(string value) => settings.screenMode = value;
        public void SetResolution(string value) => settings.resolution = value;
        public void SetRunInBackground(bool value) => settings.runInBackground = value;
        public void SetSkipMode(string value) => settings.skipMode = value;
        public void SetSkipBehavior(string value) => settings.skipBehavior = value;
        public void SetTextSpeed(float value) => settings.textSpeed = value;
        public void SetDialogueTextScale(float value) => settings.dialogueTextScale = value;
        public void SetTextboxOpacity(float value) => settings.textboxOpacity = value;
        public void SetAutoForwardDelay(float value) => settings.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => settings.skipAfterChoices = value;
        public void SetAutoForward(bool value) => settings.autoForward = value;
        public void SetAutoSave(bool value) => settings.autoSave = value;
        public void SetShowQuickMenu(bool value) => settings.showQuickMenu = value;
    }
}
