using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact facade over the existing VN actions. It owns no save, skip or auto state.</summary>
public sealed class VNQuickMenu : MonoBehaviour
{
    public VNDialogueController dialogueController;
    public GameObject root;
    public Button rollbackButton;
    public Button historyButton;
    public Button skipButton;
    public Button autoButton;
    public Button saveButton;
    public Button quickSaveButton;
    public Button quickLoadButton;
    public Button loadButton;
    public Button settingsButton;
    public Button charactersButton;
    public Button mainMenuButton;

    private const float MinimumDialogueSpacing = 12f;
    private const float RollbackStripMinWidth = 450f;
    // Three canvas units rasterize evenly at both 1920x1080 and 1280x720.
    private const float SeparatorWidth = 3f;
    private const float SeparatorHeight = 14f;
    private const float StripBandOverhang = 40f;
    private const float StripBandHeight = 72f;
    private const string RollbackButtonLabel = "Назад";
    // UI Target v1 reading language: quiet flat labels over one shared soft band,
    // thin cyan separators, and a cyan underline as the interaction accent.
    private static readonly Color PlateColor = new Color(0.02f, 0.045f, 0.07f, 0f);
    private static readonly Color UnderlineBaseColor = new Color(0.008f, 0.851f, 0.976f, 0.55f);
    private static readonly Color ActiveUnderlineColor = new Color(0.02f, 0.78f, 0.94f, 1f);
    private static readonly Color SeparatorColor = new Color(0.008f, 0.851f, 0.976f, 0.5f);
    private static readonly Color BandColor = new Color(0.006f, 0.008f, 0.012f, 1f);
    private static readonly Color IdleLabelColor = new Color(0.94f, 0.96f, 0.98f, 0.92f);
    private static readonly Color ActiveLabelColor = new Color(0.78f, 0.92f, 1f, 1f);

    private Texture2D stripBandTexture;
    private Sprite stripBandSprite;
    private Button[] stripOrder;
    private Image[] stripSeparators;

    private bool hiddenBySpecialMode;
    private bool hiddenByPlayer;
    private bool hiddenByPreferencesModal;
    private bool hiddenByGameMenuModal;
    private CanvasGroup gameMenuInputBlocker;
    private bool hiddenBySaveLoadModal;
    private bool hiddenByBacklogModal;
    private bool effectiveVisible;
    private bool safeAreaInitialized;
    private float dialogueBaseAnchoredY;
    private float dialogueSpacing;
    private RectTransform dialogueRect;
    private RectTransform quickMenuRect;
    private bool skipPresentationInitialized;
    private bool autoPresentationInitialized;
    private bool presentedSkipState;
    private bool presentedAutoState;

    public float QuickMenuSafeAreaReserve { get; private set; }
    public bool IsEffectivelyVisible => effectiveVisible;
    public bool IsCharacterHubLauncherVisible => charactersButton != null && charactersButton.gameObject.activeSelf;

