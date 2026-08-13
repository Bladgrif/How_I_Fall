using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact facade over the existing VN actions. It owns no save, skip or auto state.</summary>
public sealed class VNQuickMenu : MonoBehaviour
{
    public VNDialogueController dialogueController;
    public GameObject root;
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
    private static readonly Color NormalColor = new Color(0.035f, 0.07f, 0.11f, 0.90f);
    private static readonly Color ActiveColor = new Color(0.10f, 0.31f, 0.46f, 0.96f);

    private bool hiddenBySpecialMode;
    private bool hiddenByPlayer;
    private bool hiddenByPreferencesModal;
    private bool hiddenByGameMenuModal;
    private bool effectiveVisible;
    private bool safeAreaInitialized;
    private float dialogueBaseAnchoredY;
    private float dialogueSpacing;
    private RectTransform dialogueRect;
    private RectTransform quickMenuRect;

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

    /// <summary>Applies the final normal action order without modifying VNPrototype.unity.</summary>
    public void ApplyPlayerFacingPresentation()
    {
        EnsureCharacterHubLauncher();
        SetButtonVisible(loadButton, false);
        SetButtonLabel(settingsButton, "Настройки");
        SetButtonLabel(mainMenuButton, "Меню");

        Button[] ordered =
        {
            historyButton,
            skipButton,
            autoButton,
            saveButton,
            quickSaveButton,
            quickLoadButton,
            settingsButton,
            mainMenuButton
        };
        for (int index = 0; index < ordered.Length; index++)
        {
            Button button = ordered[index];
            ApplyButtonPresentation(button);
            if (button != null && root != null && button.transform.parent == root.transform)
            {
                button.transform.SetSiblingIndex(index);
            }
        }
    }

    private void Update()
    {
        RefreshEffectiveVisibility();
        RefreshReplayPresentation();
        UpdateActiveState(skipButton, dialogueController != null && dialogueController.IsSkipEnabled);
        UpdateActiveState(autoButton, dialogueController != null && dialogueController.IsAutoForwardEnabledState);
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
            && !hiddenByGameMenuModal;
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

    /// <summary>Temporary Game Menu blocker. It never mutates the persistent preference or clean-view state.</summary>
    public void SetGameMenuModalHidden(bool hidden)
    {
        hiddenByGameMenuModal = hidden;
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
        SetButtonVisible(saveButton, !replay);
        SetButtonVisible(quickSaveButton, !replay);
        SetButtonVisible(quickLoadButton, !replay);
        SetButtonVisible(loadButton, false);
        SetButtonLabel(mainMenuButton, "Меню");
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

        bool visible = dialogueController != null
            && dialogueController.CanOpenCharacterHub
            && !hiddenByPlayer
            && !hiddenBySpecialMode
            && !hiddenByPreferencesModal
            && !hiddenByGameMenuModal
            && !SceneFlowManager.IsReplayModeActive;
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

    private static void UpdateActiveState(Button button, bool active)
    {
        if (button != null && button.targetGraphic is Image image)
        {
            image.color = active ? ActiveColor : NormalColor;
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

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.80f, 0.91f, 1f, 1f);
        colors.pressedColor = new Color(0.52f, 0.72f, 0.88f, 1f);
        colors.selectedColor = new Color(0.72f, 0.86f, 0.98f, 1f);
        colors.disabledColor = new Color(0.45f, 0.48f, 0.52f, 0.72f);
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
        if (button.targetGraphic == null && button.TryGetComponent(out Image image))
        {
            button.targetGraphic = image;
        }

        Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.46f, 0.60f, 0.76f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);
    }
}
