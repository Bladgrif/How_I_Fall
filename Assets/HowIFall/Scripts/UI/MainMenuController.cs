using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public sealed class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;
    private const string NavigationPanelName = "Main Menu Navigation Panel";
    private static readonly string[] TargetActionRoutes =
    {
        nameof(ContinueFromLatestSave),
        nameof(StartGame),
        nameof(OpenManualLoad),
        nameof(OpenSettings),
        nameof(OpenExitConfirm)
    };

    private static readonly string[] TargetActionLabels =
    {
        "Продолжить",
        "Новая игра",
        "Загрузить",
        "Настройки",
        "Выйти"
    };

    public SettingsPanelController settingsPanel;
    public ManualSaveLoadPanel manualSaveLoadPanel;
    public DialogueSceneRegistry dialogueRegistry;
    public Button continueButton;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private Button galleryReplayButton;
    [SerializeField] private TextMeshProUGUI galleryReplayTitle;
    [SerializeField] private TextMeshProUGUI galleryReplayState;
    [SerializeField] private GameObject galleryLockedOverlay;
    [SerializeField] private List<ReplayEntryDefinition> replayEntries = new List<ReplayEntryDefinition>();

    private Coroutine notificationCoroutine;
    private readonly List<Button> playerFacingActionButtons = new List<Button>();
    private Button modalFocusRestoreButton;

    public TextMeshProUGUI HelpText => helpText;
    public GameObject GalleryPanel => galleryPanel;
    public Button GalleryReplayButton => galleryReplayButton;
    public TextMeshProUGUI GalleryReplayTitle => galleryReplayTitle;
    public TextMeshProUGUI GalleryReplayState => galleryReplayState;
    public GameObject GalleryLockedOverlay => galleryLockedOverlay;
    public IReadOnlyList<ReplayEntryDefinition> ReplayEntries => replayEntries;
    public static IReadOnlyList<string> TargetPlayerFacingActionRoutes => TargetActionRoutes;
    public IReadOnlyList<Button> PlayerFacingActionButtons => playerFacingActionButtons;

    private void Awake()
    {
        ApplyPlayerFacingPresentation();
    }

    private void Start()
    {
        RefreshHelpText();
        SaveManager.EnsureInstance(dialogueRegistry);
        RefreshContinueAvailability();
        RefreshGallery();
        FocusDefaultAction();
    }

    private void Update()
    {
        // Settings restores its temporary hidden objects when it closes; keep the
        // retired prompt outside the Main Menu contract in that transition too.
        HideLegacyPrompt();

        if (VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel) && !(settingsPanel != null && settingsPanel.IsSharedDropdownCancel))
        {
            TryHandleCloseOrCancel();
        }
    }

    /// <summary>
    /// Reuses the authored MainMenu scene wiring while presenting only the Phase 4 action set.
    /// No serialized scene mutation or replacement menu hierarchy is required.
    /// </summary>
    public bool ApplyPlayerFacingPresentation()
    {
        Button[] orderedButtons =
        {
            FindPersistentButton(this, nameof(ContinueFromLatestSave)) ?? continueButton,
            FindPersistentButton(this, nameof(StartGame)),
            FindPersistentButton(this, nameof(OpenManualLoad)) ?? FindPersistentButton(manualSaveLoadPanel, nameof(ManualSaveLoadPanel.OpenLoad)),
            FindPersistentButton(this, nameof(OpenSettings)),
            FindPersistentButton(this, nameof(OpenExitConfirm))
        };

        if (orderedButtons.Any(button => button == null))
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: one or more Phase 4 action buttons could not be resolved from existing persistent routes.", this);
            return false;
        }

        Transform[] orderedRows = orderedButtons.Select(button => button.transform.parent).ToArray();
        Transform menuContent = orderedRows[0] != null ? orderedRows[0].parent : null;
        if (menuContent == null || orderedRows.Any(row => row == null || row.parent != menuContent))
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: Phase 4 action rows do not share the authored Menu Content parent.", this);
            return false;
        }

        for (int index = 0; index < orderedRows.Length; index++)
        {
            SetButtonLabel(orderedButtons[index], TargetActionLabels[index]);
        }

        Button galleryEntry = FindPersistentButton(this, nameof(OpenGallery));
        SetPlayerFacingRowVisible(FindPersistentButton(this, nameof(OpenHelp)), menuContent, false);
        SetPlayerFacingRowVisible(FindPersistentButton(this, nameof(OpenAbout)), menuContent, false);
        SetPlayerFacingRowVisible(galleryEntry, menuContent, false);

        playerFacingActionButtons.Clear();
        playerFacingActionButtons.AddRange(orderedButtons);
        ApplyMainNavigationLayout(orderedRows);
        if (!ApplyAuthoredBackground() || !ApplyAuthoredLogo())
        {
            return false;
        }

        RemoveObsoleteRuntimePresentation();
        ApplyActionPresentation(continueButton != null && continueButton.interactable);
        HideLegacyPrompt();
        ApplyModalPresentation();
        return true;
    }

    private void ApplyMainNavigationLayout(Transform[] orderedRows)
    {
        // Keep the compact navigation column visually connected to the logo while
        // leaving the authored background as the dominant title-screen element.
        float[] verticalPositions = { 244f, 184f, 124f, 64f, -28f };
        for (int index = 0; index < orderedRows.Length; index++)
        {
            RectTransform row = orderedRows[index] as RectTransform;
            if (row == null)
            {
                continue;
            }

            row.anchorMin = row.anchorMax = new Vector2(0f, 0.5f);
            row.pivot = new Vector2(0f, 0.5f);
            row.anchoredPosition = new Vector2(220f, verticalPositions[index]);
            row.sizeDelta = new Vector2(320f, 48f);

            RectTransform buttonRect = playerFacingActionButtons[index].transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.one;
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
            }
        }

        Transform menuContent = orderedRows[0].parent;
        Image[] separators = menuContent.GetComponentsInChildren<Image>(true)
            .Where(image => image.transform.parent == menuContent && image.gameObject.name.Contains("Separator"))
            .ToArray();
        foreach (Image separator in separators)
        {
            separator.gameObject.SetActive(false);
        }

        ApplyNavigationPanel(menuContent);
    }

    private bool ApplyAuthoredBackground()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: Canvas is missing.", this);
            return false;
        }

        Transform authoredBackground = canvas.transform.Find("Background");
        if (authoredBackground == null || !authoredBackground.TryGetComponent(out Image authoredImage)
            || authoredImage.sprite == null)
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: authored Background with a sprite is required.", this);
            return false;
        }

        authoredBackground.gameObject.SetActive(true);
        authoredBackground.SetAsFirstSibling();
        RectTransform backgroundRect = authoredBackground as RectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        authoredImage.color = Color.white;
        authoredImage.preserveAspect = false;
        authoredImage.raycastTarget = false;

        Transform gradient = canvas.transform.Find("Left Gradient Overlay");
        if (gradient != null && gradient.TryGetComponent(out Image gradientImage) && gradientImage.sprite != null)
        {
            gradient.gameObject.SetActive(true);
            gradientImage.color = new Color(1f, 1f, 1f, 0.72f);
            gradientImage.raycastTarget = false;
        }

        return true;
    }

    private static void ApplyNavigationPanel(Transform menuContent)
    {
        RectTransform panel = menuContent.Find(NavigationPanelName) as RectTransform;
        if (panel == null)
        {
            GameObject panelObject = new GameObject(
                NavigationPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(Shadow));
            panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(menuContent, false);
        }

        panel.SetAsFirstSibling();
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(184f, 108f);
        panel.sizeDelta = new Vector2(376f, 392f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = null;
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        Outline outline = panel.GetComponent<Outline>();
        outline.enabled = false;

        Shadow shadow = panel.GetComponent<Shadow>();
        shadow.enabled = false;
    }

    private void ApplyActionPresentation(bool hasCompatibleSave)
    {
        for (int index = 0; index < playerFacingActionButtons.Count; index++)
        {
            Button button = playerFacingActionButtons[index];
            MainMenuButtonVisualRole role = index == playerFacingActionButtons.Count - 1
                ? MainMenuButtonVisualRole.Destructive
                : (hasCompatibleSave ? index == 0 : index == 1)
                    ? MainMenuButtonVisualRole.Primary
                    : MainMenuButtonVisualRole.Secondary;
            ApplyMainMenuButtonPresentation(button, role);
        }
    }

    private bool ApplyAuthoredLogo()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        Transform logo = canvas != null
            ? canvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "Game Logo")
            : null;
        if (logo == null || !logo.TryGetComponent(out Image logoImage) || logoImage.sprite == null)
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: authored Game Logo with a sprite is required.", this);
            return false;
        }

        logo.gameObject.SetActive(true);
        RectTransform logoRect = logo as RectTransform;
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0f, 1f);
        logoRect.pivot = new Vector2(0f, 1f);
        logoRect.anchoredPosition = new Vector2(184f, -64f);
        logoRect.sizeDelta = new Vector2(360f, 160f);
        logoRect.localRotation = Quaternion.identity;
        logoImage.color = Color.white;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        return true;
    }

    private static void RemoveObsoleteRuntimePresentation()
    {
        foreach (string name in new[] { "Temporary Main Menu Background", "Main Menu Title", "Main Menu Subtitle" })
        {
            GameObject obsolete = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(transform => transform.name == name)?.gameObject;
            if (obsolete == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(obsolete);
            }
            else
            {
                DestroyImmediate(obsolete);
            }
        }
    }

    private void HideLegacyPrompt()
    {
        // The legacy prompt is wired outside the player-facing Canvas hierarchy in
        // the authored scene, so a Canvas-local lookup can leave it visible behind
        // a modal. It is not part of the approved Main Menu contract.
        Transform legacyPrompt = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(transform => transform.name == "Press Any Button");
        if (legacyPrompt != null)
        {
            legacyPrompt.gameObject.SetActive(false);
        }
    }

    private static void SetPlayerFacingRowVisible(Button button, Transform menuContent, bool visible)
    {
        Transform row = button != null ? button.transform.parent : null;
        if (row != null && row.parent == menuContent)
        {
            row.gameObject.SetActive(visible);
        }
    }

    private void ApplyModalPresentation()
    {
        RestyleModalPanel(helpPanel, helpText, false);
        RestyleModalPanel(aboutPanel, null, false);
        RestyleModalPanel(exitConfirmPanel, null, true);
    }

    private static void RestyleModalPanel(GameObject panel, TextMeshProUGUI knownBodyText, bool destructive)
    {
        if (panel == null)
        {
            return;
        }

        Image dimmer = panel.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => image.transform.parent == panel.transform);
        if (dimmer != null)
        {
            dimmer.color = new Color(0.004f, 0.008f, 0.018f, 0.78f);
            dimmer.raycastTarget = true;
        }

        RectTransform window = panel.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.transform.parent == panel.transform && rect.gameObject.name.Contains("Window"));
        if (window == null)
        {
            return;
        }

        Image windowImage = window.GetComponent<Image>();
        if (windowImage != null)
        {
            windowImage.sprite = null;
            windowImage.type = Image.Type.Simple;
            windowImage.color = new Color(0.012f, 0.022f, 0.035f, 0.97f);
        }

        Outline outline = window.GetComponent<Outline>() ?? window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.52f, 0.12f, 0.16f, 0.48f);
        outline.effectDistance = new Vector2(1f, -1f);
        Shadow shadow = window.GetComponent<Shadow>() ?? window.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(5f, -5f);

        foreach (Image accent in window.GetComponentsInChildren<Image>(true))
        {
            RectTransform accentRect = accent.rectTransform;
            if (accent.transform != window
                && accent.GetComponentInParent<Button>(true) == null
                && Mathf.Abs(accentRect.sizeDelta.y) <= 12f
                && accentRect.sizeDelta.x > 40f)
            {
                accent.sprite = null;
                accent.type = Image.Type.Simple;
                accent.color = new Color(0.66f, 0.16f, 0.20f, 0.80f);
            }
        }

        TextMeshProUGUI[] textElements = window.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI title = textElements
            .Where(text => text.GetComponentInParent<Button>(true) == null)
            .OrderByDescending(text => text.fontSize)
            .FirstOrDefault();
        if (title != null)
        {
            title.color = Color.white;
            title.enableWordWrapping = true;
        }

        foreach (TextMeshProUGUI text in textElements)
        {
            if (text == title || text.GetComponentInParent<Button>(true) != null)
            {
                continue;
            }

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0.08f, 0.17f);
            textRect.anchorMax = new Vector2(0.92f, 0.70f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17f;
            text.fontSizeMax = 25f;
            text.lineSpacing = 5f;
            text.color = new Color(0.90f, 0.94f, 1f, 0.96f);
        }

        if (knownBodyText != null)
        {
            knownBodyText.enableWordWrapping = true;
            knownBodyText.overflowMode = TextOverflowModes.Ellipsis;
        }

        foreach (Button button in window.GetComponentsInChildren<Button>(true))
        {
            bool isDestructiveButton = destructive
                && Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                    .Any(index => button.onClick.GetPersistentMethodName(index) == nameof(ConfirmExit));
            ApplyModalButtonPresentation(button, isDestructiveButton);
        }
    }

    private Button FindPersistentButton(Object target, string methodName)
    {
        if (target == null)
        {
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        Button[] candidates = canvas != null
            ? canvas.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in candidates)
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int index = 0; index < count; index++)
            {
                if (button.onClick.GetPersistentTarget(index) == target
                    && button.onClick.GetPersistentMethodName(index) == methodName)
                {
                    return button;
                }
            }
        }

        return null;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TextMeshProUGUI label = button != null
            ? button.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(text => text.gameObject.name == "Text")
                ?? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (label != null)
        {
            label.text = value;
            return;
        }

        Text legacyLabel = button != null
            ? button.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.gameObject.name == "Text")
                ?? button.GetComponentInChildren<Text>(true)
            : null;
        if (legacyLabel != null)
        {
            legacyLabel.text = value;
        }
    }

    public void StartGame()
    {
        if (RejectActiveReplay("New Game"))
        {
            return;
        }

        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueFromLatestSave()
    {
        if (RejectActiveReplay("Continue"))
        {
            return;
        }

        SaveManager saveManager = SaveManager.EnsureInstance(dialogueRegistry);
        if (!saveManager.LoadLatest())
        {
            ShowNotification("Нет совместимых сохранений");
            RefreshContinueAvailability();
        }
    }

    public void OpenManualLoad()
    {
        if (RejectActiveReplay("Load"))
        {
            return;
        }

        if (manualSaveLoadPanel == null)
        {
            ShowNotification("Экран загрузки недоступен");
            Debug.LogError("[LOAD UI] ManualSaveLoadPanel is not assigned.", this);
            return;
        }

        SaveManager.EnsureInstance(dialogueRegistry);
        manualSaveLoadPanel.OpenLoad();
    }

    public void RefreshContinueAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        SaveManager saveManager = SaveManager.EnsureInstance(dialogueRegistry);
        bool hasCompatibleSave = saveManager.HasAnyValidSave();
        continueButton.interactable = hasCompatibleSave;
        ApplyActionPresentation(hasCompatibleSave);

        if (!hasCompatibleSave && GetEventSystem()?.currentSelectedGameObject == continueButton.gameObject)
        {
            FocusDefaultAction();
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        Debug.LogWarning("SettingsPanelController is not assigned.", this);
    }

    public void OpenAbout()
    {
        if (aboutPanel != null)
        {
            CaptureModalFocusRestoreTarget(FindPersistentButton(this, nameof(OpenAbout)));
            aboutPanel.SetActive(true);
            FocusButton(FindPersistentButton(this, nameof(CloseAbout)));
        }
    }

    public void OpenHelp()
    {
        RefreshHelpText();

        if (helpPanel != null)
        {
            CaptureModalFocusRestoreTarget(FindPersistentButton(this, nameof(OpenHelp)));
            helpPanel.SetActive(true);
            FocusButton(FindPersistentButton(this, nameof(CloseHelp)));
        }
    }

    private void RefreshHelpText()
    {
        if (helpText != null)
        {
            helpText.text = VNInputMap.BuildHelpText();
        }
    }

    public void CloseAbout()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(false);
            RestoreFocusAfterModal();
        }
    }

    public void CloseHelp()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
            RestoreFocusAfterModal();
        }
    }

    public void OpenExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            CaptureModalFocusRestoreTarget(FindPersistentButton(this, nameof(OpenExitConfirm)));
            exitConfirmPanel.SetActive(true);
            FocusExitConfirmationCancel();
            return;
        }

        ExitGame();
    }

    public void CloseExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(false);
            RestoreFocusAfterModal();
        }
    }

    /// <summary>Applies the Main Menu default selection without relying on serialized EventSystem state.</summary>
    public void FocusDefaultAction()
    {
        Button fallback = continueButton != null && continueButton.isActiveAndEnabled && continueButton.interactable
            ? continueButton
            : playerFacingActionButtons.Count > 1 ? playerFacingActionButtons[1] : null;
        FocusButton(fallback);
    }

    public void FocusSettingsAction()
    {
        FocusButton(FindPersistentButton(this, nameof(OpenSettings)));
    }

    /// <summary>Handles only Main Menu-owned modal cancellation. Child screens keep their own Back/Esc ownership.</summary>
    public bool TryHandleCloseOrCancel()
    {
        if (exitConfirmPanel != null && exitConfirmPanel.activeSelf)
        {
            CloseExitConfirm();
            return true;
        }

        if (helpPanel != null && helpPanel.activeSelf)
        {
            CloseHelp();
            return true;
        }

        if (aboutPanel != null && aboutPanel.activeSelf)
        {
            CloseAbout();
            return true;
        }

        return false;
    }

    public void ConfirmExit()
    {
        CloseExitConfirm();
        ExitGame();
    }

    public void ShowNotification(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
        }

        if (notificationPanel == null)
        {
            Debug.Log(message, this);
            return;
        }

        notificationPanel.SetActive(true);

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(NotificationDurationSeconds);

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }

        notificationCoroutine = null;
    }

    private static void ApplyMainMenuButtonPresentation(Button button, MainMenuButtonVisualRole role)
    {
        if (button == null)
        {
            return;
        }

        if (button.targetGraphic == null && button.TryGetComponent(out Image image))
        {
            button.targetGraphic = image;
        }

        if (button.targetGraphic is Image targetImage)
        {
            targetImage.sprite = null;
            targetImage.type = Image.Type.Simple;
            targetImage.color = Color.clear;
        }

        ApplyMainMenuButtonTypography(button);
        button.transition = Selectable.Transition.None;

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        MainMenuButtonHoverEffect hoverEffect = button.GetComponent<MainMenuButtonHoverEffect>();
        if (hoverEffect != null)
        {
            hoverEffect.Configure(role);
        }
    }

    private static void ApplyMainMenuButtonTypography(Button button)
    {
        TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
        {
            tmpLabel.alignment = TextAlignmentOptions.MidlineLeft;
            tmpLabel.fontSize = 20f;
            tmpLabel.enableAutoSizing = false;
            ApplyLabelPadding(tmpLabel.rectTransform);
            return;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 20;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            ApplyLabelPadding(label.rectTransform);
        }
    }

    private static void ApplyLabelPadding(RectTransform labelRect)
    {
        if (labelRect == null)
        {
            return;
        }

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 0f);
        labelRect.offsetMax = new Vector2(-18f, 0f);
    }

    private static void ApplyModalButtonPresentation(Button button, bool destructive)
    {
        if (button == null)
        {
            return;
        }

        if (button.targetGraphic == null && button.TryGetComponent(out Image image))
        {
            button.targetGraphic = image;
        }

        button.transition = Selectable.Transition.None;

        TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
        {
            tmpLabel.alignment = TextAlignmentOptions.Midline;
            tmpLabel.fontSize = 20f;
            tmpLabel.enableAutoSizing = false;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        if (button.targetGraphic is Image targetImage)
        {
            targetImage.sprite = null;
            targetImage.type = Image.Type.Simple;
            targetImage.color = Color.clear;
        }

        MainMenuButtonHoverEffect hoverEffect = button.GetComponent<MainMenuButtonHoverEffect>()
            ?? button.gameObject.AddComponent<MainMenuButtonHoverEffect>();
        hoverEffect.useRedFocusText = true;
        hoverEffect.suppressFocusAccent = true;
        hoverEffect.Configure(destructive
            ? MainMenuButtonVisualRole.Destructive
            : MainMenuButtonVisualRole.Secondary);

        Transform marker = button.transform.Find("Focus Accent");
        // Quit confirmation uses text-state focus instead of a separate marker.
        if (marker != null) marker.gameObject.SetActive(false);
    }

    private void CaptureModalFocusRestoreTarget(Button fallback)
    {
        EventSystem eventSystem = GetEventSystem();
        Button selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.GetComponent<Button>()
            : null;
        modalFocusRestoreButton = IsPlayerFacingAction(selected) ? selected : fallback;
    }

    private void RestoreFocusAfterModal()
    {
        Button restoreTarget = modalFocusRestoreButton;
        modalFocusRestoreButton = null;
        if (IsPlayerFacingAction(restoreTarget) && restoreTarget.interactable)
        {
            FocusButton(restoreTarget);
            return;
        }

        FocusDefaultAction();
    }

    private bool IsPlayerFacingAction(Button button)
    {
        return button != null
            && button.isActiveAndEnabled
            && playerFacingActionButtons.Contains(button);
    }

    private void FocusButton(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.interactable)
        {
            return;
        }

        GetEventSystem()?.SetSelectedGameObject(button.gameObject);
    }

    private EventSystem GetEventSystem()
    {
        return EventSystem.current ?? FindFirstObjectByType<EventSystem>();
    }

    private void FocusExitConfirmationCancel()
    {
        Button cancel = exitConfirmPanel == null
            ? null
            : exitConfirmPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => HasPersistentRoute(button, nameof(CloseExitConfirm)));
        EventSystem eventSystem = GetEventSystem();
        if (cancel != null && eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(cancel.gameObject);
        }
    }

    private bool HasPersistentRoute(Button button, string methodName)
    {
        if (button == null)
        {
            return false;
        }

        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) == this
                && button.onClick.GetPersistentMethodName(index) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    public void OpenGallery()
    {
        RefreshGallery();
        if (galleryPanel != null)
        {
            galleryPanel.SetActive(true);
        }
    }

    public void CloseGallery()
    {
        if (galleryPanel != null)
        {
            galleryPanel.SetActive(false);
        }
    }

    public void StartTestReplay()
    {
        if (replayEntries == null || replayEntries.Count != 1 || replayEntries[0] == null)
        {
            ShowNotification("TEST REPLAY is unavailable");
            return;
        }

        ReplayEntryDefinition definition = replayEntries[0];
        if (!ReplayUnlockRegistry.Default.IsUnlocked(definition.replayId))
        {
            ShowNotification("TEST REPLAY is locked");
            RefreshGallery();
            return;
        }

        SceneFlowManager flow = SceneFlowManager.EnsureInstance();
        if (!flow.TryStartReplay(definition, replayEntries, dialogueRegistry, out string error))
        {
            ShowNotification(string.IsNullOrEmpty(error) ? "TEST REPLAY could not start" : error);
        }
    }

    public void RefreshGallery()
    {
        ReplayEntryDefinition definition = replayEntries != null && replayEntries.Count == 1
            ? replayEntries[0]
            : null;
        bool valid = definition != null
            && SceneFlowManager.TryValidateReplayDefinition(definition, replayEntries, dialogueRegistry, out _);
        bool unlocked = valid && ReplayUnlockRegistry.Default.IsUnlocked(definition.replayId);

        if (galleryReplayTitle != null)
        {
            galleryReplayTitle.text = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : "TEST REPLAY";
        }

        if (galleryReplayState != null)
        {
            galleryReplayState.text = unlocked
                ? "TECH DEMO ONLY - NOT CANON"
                : "LOCKED";
        }

        if (galleryLockedOverlay != null)
        {
            galleryLockedOverlay.SetActive(!unlocked);
        }

        if (galleryReplayButton != null)
        {
            galleryReplayButton.interactable = unlocked;
        }
    }

    private bool RejectActiveReplay(string operation)
    {
        if (!SceneFlowManager.IsReplayModeActive)
        {
            return false;
        }

        Debug.LogWarning($"[REPLAY] Main Menu {operation} was denied while replay cleanup is pending.", this);
        ShowNotification("End Replay before continuing");
        return true;
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}