    private void Awake()
    {
        ApplyPlayerFacingPresentation();
        Bind(historyButton, () => TryInvokeQuickMenuAction(() => dialogueController.ShowBacklog()));
        Bind(skipButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleSkip()));
        Bind(autoButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleAutoForward()));
        Bind(saveButton, () => TryInvokeQuickMenuAction(() => dialogueController.manualSaveLoadPanel?.OpenSave()));
        Bind(quickSaveButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickSave()));
        Bind(quickLoadButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickLoad()));
        Bind(settingsButton, () => TryInvokeQuickMenuAction(() => dialogueController.OpenSettings()));
        Bind(charactersButton, () => dialogueController?.OpenCharacterHub());
        Bind(mainMenuButton, () => TryInvokeQuickMenuAction(HandleMenuAction));
        RefreshReplayPresentation();
        SettingsManager.QuickMenuVisibilityChanged += RefreshEffectiveVisibility;
        RefreshEffectiveVisibility();
    }

    private void OnDestroy()
    {
        SettingsManager.QuickMenuVisibilityChanged -= RefreshEffectiveVisibility;
        DestroyStripBandResources();
    }

    /// <summary>Compatibility entry retained for existing editor callers.</summary>
    public bool EnsureCharactersButton()
    {
        return EnsureCharacterHubLauncher();
    }

    /// <summary>Builds the narrow Character Hub entry outside the Quick Menu strip.</summary>
    public bool EnsureCharacterHubLauncher()
    {
        if (charactersButton != null)
        {
            return false;
        }

        Canvas canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        Transform host = canvas != null
            ? canvas.transform
            : root != null && root.transform.parent != null ? root.transform.parent : null;
        if (host == null)
        {
            return false;
        }

        GameObject launcher = new GameObject(
            "Character Hub Launcher",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        launcher.layer = host.gameObject.layer;
        launcher.transform.SetParent(host, false);
        launcher.transform.SetAsLastSibling();
        RectTransform rect = launcher.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -28f);
        rect.sizeDelta = new Vector2(150f, 42f);

        Image image = launcher.GetComponent<Image>();
        image.color = new Color(0.025f, 0.075f, 0.12f, 0.92f);
        charactersButton = launcher.GetComponent<Button>();
        charactersButton.targetGraphic = image;
        charactersButton.colors = CreateButtonColors();
        Outline outline = launcher.GetComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.78f, 0.90f, 0.34f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = launcher.layer;
        labelObject.transform.SetParent(launcher.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = "Персонажи";
        label.fontSize = 17f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return true;
    }

    /// <summary>Applies the compact reading-action strip without modifying VNPrototype.unity.</summary>
    public void ApplyPlayerFacingPresentation()
    {
        EnsureCharacterHubLauncher();
        EnsureRollbackButton();
        EnsureStripChrome();
        SetButtonVisible(rollbackButton, !SceneFlowManager.IsReplayModeActive);
        SetButtonVisible(charactersButton, false);
        SetButtonVisible(saveButton, false);
        SetButtonVisible(quickSaveButton, true);
        SetButtonVisible(quickLoadButton, false);
        SetButtonVisible(loadButton, false);
        SetButtonVisible(settingsButton, false);
        SetButtonVisible(mainMenuButton, false);

        Button[] ordered =
        {
            rollbackButton,
            historyButton,
            skipButton,
            autoButton,
            quickSaveButton
        };
        stripOrder = ordered;
        RectTransform rootRect = root != null ? root.transform as RectTransform : null;
        if (rootRect != null)
        {
            // The compact strip mirrors the horizontally centered reading field and
            // stays visually secondary to the dialogue above it.
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 22f);
            rootRect.sizeDelta = new Vector2(450f, 36f);
        }
        HorizontalLayoutGroup rootLayout = root != null ? root.GetComponent<HorizontalLayoutGroup>() : null;
        if (rootLayout != null)
        {
            rootLayout.spacing = 12f;
            rootLayout.childAlignment = TextAnchor.MiddleCenter;
        }
        for (int index = 0; index < ordered.Length; index++)
        {
            Button button = ordered[index];
            ApplyButtonPresentation(button);
        }
        OrderStripChildren(ordered);

        // Width follows the label-driven buttons so the fifth (rollback) action
        // keeps the strip a single compact centered row instead of overflowing it.
        if (rootRect != null)
        {
            rootRect.sizeDelta = new Vector2(
                Mathf.Max(RollbackStripMinWidth, MeasureStripWidth(ordered, rootLayout)), 36f);
        }
    }

    /// <summary>
    /// Builds the shared soft band and the thin cyan separators once. They are runtime
    /// children born with final geometry: the scene keeps its serialized strip untouched.
    /// </summary>
    private void EnsureStripChrome()
    {
        if (root == null)
        {
            return;
        }

        if (stripSeparators == null)
        {
            stripSeparators = new Image[4];
            for (int index = 0; index < stripSeparators.Length; index++)
            {
                stripSeparators[index] = CreateStripSeparator(index);
            }
        }

        if (root.transform.Find("Strip Band") == null)
        {
            GameObject band = new GameObject(
                "Strip Band",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            band.layer = root.layer;
            band.transform.SetParent(root.transform, false);
            RectTransform bandRect = band.GetComponent<RectTransform>();
            bandRect.anchorMin = new Vector2(0f, 0.5f);
            bandRect.anchorMax = new Vector2(1f, 0.5f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.sizeDelta = new Vector2(StripBandOverhang, StripBandHeight);
            Image bandImage = band.GetComponent<Image>();
            bandImage.sprite = GetOrCreateStripBandSprite();
            bandImage.color = BandColor;
            bandImage.raycastTarget = false;
            // The band is chrome behind the whole strip, not a strip member.
            band.GetComponent<LayoutElement>().ignoreLayout = true;
            band.transform.SetAsFirstSibling();
        }
    }

    private Image CreateStripSeparator(int index)
    {
        GameObject separator = new GameObject(
            "Strip Separator " + index,
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        separator.layer = root.layer;
        separator.transform.SetParent(root.transform, false);
        RectTransform rect = separator.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(SeparatorWidth, SeparatorHeight);
        Image image = separator.GetComponent<Image>();
        image.color = SeparatorColor;
        image.raycastTarget = false;
        // Child height control would stretch the bar to the full strip height; the
        // explicit element keeps the accent thin while the strip stays one row.
        LayoutElement element = separator.GetComponent<LayoutElement>();
        element.preferredHeight = SeparatorHeight;
        return image;
    }

    /// <summary>Keeps the separator between two actions only while both actions are visible.</summary>
    private void RefreshStripSeparators()
    {
        if (stripSeparators == null || stripOrder == null)
        {
            return;
        }

        for (int index = 0; index < stripSeparators.Length; index++)
        {
            bool visible = index + 1 < stripOrder.Length
                && stripOrder[index] != null && stripOrder[index].gameObject.activeSelf
                && stripOrder[index + 1] != null && stripOrder[index + 1].gameObject.activeSelf;
            if (stripSeparators[index] != null
                && stripSeparators[index].gameObject.activeSelf != visible)
            {
                stripSeparators[index].gameObject.SetActive(visible);
            }
        }
    }

    private void OrderStripChildren(Button[] ordered)
    {
        if (root == null)
        {
            return;
        }

        Transform band = root.transform.Find("Strip Band");
        int slot = 0;
        if (band != null)
        {
            band.SetSiblingIndex(slot++);
        }

        for (int index = 0; index < ordered.Length; index++)
        {
            if (ordered[index] != null)
            {
                ordered[index].transform.SetSiblingIndex(slot++);
            }

            if (index < ordered.Length - 1 && stripSeparators != null && stripSeparators[index] != null)
            {
                stripSeparators[index].transform.SetSiblingIndex(slot++);
            }
        }

        RefreshStripSeparators();
    }

    private Sprite GetOrCreateStripBandSprite()
    {
        if (stripBandSprite != null)
        {
            return stripBandSprite;
        }

        const int width = 64;
        const int height = 8;
        stripBandTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Quick Menu Strip Band",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float normalizedY = y / (height - 1f);
            float vertical = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.28f, normalizedY))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, normalizedY)));
            for (int x = 0; x < width; x++)
            {
                float normalizedX = x / (width - 1f);
                float horizontal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, normalizedX))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, normalizedX)));
                pixels[y * width + x] = new Color(1f, 1f, 1f, 0.30f * horizontal * vertical);
            }
        }

        stripBandTexture.SetPixels(pixels);
        stripBandTexture.Apply(false, true);
        stripBandSprite = Sprite.Create(
            stripBandTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        stripBandSprite.name = "Runtime Quick Menu Strip Band Sprite";
        stripBandSprite.hideFlags = HideFlags.HideAndDontSave;
        return stripBandSprite;
    }

    private void DestroyStripBandResources()
    {
        if (stripBandSprite != null)
        {
            if (Application.isPlaying) Destroy(stripBandSprite);
            else DestroyImmediate(stripBandSprite);
            stripBandSprite = null;
        }

        if (stripBandTexture != null)
        {
            if (Application.isPlaying) Destroy(stripBandTexture);
            else DestroyImmediate(stripBandTexture);
            stripBandTexture = null;
        }
    }

    /// <summary>
    /// Builds the "Назад" rollback action as a runtime strip member so the scene
    /// stays untouched. Rollback intentionally lives here and on the mouse wheel
    /// only; the side Game Menu exposes system actions exclusively.
    /// </summary>
    public bool EnsureRollbackButton()
    {
        if (rollbackButton != null)
        {
            return false;
        }

        if (root == null)
        {
            return false;
        }

        GameObject buttonObject = new GameObject(
            "Rollback Button",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        buttonObject.layer = root.layer;
        buttonObject.transform.SetParent(root.transform, false);
        // Birth geometry matches the strip height: layout passes do not run while the
        // editor is idle, and the strip's bounds measurement includes child rects.
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(70f, 36f);
        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;
        rollbackButton = buttonObject.GetComponent<Button>();
        rollbackButton.targetGraphic = image;
        rollbackButton.colors = CreateButtonColors();
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.46f, 0.60f, 0.76f, 0.16f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = buttonObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = RollbackButtonLabel;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        rollbackButton.onClick.AddListener(RollbackOnce);
        return true;
    }

    private void RollbackOnce()
    {
        // TryRollback owns the full availability gate (replay, specials, choices,
        // modals), so the wheel and this button share one contract.
        dialogueController?.TryRollback();
    }

    private static float MeasureStripWidth(Button[] ordered, HorizontalLayoutGroup layout)
    {
        float total = 0f;
        int visibleCount = 0;
        foreach (Button button in ordered)
        {
            if (button == null || !button.gameObject.activeSelf)
            {
                continue;
            }

            if (button.transform is RectTransform buttonRect)
            {
                total += buttonRect.sizeDelta.x;
            }

            visibleCount++;
        }

        if (visibleCount > 1 && layout != null)
        {
            // Every pair of adjacent visible actions is separated by one thin cyan
            // divider with spacing on both sides.
            total += (visibleCount - 1) * (layout.spacing * 2f + SeparatorWidth);
        }

        return total;
    }

    private void Update()
    {
        RefreshEffectiveVisibility();
        RefreshReplayPresentation();
        RefreshActionPresentation(
            skipButton,
            dialogueController != null && dialogueController.IsSkipEnabled,
            ref skipPresentationInitialized,
            ref presentedSkipState);
        RefreshActionPresentation(
            autoButton,
            dialogueController != null && dialogueController.IsAutoForwardEnabledState,
            ref autoPresentationInitialized,
            ref presentedAutoState);
        RefreshCharacterHubLauncherVisibility();
    }

    public void RefreshSpecialModeVisibility()
    {
        // This owner is exclusively for SpecialModeCoordinator. Ordinary modals, including
        // Character Hub, may deny Quick Menu actions but must not deactivate this root.
        hiddenBySpecialMode = dialogueController != null
            && dialogueController.HasActiveSpecialMode
            && !dialogueController.CanOpenQuickMenu;
        RefreshEffectiveVisibility();
    }

    /// <summary>Applies the persistent preference and independent transient visibility owners.</summary>
    public void RefreshEffectiveVisibility()
    {
        hiddenBySpecialMode = dialogueController != null
            && dialogueController.HasActiveSpecialMode
            && !dialogueController.CanOpenQuickMenu;

        bool showQuickMenu = SettingsManager.Instance == null
            || SettingsManager.Instance.settings == null
            || SettingsManager.Instance.settings.showQuickMenu;
        effectiveVisible = showQuickMenu
            && !hiddenByPlayer
            && !hiddenBySpecialMode
            && !hiddenByPreferencesModal
            && !hiddenBySaveLoadModal
            && !hiddenByBacklogModal;
        if (root != null && root.activeSelf != effectiveVisible)
        {
            root.SetActive(effectiveVisible);
        }

        RefreshDialogueSafeArea();
        RefreshCharacterHubLauncherVisibility();
    }

    /// <summary>
    /// Temporary visual ownership for gameplay Preferences. This never changes
    /// the persistent Quick Menu preference or any Quick Menu action semantics.
    /// </summary>
    public void SetPreferencesModalHidden(bool hidden)
    {
        hiddenByPreferencesModal = hidden;
        RefreshEffectiveVisibility();
    }

    /// <summary>Keep the Reading strip visible but inert beneath Game Menu.</summary>
    public void SetGameMenuModalHidden(bool hidden)
    {
        hiddenByGameMenuModal = hidden;
        if (root != null)
        {
            gameMenuInputBlocker ??= root.AddComponent<CanvasGroup>();
            gameMenuInputBlocker.interactable = !hidden;
            gameMenuInputBlocker.blocksRaycasts = !hidden;
        }
        RefreshEffectiveVisibility();
    }

    /// <summary>Temporary Save/Load blocker that leaves the player's preference unchanged.</summary>
    public void SetSaveLoadModalHidden(bool hidden)
    {
        hiddenBySaveLoadModal = hidden;
        RefreshEffectiveVisibility();
    }

    /// <summary>Temporary History blocker that leaves the player's preference unchanged.</summary>
    public void SetBacklogModalHidden(bool hidden)
    {
        hiddenByBacklogModal = hidden;
        RefreshEffectiveVisibility();
    }

    /// <summary>Temporarily hides the menu for the player's clean-view request without changing its normal visibility policy.</summary>
    public void SetPlayerInterfaceHidden(bool hidden)
    {
        hiddenByPlayer = hidden;
        RefreshEffectiveVisibility();
    }

    public void RefreshReplayPresentation()
    {
        bool replay = SceneFlowManager.IsReplayModeActive;
        SetButtonVisible(rollbackButton, !replay);
        SetButtonVisible(saveButton, false);
        SetButtonVisible(quickSaveButton, !replay);
        SetButtonVisible(quickLoadButton, false);
        SetButtonVisible(loadButton, false);
        SetButtonVisible(settingsButton, false);
        SetButtonVisible(mainMenuButton, false);
        RefreshStripSeparators();
        RefreshCharacterHubLauncherVisibility();
    }

    /// <summary>Updates the real dialogue layout consumer without touching dialogue state/content.</summary>
    public void RefreshDialogueSafeArea()
    {
        if (!TryInitializeSafeArea())
        {
            QuickMenuSafeAreaReserve = 0f;
            return;
        }

        float measuredMenuReserve = MeasureQuickMenuBottomReserve();
        QuickMenuSafeAreaReserve = effectiveVisible
            ? Mathf.Max(0f, measuredMenuReserve + dialogueSpacing)
            : 0f;
        Vector2 anchoredPosition = dialogueRect.anchoredPosition;
        anchoredPosition.y = dialogueBaseAnchoredY + QuickMenuSafeAreaReserve;
        dialogueRect.anchoredPosition = anchoredPosition;
    }

    private bool TryInitializeSafeArea()
    {
        RectTransform currentDialogueRect = dialogueController != null && dialogueController.dialogueUiRoot != null
            ? dialogueController.dialogueUiRoot.transform as RectTransform
            : null;
        RectTransform currentQuickMenuRect = root != null ? root.transform as RectTransform : null;
        if (currentDialogueRect == null || currentQuickMenuRect == null || currentDialogueRect.parent is not RectTransform)
        {
            return false;
        }

        if (safeAreaInitialized && dialogueRect == currentDialogueRect && quickMenuRect == currentQuickMenuRect)
        {
            return true;
        }

        dialogueRect = currentDialogueRect;
        quickMenuRect = currentQuickMenuRect;
        float measuredMenuReserve = MeasureQuickMenuBottomReserve();
        dialogueSpacing = Mathf.Max(MinimumDialogueSpacing, dialogueRect.anchoredPosition.y - measuredMenuReserve);
        dialogueBaseAnchoredY = dialogueRect.anchoredPosition.y - measuredMenuReserve - dialogueSpacing;
        safeAreaInitialized = true;
        return true;
    }

    private float MeasureQuickMenuBottomReserve()
    {
        if (dialogueRect == null || quickMenuRect == null || dialogueRect.parent is not RectTransform dialogueParent)
        {
            return 0f;
        }

        Bounds menuBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(dialogueParent, quickMenuRect);
        return Mathf.Max(0f, menuBounds.max.y - dialogueParent.rect.yMin);
    }

    private void RefreshCharacterHubLauncherVisibility()
    {
        if (charactersButton == null)
        {
            return;
        }

        // Character Hub remains available to its technical/runtime owners, but its
        // launcher is intentionally deferred from the ordinary player-facing demo.
        bool visible = false;
        SetButtonVisible(charactersButton, visible);
        charactersButton.interactable = visible;
    }

    private void HandleMenuAction()
    {
        dialogueController.OpenGameMenu();
    }

    private void TryInvokeQuickMenuAction(UnityEngine.Events.UnityAction action)
    {
        if (dialogueController == null || !dialogueController.CanOpenQuickMenu)
        {
            return;
        }

        action?.Invoke();
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void RefreshActionPresentation(Button button, bool active, ref bool initialized, ref bool previousState)
    {
        if (button == null || (initialized && previousState == active))
        {
            return;
        }

        initialized = true;
        previousState = active;
        button.colors = CreateButtonColors();
        if (button.targetGraphic is Image image)
        {
            image.color = UnderlineBaseColor;
        }

        // ACTIVE keeps a persistent, brighter underline beside the hover-driven one so
        // the mode stays clearly stronger than any transient pointer/focus state.
        Transform activeMark = button.transform.Find("Active Underline");
        if (activeMark != null)
        {
            activeMark.gameObject.SetActive(active);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = active ? ActiveLabelColor : IdleLabelColor;
        }
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null && button.gameObject.activeSelf != visible)
        {
            button.gameObject.SetActive(visible);
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (text != null && text.text != label)
        {
            text.text = label;
        }
    }

    private static ColorBlock CreateButtonColors(bool active = false)
    {
        // State tints stay neutral so the underline graphic owns the color instead of
        // being multiplied twice into an unpredictable shade. The resting transition
        // hides the underline completely; hover, keyboard selection and pressed reveal
        // it in a strict ladder that stays below the persistent ACTIVE underline.
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1.28f, 1.40f, 1.55f, 1.60f);
        colors.pressedColor = new Color(1.42f, 1.56f, 1.72f, 1.80f);
        colors.selectedColor = new Color(1.12f, 1.22f, 1.36f, 1.45f);
        colors.disabledColor = new Color(0.64f, 0.66f, 0.70f, 0.72f);
        colors.colorMultiplier = 1f;
        return colors;
    }

    private static void ApplyButtonPresentation(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.colors = CreateButtonColors();
        // The button's own Image only serves as the pointer hit area in the flat
        // strip; the interaction color lives on the runtime underline child.
        if (button.TryGetComponent(out Image plate))
        {
            plate.color = PlateColor;
        }

        Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        // Flat target language: the per-item plate and its outline stay fully
        // transparent; the plate only keeps serving as the pointer hit area.
        outline.effectColor = new Color(0.46f, 0.60f, 0.76f, 0f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            // 17 px keeps the flat labels readable without the old chip background at
            // the 1280x720 scale while the strip stays one compact centered row.
            label.fontSize = 17f;
            label.fontStyle = FontStyles.Normal;
            label.color = IdleLabelColor;
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                float width = Mathf.Clamp(label.GetPreferredValues(label.text).x + 24f, 70f, 130f);
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                EnsureUnderline(button, rect);
            }
        }
    }

    /// <summary>
    /// Gives the button its cyan interaction underline: a hover-driven bar (the button
    /// target graphic, revealed by the ColorBlock ladder) and the stronger persistent
    /// ACTIVE bar for Auto/Skip. Both are runtime children with final geometry.
    /// </summary>
    private static void EnsureUnderline(Button button, RectTransform buttonRect)
    {
        const float underlineHeight = 3f;
        const float underlineOffset = 3f;
        float width = Mathf.Max(24f, buttonRect.sizeDelta.x - 16f);

        Transform existing = button.transform.Find("Underline");
        if (existing == null)
        {
            GameObject underlineObject = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            underlineObject.layer = button.gameObject.layer;
            underlineObject.transform.SetParent(button.transform, false);
            Image created = underlineObject.GetComponent<Image>();
            created.raycastTarget = false;
            button.targetGraphic = created;
            existing = underlineObject.transform;
        }

        Image underline = existing.GetComponent<Image>();
        underline.color = UnderlineBaseColor;

        RectTransform underlineRect = existing as RectTransform;
        underlineRect.anchorMin = underlineRect.anchorMax = new Vector2(0.5f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0.5f);
        underlineRect.anchoredPosition = new Vector2(0f, underlineOffset);
        underlineRect.sizeDelta = new Vector2(width, underlineHeight);

        if (button.transform.Find("Active Underline") == null)
        {
            GameObject activeObject = new GameObject("Active Underline", typeof(RectTransform), typeof(Image));
            activeObject.layer = button.gameObject.layer;
            activeObject.transform.SetParent(button.transform, false);
            Image activeUnderline = activeObject.GetComponent<Image>();
            activeUnderline.color = ActiveUnderlineColor;
            activeUnderline.raycastTarget = false;
            activeObject.SetActive(false);
            RectTransform activeRect = activeObject.GetComponent<RectTransform>();
            activeRect.anchorMin = activeRect.anchorMax = new Vector2(0.5f, 0f);
            activeRect.pivot = new Vector2(0.5f, 0.5f);
            activeRect.anchoredPosition = new Vector2(0f, underlineOffset);
            activeRect.sizeDelta = new Vector2(width, underlineHeight);
        }
    }
}
