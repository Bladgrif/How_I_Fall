using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ManualSaveLoadPanel : MonoBehaviour
{
    private enum PanelMode
    {
        Save,
        Load
    }

    private enum ConfirmationAction
    {
        None,
        Overwrite,
        Delete,
        Load
    }

    private const float PanelFadeDuration = 0.16f;
    private const float ConfirmationDuration = 0.12f;
    private static readonly Color ActiveTabOutlineColor = new Color(0.28f, 0.54f, 0.76f, 0.62f);
    private static readonly Color InactiveTabOutlineColor = new Color(0.16f, 0.25f, 0.34f, 0.34f);

    public int visualVersion;
    public CanvasGroup canvasGroup;
    public CanvasGroup contentCanvasGroup;
    public RectTransform windowRect;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI slotTypeHintText;
    public Button manualTabButton;
    public Button autoTabButton;
    public Button quickTabButton;
    public GameObject manualPaginationRoot;
    public Button previousManualPageButton;
    public Button nextManualPageButton;
    public Button[] manualPageButtons;
    public TextMeshProUGUI statusText;
    public CanvasGroup statusCanvasGroup;
    public float statusVisibleDuration = 1.75f;
    public Button closeButton;
    public ManualSaveSlotView[] slotViews;
    public GameObject confirmationRoot;
    public CanvasGroup confirmationCanvasGroup;
    public RectTransform confirmationWindow;
    public TextMeshProUGUI confirmationText;
    public Button confirmationYesButton;
    public Button confirmationNoButton;

    private PanelMode mode;
    [SerializeField] private SaveSlotType currentSlotType = SaveSlotType.Manual;
    [SerializeField] private int currentManualPage = 1;
    private ConfirmationAction pendingConfirmationAction;
    private SaveSlotType? pendingConfirmationSlotType;
    private int pendingConfirmationSlot;
    private bool saveInProgress;
    private bool loadInProgress;
    private Coroutine panelAnimation;
    private Coroutine confirmationAnimation;
    private Coroutine statusAnimation;
    private RectTransform compactNavigationRoot;
    private TextMeshProUGUI selectedSlotTitle;
    private TextMeshProUGUI selectedSlotMetadata;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsConfirmationOpen => confirmationRoot != null && confirmationRoot.activeSelf;
    public SaveSlotType CurrentSlotType => currentSlotType;
    public bool IsSaveMode => mode == PanelMode.Save;
    public bool IsOperationInProgress => saveInProgress || loadInProgress;
    public bool LoadInProgress => loadInProgress;
    public SaveSlotType? PendingConfirmationSlotType => pendingConfirmationSlotType;
    public int PendingConfirmationSlot => pendingConfirmationSlot;
    public int CurrentManualPage => currentManualPage;

    public static int GetGlobalManualSlot(int pageIndex, int localSlotIndex)
    {
        if (pageIndex < 1 || pageIndex > SaveManager.ManualPageCount || localSlotIndex < 1 || localSlotIndex > SaveManager.SlotsPerPage) return 0;
        return (pageIndex - 1) * SaveManager.SlotsPerPage + localSlotIndex;
    }

    public void SelectManualPage(int pageIndex)
    {
        int clampedPage = Mathf.Clamp(pageIndex, 1, SaveManager.ManualPageCount);
        if (HasOperationInProgress() || IsConfirmationOpen)
        {
            return;
        }

        if (currentSlotType == SaveSlotType.Manual && currentManualPage == clampedPage)
        {
            return;
        }

        // Numeric strip entries carry the whole Manual family meaning: selecting a
        // number is a Manual selection with its page, in Load and Save alike.
        currentSlotType = SaveSlotType.Manual;
        currentManualPage = clampedPage;
        ApplySlotTypePresentation();
        Refresh();
        FocusButton(GetManualPageButton(clampedPage));
    }

    public void NextManualPage() => SelectManualPage(currentManualPage + 1);
    public void PreviousManualPage() => SelectManualPage(currentManualPage - 1);

    private void Awake()
    {
        ApplyPlayerFacingPalette();
        ConfigureConfirmationPresentation();
        ConfigureCompactNavigationPresentation();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        BindTabButton(manualTabButton, SelectManualTab);
        BindTabButton(autoTabButton, SelectAutoTab);
        BindTabButton(quickTabButton, SelectQuickTab);
        BindTabButton(previousManualPageButton, PreviousManualPage);
        BindTabButton(nextManualPageButton, NextManualPage);
        if (manualPageButtons != null)
        {
            for (int i = 0; i < manualPageButtons.Length; i++)
            {
                int page = i + 1;
                BindTabButton(manualPageButtons[i], () => SelectManualPage(page));
            }
        }

        if (confirmationYesButton != null)
        {
            confirmationYesButton.onClick.RemoveListener(ConfirmPendingAction);
            confirmationYesButton.onClick.AddListener(ConfirmPendingAction);
        }

        if (confirmationNoButton != null)
        {
            confirmationNoButton.onClick.RemoveListener(CancelConfirmation);
            confirmationNoButton.onClick.AddListener(CancelConfirmation);
        }

        if (slotViews != null)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i]?.Initialize(this, i + 1);
            }
        }

        ConfigureStoryMomentPresentation();
        ConfigureStatusFeedbackPresentation();

        SetConfirmationVisible(false, true);
        ApplySlotTypePresentation();
    }

    // Reuse the existing family buttons and Manual pagination controls as one small
    // navigation line. Keeping this runtime-only preserves the shared prefab wiring
    // used by Main Menu and the embedded Game Menu presentation.
    private void ConfigureCompactNavigationPresentation()
    {
        compactNavigationRoot = manualPaginationRoot != null
            ? manualPaginationRoot.transform as RectTransform
            : null;
        if (compactNavigationRoot == null)
        {
            return;
        }

        Transform legacyTabsRoot = manualTabButton != null ? manualTabButton.transform.parent : null;
        MoveToCompactNavigation(manualTabButton);
        MoveToCompactNavigation(autoTabButton);
        MoveToCompactNavigation(quickTabButton);
        MoveToCompactNavigation(previousManualPageButton);
        MoveToCompactNavigation(nextManualPageButton);
        if (manualPageButtons != null)
        {
            foreach (Button pageButton in manualPageButtons)
            {
                MoveToCompactNavigation(pageButton);
            }
        }

        if (legacyTabsRoot != null && legacyTabsRoot != compactNavigationRoot)
        {
            legacyTabsRoot.gameObject.SetActive(false);
        }

        compactNavigationRoot.anchorMin = new Vector2(0.5f, 0f);
        compactNavigationRoot.anchorMax = new Vector2(0.5f, 0f);
        compactNavigationRoot.pivot = new Vector2(0.5f, 0.5f);
        compactNavigationRoot.anchoredPosition = new Vector2(0f, 54f);
        compactNavigationRoot.sizeDelta = new Vector2(1040f, 64f);
    }

    private void ConfigureStoryMomentPresentation()
    {
        if (windowRect == null || slotViews == null || slotViews.Length == 0)
        {
            return;
        }

        windowRect.sizeDelta = new Vector2(1680f, 960f);
        RectTransform gridRect = slotViews[0] != null ? slotViews[0].transform.parent as RectTransform : null;
        GridLayoutGroup grid = gridRect != null ? gridRect.GetComponent<GridLayoutGroup>() : null;
        if (gridRect != null)
        {
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, 54f);
            gridRect.sizeDelta = new Vector2(1460f, 496f);
        }
        if (grid != null)
        {
            grid.cellSize = new Vector2(470f, 238f);
            grid.spacing = new Vector2(25f, 20f);
        }

        foreach (ManualSaveSlotView view in slotViews)
        {
            view?.ApplyStoryMomentLayout();
        }

        Transform existing = windowRect.Find("Selected Story Moment");
        GameObject summary = existing != null ? existing.gameObject : new GameObject(
            "Selected Story Moment",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        summary.layer = windowRect.gameObject.layer;
        summary.transform.SetParent(windowRect, false);
        RectTransform summaryRect = summary.GetComponent<RectTransform>();
        summaryRect.anchorMin = summaryRect.anchorMax = new Vector2(0.5f, 0f);
        summaryRect.pivot = new Vector2(0.5f, 0.5f);
        // Reserve a dedicated band for transient feedback above the compact
        // family/page navigation. At 1280x720 the old footer coordinates let
        // the toast occupy the same pixels as the numeric page controls.
        summaryRect.anchoredPosition = new Vector2(0f, 194f);
        summaryRect.sizeDelta = new Vector2(1460f, 124f);
        Image summaryImage = summary.GetComponent<Image>();
        summaryImage.color = new Color(0.018f, 0.050f, 0.075f, 0.92f);
        summaryImage.raycastTarget = false;
        Outline summaryOutline = summary.GetComponent<Outline>();
        summaryOutline.effectColor = new Color(0.28f, 0.57f, 0.78f, 0.42f);
        summaryOutline.effectDistance = new Vector2(1f, -1f);

        selectedSlotTitle = CreateSummaryText(summary.transform, "Moment Title", 24f, FontStyles.Bold);
        RectTransform titleRect = selectedSlotTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.46f); titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(28f, 0f); titleRect.offsetMax = new Vector2(-28f, -16f);
        selectedSlotTitle.color = new Color(0.93f, 0.97f, 1f, 1f);

        selectedSlotMetadata = CreateSummaryText(summary.transform, "Moment Metadata", 17f, FontStyles.Normal);
        RectTransform metadataRect = selectedSlotMetadata.rectTransform;
        metadataRect.anchorMin = Vector2.zero; metadataRect.anchorMax = new Vector2(1f, 0.46f);
        metadataRect.offsetMin = new Vector2(28f, 12f); metadataRect.offsetMax = new Vector2(-28f, 0f);
        selectedSlotMetadata.color = new Color(0.58f, 0.72f, 0.84f, 1f);
        summary.transform.SetAsLastSibling();
        if (confirmationRoot != null) confirmationRoot.transform.SetAsLastSibling();
    }

    private void ConfigureStatusFeedbackPresentation()
    {
        RectTransform statusRoot = statusCanvasGroup != null
            ? statusCanvasGroup.transform as RectTransform
            : statusText != null ? statusText.rectTransform : null;
        if (statusRoot == null)
        {
            return;
        }

        statusRoot.anchorMin = statusRoot.anchorMax = new Vector2(0.5f, 0f);
        statusRoot.pivot = new Vector2(0.5f, 0.5f);
        statusRoot.anchoredPosition = new Vector2(0f, 112f);
        statusRoot.sizeDelta = new Vector2(720f, 36f);
        if (statusText != null)
        {
            statusText.alignment = TextAlignmentOptions.Center;
        }
    }

    private static TextMeshProUGUI CreateSummaryText(Transform parent, string name, float size, FontStyles style)
    {
        Transform existing = parent.Find(name);
        GameObject owner = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        owner.layer = parent.gameObject.layer;
        owner.transform.SetParent(parent, false);
        TextMeshProUGUI text = owner.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void MoveToCompactNavigation(Button button)
    {
        if (button == null || compactNavigationRoot == null)
        {
            return;
        }

        button.transform.SetParent(compactNavigationRoot, false);
    }

    private void ApplyPlayerFacingPalette()
    {
        if (windowRect == null)
        {
            return;
        }

        Transform topAccent = windowRect.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == "Red Accent");
        Image accentImage = topAccent != null ? topAccent.GetComponent<Image>() : null;
        if (accentImage != null)
        {
            accentImage.sprite = null;
            accentImage.type = Image.Type.Simple;
            accentImage.color = new Color(0.30f, 0.58f, 0.80f, 0.72f);
        }
    }

    private void ConfigureConfirmationPresentation()
    {
        if (confirmationRoot == null || confirmationWindow == null)
        {
            return;
        }

        Image dimmer = confirmationRoot.GetComponent<Image>();
        if (dimmer != null)
        {
            dimmer.color = new Color(0.004f, 0.008f, 0.018f, 0.80f);
            dimmer.raycastTarget = true;
        }

        confirmationWindow.sizeDelta = new Vector2(620f, 250f);
        Image windowImage = confirmationWindow.GetComponent<Image>();
        if (windowImage != null)
        {
            windowImage.sprite = null;
            windowImage.type = Image.Type.Simple;
            windowImage.color = new Color(0.012f, 0.022f, 0.035f, 0.98f);
        }

        Outline outline = confirmationWindow.GetComponent<Outline>() ?? confirmationWindow.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.58f, 0.80f, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);
        Shadow shadow = confirmationWindow.GetComponent<Shadow>() ?? confirmationWindow.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(5f, -5f);

        Transform accent = confirmationWindow.Find("Confirmation Accent");
        Image accentImage = accent != null ? accent.GetComponent<Image>() : null;
        if (accentImage != null)
        {
            accentImage.sprite = null;
            accentImage.type = Image.Type.Simple;
            accentImage.color = new Color(0.66f, 0.16f, 0.20f, 0.76f);
        }

        if (confirmationText != null)
        {
            RectTransform textRect = confirmationText.rectTransform;
            textRect.anchorMin = new Vector2(0.08f, 0.40f);
            textRect.anchorMax = new Vector2(0.92f, 0.82f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            confirmationText.alignment = TextAlignmentOptions.Center;
            confirmationText.fontStyle = FontStyles.Normal;
            confirmationText.fontSize = 22f;
            confirmationText.enableAutoSizing = true;
            confirmationText.fontSizeMin = 17f;
            confirmationText.fontSizeMax = 22f;
            confirmationText.enableWordWrapping = true;
            confirmationText.overflowMode = TextOverflowModes.Overflow;
            confirmationText.color = new Color(0.92f, 0.96f, 1f, 1f);
        }

        if (confirmationYesButton != null && confirmationYesButton.transform.parent != confirmationWindow)
        {
            confirmationYesButton.transform.SetParent(confirmationWindow, false);
        }
        if (confirmationNoButton != null && confirmationNoButton.transform.parent != confirmationWindow)
        {
            confirmationNoButton.transform.SetParent(confirmationWindow, false);
        }
        ConfigureConfirmationButton(confirmationYesButton, true, new Vector2(0.36f, 0.20f));
        ConfigureConfirmationButton(confirmationNoButton, false, new Vector2(0.64f, 0.20f));
        Button[] confirmationButtons = { confirmationYesButton, confirmationNoButton };
        confirmationYesButton?.GetComponent<MainMenuButtonHoverEffect>()?.ConfigureExclusiveActions(confirmationButtons);
        confirmationNoButton?.GetComponent<MainMenuButtonHoverEffect>()?.ConfigureExclusiveActions(confirmationButtons);
        RectTransform actions = confirmationYesButton != null
            && confirmationNoButton != null
            && confirmationYesButton.transform.parent == confirmationNoButton.transform.parent
            ? confirmationYesButton.transform.parent as RectTransform
            : null;
        if (actions != null && actions != confirmationWindow)
        {
            actions.anchorMin = actions.anchorMax = new Vector2(0.5f, 0.22f);
            actions.pivot = new Vector2(0.5f, 0.5f);
            actions.anchoredPosition = Vector2.zero;
            actions.sizeDelta = new Vector2(400f, 52f);
        }
    }

    private static void ConfigureConfirmationButton(Button button, bool destructive, Vector2 anchor)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(164f, 46f);
        button.transition = Selectable.Transition.None;

        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.clear;
            button.targetGraphic = image;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.fontSize = 19f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
        }

        MainMenuButtonHoverEffect effect = button.GetComponent<MainMenuButtonHoverEffect>()
            ?? button.gameObject.AddComponent<MainMenuButtonHoverEffect>();
        effect.highlightImage = image;
        effect.useRedFocusText = destructive;
        effect.suppressFocusAccent = destructive;
        effect.Configure(destructive ? MainMenuButtonVisualRole.Destructive : MainMenuButtonVisualRole.Secondary);
    }

    private void Update()
    {
        // The panel can remain open while the Game View or player window changes size.
        // Keep its existing viewport-fit presentation current instead of leaving the
        // 1920x1080 scale in place at a smaller resolution.
        if (IsOpen && panelAnimation == null && windowRect != null)
        {
            windowRect.localScale = GetWindowPresentationScale();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel))
        {
            HandleEscape();
        }
    }

    public void OpenSave()
    {
        if (RejectReplayOperation("SAVE UI"))
        {
            return;
        }

        if (VNDialogueController.Instance == null)
        {
            Debug.LogWarning("[SAVE UI] Save panel can only be opened from VNPrototype.", this);
            return;
        }

        if (!VNDialogueController.Instance.CanSave)
        {
            return;
        }

        mode = PanelMode.Save;
        currentSlotType = SaveSlotType.Manual;
        currentManualPage = 1;
        Open();
    }

    public void OpenLoad()
    {
        if (RejectReplayOperation("LOAD UI"))
        {
            return;
        }

        if (VNDialogueController.Instance != null && !VNDialogueController.Instance.CanLoad)
        {
            return;
        }

        mode = PanelMode.Load;
        currentSlotType = SaveSlotType.Manual;
        currentManualPage = 1;
        Open();
    }

    /// <summary>Requests the newest valid quick slot through the normal load confirmation pipeline.</summary>
    public bool RequestQuickLoad()
    {
        if (RejectReplayOperation("QUICK LOAD UI"))
        {
            return false;
        }

        if (VNDialogueController.Instance != null && !VNDialogueController.Instance.CanLoad)
        {
            return false;
        }

        SaveManager saveManager = ResolveSaveManager();
        SaveSlotInfo slot = saveManager == null
            ? null
            : saveManager.GetAllSlots(SaveSlotType.Quick)
                .Where(candidate => candidate.IsLoadable)
                .OrderByDescending(candidate => candidate.CreatedAtUtc)
                .ThenBy(candidate => candidate.SlotIndex)
                .FirstOrDefault();

        if (slot == null)
        {
            return false;
        }

        mode = PanelMode.Load;
        currentSlotType = SaveSlotType.Quick;
        Open();
        OpenConfirmation(ConfirmationAction.Load, SaveSlotType.Quick, slot.SlotIndex);
        return true;
    }

    public void SelectManualTab()
    {
        SelectSlotType(SaveSlotType.Manual);
    }

    public void SelectAutoTab()
    {
        SelectSlotType(SaveSlotType.Auto);
    }

    public void SelectQuickTab()
    {
        SelectSlotType(SaveSlotType.Quick);
    }

    public void Close()
    {
        if (HasOperationInProgress() || !gameObject.activeSelf)
        {
            return;
        }

        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
        StartPanelAnimation(false);
    }

    public bool HandleEscape()
    {
        if (!gameObject.activeSelf || HasOperationInProgress())
        {
            return false;
        }

        if (IsConfirmationOpen)
        {
            CancelConfirmation();
            return true;
        }

        Close();
        return true;
    }

    public void OnSlotSelected(int slotIndex)
    {
        if (RejectReplayOperation("SLOT UI"))
        {
            return;
        }

        if (HasOperationInProgress() || IsConfirmationOpen)
        {
            return;
        }

        SaveManager saveManager = ResolveSaveManager();
        if (saveManager == null)
        {
            SetStatus("Система сохранений недоступна", true);
            return;
        }

        if (mode == PanelMode.Save)
        {
            if (currentSlotType != SaveSlotType.Manual)
            {
                return;
            }

            SaveSlotInfo slot = saveManager.GetSlot(currentSlotType, slotIndex);
            if (slot.IsOccupied)
            {
                OpenConfirmation(ConfirmationAction.Overwrite, currentSlotType, slotIndex);
                return;
            }

            StartCoroutine(CaptureAndSave(currentSlotType, slotIndex));
            return;
        }

        SaveSlotInfo slotToLoad = saveManager.GetSlot(currentSlotType, slotIndex);
        if (!slotToLoad.IsLoadable)
        {
            SetStatus(GetUnavailableSlotMessage(slotToLoad), true);
            Refresh();
            return;
        }

        if (VNDialogueController.Instance != null)
        {
            OpenConfirmation(ConfirmationAction.Load, currentSlotType, slotIndex);
            return;
        }

        if (!saveManager.LoadSlot(currentSlotType, slotIndex))
        {
            SaveSlotInfo slot = saveManager.GetSlot(currentSlotType, slotIndex);
            SetStatus(GetUnavailableSlotMessage(slot), true);
            Refresh();
            return;
        }
    }

    public void OnDeleteRequested(int slotIndex)
    {
        if (HasOperationInProgress() || IsConfirmationOpen)
        {
            return;
        }

        SaveManager saveManager = ResolveSaveManager();
        if (saveManager == null)
        {
            SetStatus("Система сохранений недоступна", true);
            return;
        }

        if (!saveManager.GetSlot(currentSlotType, slotIndex).IsOccupied)
        {
            Refresh();
            return;
        }

        OpenConfirmation(ConfirmationAction.Delete, currentSlotType, slotIndex);
    }

    private void Open()
    {
        bool wasAlreadyOpen = gameObject.activeSelf;
        gameObject.SetActive(true);
        VNDialogueController.Instance?.SetQuickMenuSaveLoadModalHidden(true);
        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
        SetStatus(string.Empty, false);

        if (titleText != null)
        {
            titleText.text = mode == PanelMode.Save ? "СОХРАНИТЬ" : "ЗАГРУЗИТЬ";
        }

        ApplySlotTypePresentation();
        Refresh();
        FocusDefaultControl();
        if (wasAlreadyOpen)
        {
            ShowImmediately();
        }
        else
        {
            StartPanelAnimation(true);
        }
    }

    private void ShowImmediately()
    {
        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (windowRect != null)
        {
            windowRect.localScale = GetWindowPresentationScale();
        }

        SetContentInteractive(true);
    }

    private void StartPanelAnimation(bool opening)
    {
        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
        }

        panelAnimation = StartCoroutine(AnimatePanel(opening));
    }

    private IEnumerator AnimatePanel(bool opening)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = opening;
        }

        SetContentInteractive(opening);
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : (opening ? 0f : 1f);
        float targetAlpha = opening ? 1f : 0f;
        Vector3 presentationScale = GetWindowPresentationScale();
        Vector3 startScale = windowRect != null
            ? windowRect.localScale
            : presentationScale * (opening ? 0.985f : 1f);
        Vector3 targetScale = presentationScale * (opening ? 1f : 0.985f);
        float elapsed = 0f;

        while (elapsed < PanelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / PanelFadeDuration));
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            }

            if (windowRect != null)
            {
                windowRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = targetAlpha;
        }

        if (windowRect != null)
        {
            windowRect.localScale = targetScale;
        }

        panelAnimation = null;
        if (opening)
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
            }

            SetContentInteractive(!IsConfirmationOpen);
            yield break;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // This is a transient modal owner only; never write the persistent
        // Quick Menu preference when Save/Load closes or a scene unloads.
        VNDialogueController.Instance?.SetQuickMenuSaveLoadModalHidden(false);
    }

    private void ConfirmPendingAction()
    {
        if (RejectReplayOperation("CONFIRMED SAVE/LOAD UI"))
        {
            ClearPendingConfirmation();
            SetConfirmationVisible(false, true);
            return;
        }

        ConfirmationAction action = pendingConfirmationAction;
        SaveSlotType? slotType = pendingConfirmationSlotType;
        int slotIndex = pendingConfirmationSlot;
        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);

        if (!slotType.HasValue || slotIndex <= 0)
        {
            return;
        }

        if (action == ConfirmationAction.Overwrite)
        {
            if (slotType.Value != SaveSlotType.Manual)
            {
                SetStatus("Перезапись доступна только для ручных сохранений", true);
                return;
            }

            StartCoroutine(CaptureAndSave(slotType.Value, slotIndex));
            return;
        }

        if (action == ConfirmationAction.Delete)
        {
            SaveManager saveManager = ResolveSaveManager();
            bool deleted = saveManager != null && saveManager.DeleteSlot(slotType.Value, slotIndex);
            SetStatus(deleted ? $"Слот {slotIndex} удалён" : $"Не удалось удалить слот {slotIndex}", !deleted);
            Refresh();
            RefreshContinueAvailability();
            return;
        }

        if (action == ConfirmationAction.Load)
        {
            if (VNDialogueController.Instance == null || slotType.Value == SaveSlotType.Auto)
            {
                CompleteLoad(slotType.Value, slotIndex);
                return;
            }

            BeginPreLoadAutoSave(slotType.Value, slotIndex);
        }
    }

    private void BeginPreLoadAutoSave(SaveSlotType slotType, int slotIndex)
    {
        loadInProgress = true;
        HidePanelForPreLoadCapture();
        VNDialogueController.Instance.RequestPreLoadAutoSave(saved =>
        {
            if (!saved)
            {
                RestorePanelAfterFailedPreLoad();
                SetStatus("\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0441\u043e\u0437\u0434\u0430\u0442\u044c \u0430\u0432\u0442\u043e\u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435 \u043f\u0435\u0440\u0435\u0434 \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u043e\u0439", true);
                return;
            }

            CompleteLoad(slotType, slotIndex);
        });
    }

    private void CompleteLoad(SaveSlotType slotType, int slotIndex)
    {
        SaveManager saveManager = ResolveSaveManager();
        bool loaded = saveManager != null && saveManager.LoadSlot(slotType, slotIndex);
        loadInProgress = false;

        if (!loaded)
        {
            RestorePanelAfterFailedPreLoad();
            SaveSlotInfo slot = saveManager != null
                ? saveManager.GetSlot(slotType, slotIndex)
                : null;
            SetStatus(
                slot == null || string.IsNullOrEmpty(slot.Error)
                    ? "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u0441\u043b\u043e\u0442"
                    : slot.Error,
                true);
            Refresh();
            return;
        }

        Close();
    }

    private void HidePanelForPreLoadCapture()
    {
        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        SetContentInteractive(false);
    }

    private void RestorePanelAfterFailedPreLoad()
    {
        loadInProgress = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        SetContentInteractive(true);
    }

    private void CancelConfirmation()
    {
        int slotIndex = pendingConfirmationSlot;
        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
        FocusSlotOrDefault(slotIndex);
    }

    private void OpenConfirmation(ConfirmationAction action, SaveSlotType slotType, int slotIndex)
    {
        if (action == ConfirmationAction.Overwrite && slotType != SaveSlotType.Manual)
        {
            return;
        }

        ClearPendingConfirmation();
        pendingConfirmationAction = action;
        pendingConfirmationSlotType = slotType;
        pendingConfirmationSlot = slotIndex;

        if (confirmationText != null)
        {
            confirmationText.text = action switch
            {
                ConfirmationAction.Delete => $"Удалить сохранение из слота {slotIndex}?",
                ConfirmationAction.Load => "Загрузить это сохранение? Несохранённый прогресс будет потерян.",
                _ => $"Перезаписать слот {slotIndex}?"
            };
        }

        SetButtonLabel(
            confirmationYesButton,
            action == ConfirmationAction.Delete
                ? "Удалить"
                : action == ConfirmationAction.Load
                    ? "Загрузить"
                    : "Перезаписать");
        SetButtonLabel(confirmationNoButton, "Отмена");
        SetConfirmationVisible(true, false);
    }

    private void ClearPendingConfirmation()
    {
        pendingConfirmationAction = ConfirmationAction.None;
        pendingConfirmationSlotType = null;
        pendingConfirmationSlot = 0;
    }

    private IEnumerator CaptureAndSave(SaveSlotType slotType, int slotIndex)
    {
        if (RejectReplayOperation("MANUAL SAVE CAPTURE"))
        {
            yield break;
        }

        if (slotType != SaveSlotType.Manual)
        {
            yield break;
        }

        saveInProgress = true;
        SetStatus("Создание сохранения...", false);

        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        if (windowRect != null)
        {
            windowRect.localScale = GetWindowPresentationScale();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        SetContentInteractive(true);

        float previousAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        bool previousInteractable = canvasGroup == null || canvasGroup.interactable;
        bool previousBlocksRaycasts = canvasGroup == null || canvasGroup.blocksRaycasts;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // WaitForEndOfFrame is not resumed by Unity's batch-mode PlayMode runner.
        // A normal player build keeps the end-of-frame capture; CI advances one frame instead.
        if (Application.isBatchMode)
        {
            yield return null;
        }
        else
        {
            yield return new WaitForEndOfFrame();
        }

        Texture2D screenshot = null;
        try
        {
            screenshot = SaveManager.CaptureScreenshotForSave();
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[SAVE UI] Screenshot capture failed. {exception.Message}", this);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = previousAlpha;
            canvasGroup.interactable = previousInteractable;
            canvasGroup.blocksRaycasts = previousBlocksRaycasts;
        }

        SaveManager saveManager = ResolveSaveManager();
        bool saved = saveManager != null && saveManager.SaveSlot(slotType, slotIndex, screenshot);

        if (screenshot != null)
        {
            Destroy(screenshot);
        }

        saveInProgress = false;
        SetStatus(saved ? $"Слот {slotIndex} сохранён" : "Не удалось сохранить слот", !saved);
        Refresh();
    }

    private void FocusDefaultControl()
    {
        Button firstInteractiveSlot = FindFirstInteractiveSlotButton();
        FocusButton(firstInteractiveSlot != null ? firstInteractiveSlot : closeButton);
    }

    private void FocusSlotOrDefault(int slotIndex)
    {
        Button slotButton = slotViews != null && slotIndex > 0 && slotIndex <= slotViews.Length
            ? slotViews[slotIndex - 1]?.button
            : null;
        Button target = IsInteractive(slotButton)
            ? slotButton
            : FindFirstInteractiveSlotButton() ?? closeButton;
        FocusButton(target);
    }

    private void Refresh()
    {
        SaveManager saveManager = ResolveSaveManager();
        if (slotViews == null)
        {
            return;
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            int localSlotIndex = i + 1;
            int globalSlotIndex = currentSlotType == SaveSlotType.Manual
                ? GetGlobalManualSlot(currentManualPage, localSlotIndex)
                : localSlotIndex;
            SaveSlotInfo slot = saveManager != null
                ? saveManager.GetSlot(currentSlotType, globalSlotIndex)
                : new SaveSlotInfo { SlotType = currentSlotType, SlotIndex = globalSlotIndex };
            slotViews[i]?.Render(slot, mode == PanelMode.Save, localSlotIndex);
        }

        ManualSaveSlotView focusedView = slotViews.FirstOrDefault(view => view != null && view.HasEventSystemFocus)
            ?? slotViews.FirstOrDefault(view => view != null && view.IsLoadable)
            ?? slotViews.FirstOrDefault(view => view != null);
        if (focusedView != null)
        {
            OnSlotFocused(focusedView.GlobalSlotIndex);
        }

        ConfigureNavigation();
    }

    public void OnSlotFocused(int slotIndex)
    {
        if (selectedSlotTitle == null || selectedSlotMetadata == null || slotIndex <= 0)
        {
            return;
        }

        SaveManager saveManager = ResolveSaveManager();
        SaveSlotInfo slot = saveManager != null ? saveManager.GetSlot(currentSlotType, slotIndex) : null;
        string family = currentSlotType switch
        {
            SaveSlotType.Auto => "АВТО",
            SaveSlotType.Quick => "БЫСТРОЕ",
            _ => "РУЧНОЕ"
        };
        if (slot != null && slot.IsLoadable)
        {
            selectedSlotTitle.text = slot.DisplayName;
            selectedSlotMetadata.text = $"{family}  ·  СЛОТ {slot.SlotIndex:00}  ·  {slot.DisplayDate}";
            return;
        }

        selectedSlotTitle.text = slot != null && slot.IsOccupied ? "Сохранение недоступно" : "Свободный момент истории";
        selectedSlotMetadata.text = $"{family}  ·  СЛОТ {slotIndex:00}";
    }

    private void SelectSlotType(SaveSlotType slotType)
    {
        if (HasOperationInProgress() || IsConfirmationOpen || (mode == PanelMode.Save && slotType != SaveSlotType.Manual) || currentSlotType == slotType)
        {
            return;
        }

        currentSlotType = slotType;
        ApplySlotTypePresentation();
        Refresh();
        FocusButton(slotType == SaveSlotType.Manual
            ? GetManualPageButton(currentManualPage)
            : GetTabButton(slotType));
    }

    private void ApplySlotTypePresentation()
    {
        if (subtitleText != null)
        {
            subtitleText.text = currentSlotType switch
            {
                SaveSlotType.Auto => "АВТОСОХРАНЕНИЯ",
                SaveSlotType.Quick => "БЫСТРЫЕ СОХРАНЕНИЯ",
                _ => "РУЧНЫЕ СОХРАНЕНИЯ"
            };
        }

        bool loadMode = mode == PanelMode.Load;
        if (slotTypeHintText != null)
        {
            slotTypeHintText.text = string.Empty;
            slotTypeHintText.gameObject.SetActive(false);
        }

        if (manualPaginationRoot != null) manualPaginationRoot.SetActive(true);
        SetButtonLabel(quickTabButton, "QS");
        SetButtonLabel(autoTabButton, "AS");

        // The strip itself carries family and page meaning, so the legacy
        // family tab and arrow controls stay serialized but never visible.
        if (manualTabButton != null) manualTabButton.gameObject.SetActive(false);
        if (previousManualPageButton != null) previousManualPageButton.gameObject.SetActive(false);
        if (nextManualPageButton != null) nextManualPageButton.gameObject.SetActive(false);
        if (autoTabButton != null) autoTabButton.gameObject.SetActive(loadMode);
        if (quickTabButton != null) quickTabButton.gameObject.SetActive(loadMode);

        SetStripEntryVisual(quickTabButton, loadMode && currentSlotType == SaveSlotType.Quick);
        SetStripEntryVisual(autoTabButton, loadMode && currentSlotType == SaveSlotType.Auto);
        ConfigureManualPageButtons();
        LayoutStrip();
    }

    private void ConfigureManualPageButtons()
    {
        if (manualPageButtons == null)
        {
            return;
        }

        bool manualActive = currentSlotType == SaveSlotType.Manual;
        for (int index = 0; index < manualPageButtons.Length; index++)
        {
            Button pageButton = manualPageButtons[index];
            if (pageButton == null)
            {
                continue;
            }

            int page = index + 1;
            pageButton.gameObject.SetActive(page <= SaveManager.ManualPageCount);
            pageButton.interactable = page <= SaveManager.ManualPageCount;
            SetButtonLabel(pageButton, page.ToString());
            SetStripEntryVisual(pageButton, manualActive && page == currentManualPage);
        }
    }

    private List<Button> GetVisibleStripButtons()
    {
        List<Button> strip = new List<Button>();
        if (mode == PanelMode.Load)
        {
            if (IsInteractive(quickTabButton)) strip.Add(quickTabButton);
            if (IsInteractive(autoTabButton)) strip.Add(autoTabButton);
        }

        if (manualPageButtons != null)
        {
            for (int page = 1; page <= SaveManager.ManualPageCount && page <= manualPageButtons.Length; page++)
            {
                Button pageButton = manualPageButtons[page - 1];
                if (IsInteractive(pageButton))
                {
                    strip.Add(pageButton);
                }
            }
        }

        return strip;
    }

    private Button GetActiveStripEntry()
    {
        if (mode == PanelMode.Load)
        {
            if (currentSlotType == SaveSlotType.Quick) return IsInteractive(quickTabButton) ? quickTabButton : null;
            if (currentSlotType == SaveSlotType.Auto) return IsInteractive(autoTabButton) ? autoTabButton : null;
        }

        return GetManualPageButton(currentManualPage);
    }

    private void LayoutStrip()
    {
        if (compactNavigationRoot == null)
        {
            return;
        }

        const float gap = 10f;
        List<Button> strip = GetVisibleStripButtons();
        float totalWidth = strip.Sum(GetStripEntryWidth) + gap * Mathf.Max(0, strip.Count - 1);
        float cursor = -totalWidth * 0.5f;
        foreach (Button entry in strip)
        {
            float width = GetStripEntryWidth(entry);
            SetCompactButtonLayout(entry, cursor + width * 0.5f, width);
            cursor += width + gap;
        }
    }

    private float GetStripEntryWidth(Button entry)
    {
        return entry != null && (entry == quickTabButton || entry == autoTabButton) ? 96f : 56f;
    }

    private static void SetCompactButtonLayout(Button button, float x, float width, float y = 0f)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, 38f);
    }

    private static string GetUnavailableSlotMessage(SaveSlotInfo slot)
    {
        if (slot == null || !slot.IsOccupied)
        {
            return "В этом слоте нет сохранения";
        }

        return "Сохранение недоступно";
    }

    private void ConfigureNavigation()
    {
        Button firstSlot = FindFirstInteractiveSlotButton();
        Button gridEntry = firstSlot ?? closeButton;
        Button activeStripEntry = GetActiveStripEntry();
        List<Button> strip = GetVisibleStripButtons();

        // Explicit navigation is rebuilt only from visible strip entries, so
        // keyboard/controller focus can never land on a hidden legacy control.
        for (int index = 0; index < strip.Count; index++)
        {
            Button left = index > 0 ? strip[index - 1] : closeButton;
            Button right = index < strip.Count - 1 ? strip[index + 1] : closeButton;
            SetNavigation(strip[index], left, right, gridEntry, gridEntry);
        }

        if (closeButton != null)
        {
            SetNavigation(
                closeButton,
                strip.Count > 0 ? strip[strip.Count - 1] : gridEntry,
                strip.Count > 0 ? strip[0] : gridEntry,
                gridEntry,
                activeStripEntry ?? gridEntry);
        }

        for (int index = 0; slotViews != null && index < slotViews.Length; index++)
        {
            ManualSaveSlotView view = slotViews[index];
            Button slotButton = view != null ? view.button : null;
            if (!IsInteractive(slotButton))
            {
                continue;
            }

            int column = index % 3;
            int row = index / 3;
            Button left = FindInteractiveSlotInDirection(index, -1, row, true) ?? activeStripEntry;
            Button right = FindInteractiveSlotInDirection(index, 1, row, true) ?? (view != null && IsInteractive(view.deleteButton) ? view.deleteButton : closeButton);
            Button up = row == 0
                ? activeStripEntry ?? gridEntry
                : FindSlotButton(index - 3) ?? activeStripEntry ?? gridEntry;
            Button down = row == 0
                ? FindSlotButton(index + 3) ?? activeStripEntry ?? gridEntry
                : activeStripEntry ?? gridEntry;
            SetNavigation(slotButton, left, right, up, down);

            if (view != null && IsInteractive(view.deleteButton))
            {
                SetNavigation(view.deleteButton, slotButton, closeButton, slotButton, slotButton);
            }
        }
    }

    private Button GetManualPageButton(int page)
    {
        if (manualPageButtons == null || page < 1 || page > manualPageButtons.Length)
        {
            return null;
        }

        Button pageButton = manualPageButtons[page - 1];
        return IsInteractive(pageButton) ? pageButton : null;
    }

    private Button FindInteractiveSlotInDirection(int startIndex, int direction, int row, bool sameRow)
    {
        for (int index = startIndex + direction;
             slotViews != null && index >= 0 && index < slotViews.Length;
             index += direction)
        {
            if (sameRow && index / 3 != row)
            {
                break;
            }

            Button candidate = FindSlotButton(index);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private Button FindSlotButton(int index)
    {
        if (slotViews == null || index < 0 || index >= slotViews.Length)
        {
            return null;
        }

        Button candidate = slotViews[index] != null ? slotViews[index].button : null;
        return IsInteractive(candidate) ? candidate : null;
    }

    private Button FindFirstInteractiveSlotButton()
    {
        if (slotViews == null)
        {
            return null;
        }

        for (int index = 0; index < slotViews.Length; index++)
        {
            Button candidate = FindSlotButton(index);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private Button GetTabButton(SaveSlotType slotType)
    {
        return slotType switch
        {
            SaveSlotType.Auto => autoTabButton,
            SaveSlotType.Quick => quickTabButton,
            _ => manualTabButton
        };
    }

    private static bool IsInteractive(Button button)
    {
        return button != null && button.isActiveAndEnabled && button.interactable;
    }

    private static void SetNavigation(Button button, Button left, Button right, Button up, Button down)
    {
        if (button == null)
        {
            return;
        }

        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = left,
            selectOnRight = right,
            selectOnUp = up,
            selectOnDown = down
        };
        button.navigation = navigation;
    }

    private static void FocusButton(Button target)
    {
        if (!IsInteractive(target))
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        eventSystem?.SetSelectedGameObject(target.gameObject);
    }

    private SaveManager ResolveSaveManager()
    {
        if (SaveManager.Instance != null)
        {
            return SaveManager.Instance;
        }

        DialogueSceneRegistry registry = VNDialogueController.Instance != null
            ? VNDialogueController.Instance.sceneRegistry
            : FindAnyObjectByType<MainMenuController>()?.dialogueRegistry;
        return SaveManager.EnsureInstance(registry);
    }

    private void SetConfirmationVisible(bool visible, bool immediate)
    {
        if (confirmationAnimation != null)
        {
            StopCoroutine(confirmationAnimation);
            confirmationAnimation = null;
        }

        if (confirmationRoot == null)
        {
            SetContentInteractive(!visible);
            return;
        }

        SetContentInteractive(!visible);
        if (!visible || immediate)
        {
            if (confirmationCanvasGroup != null)
            {
                confirmationCanvasGroup.alpha = visible ? 1f : 0f;
                confirmationCanvasGroup.interactable = visible;
                confirmationCanvasGroup.blocksRaycasts = visible;
            }

            if (confirmationWindow != null)
            {
                confirmationWindow.localScale = Vector3.one;
            }

            confirmationRoot.SetActive(visible);
            FocusConfirmationCancel(visible);
            return;
        }

        confirmationRoot.SetActive(true);
        FocusConfirmationCancel(true);
        confirmationAnimation = StartCoroutine(AnimateConfirmation());
    }

    private IEnumerator AnimateConfirmation()
    {
        if (confirmationCanvasGroup != null)
        {
            confirmationCanvasGroup.alpha = 0f;
            confirmationCanvasGroup.interactable = false;
            confirmationCanvasGroup.blocksRaycasts = true;
        }

        if (confirmationWindow != null)
        {
            confirmationWindow.localScale = Vector3.one * 0.96f;
        }

        float elapsed = 0f;
        while (elapsed < ConfirmationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ConfirmationDuration));
            if (confirmationCanvasGroup != null)
            {
                confirmationCanvasGroup.alpha = t;
            }

            if (confirmationWindow != null)
            {
                confirmationWindow.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
            }

            yield return null;
        }

        if (confirmationCanvasGroup != null)
        {
            confirmationCanvasGroup.alpha = 1f;
            confirmationCanvasGroup.interactable = true;
        }

        if (confirmationWindow != null)
        {
            confirmationWindow.localScale = Vector3.one;
        }

        confirmationAnimation = null;
    }

    private Vector3 GetWindowPresentationScale()
    {
        // Both player canvases already use Scale With Screen Size at 1920x1080.
        // A second screen-space scale here would shrink the panel twice at 1280x720.
        return Vector3.one;
    }

    private bool HasOperationInProgress()
    {
        return IsOperationInProgress;
    }

    private void FocusConfirmationCancel(bool visible)
    {
        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        if (visible && confirmationNoButton != null && eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(confirmationNoButton.gameObject);
        }
    }

    private bool RejectReplayOperation(string operation)
    {
        if (!SceneFlowManager.IsReplayModeActive)
        {
            return false;
        }

        Debug.LogWarning($"[REPLAY] {operation} denied.", this);
        return true;
    }

    private void SetContentInteractive(bool interactive)
    {
        if (contentCanvasGroup == null)
        {
            return;
        }

        contentCanvasGroup.interactable = interactive;
        contentCanvasGroup.blocksRaycasts = interactive;
    }

    private void RefreshContinueAvailability()
    {
        MainMenuController mainMenu = FindAnyObjectByType<MainMenuController>();
        if (mainMenu != null)
        {
            mainMenu.RefreshContinueAvailability();
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private static void BindTabButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetStripEntryVisual(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        if (button.targetGraphic is Image background)
        {
            background.color = active
                ? new Color(0.10f, 0.25f, 0.36f, 0.96f)
                : new Color(0.032f, 0.055f, 0.085f, 0.72f);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = active
                ? new Color(0.9f, 0.97f, 1f, 1f)
                : new Color(0.52f, 0.62f, 0.73f, 0.9f);
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = active
                ? ActiveTabOutlineColor
                : InactiveTabOutlineColor;
        }

        Transform accent = button.transform.Find("Active Accent");
        if (accent != null)
        {
            accent.gameObject.SetActive(active);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
        {
            return;
        }

        if (statusAnimation != null)
        {
            StopCoroutine(statusAnimation);
            statusAnimation = null;
        }

        statusText.text = message;
        statusText.color = isError
            ? new Color(0.92f, 0.48f, 0.52f, 1f)
            : new Color(0.72f, 0.82f, 0.94f, 1f);

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = string.IsNullOrEmpty(message) ? 0f : 1f;
        }

        if (!string.IsNullOrEmpty(message))
        {
            statusAnimation = StartCoroutine(HideStatusAfterDelay());
        }
    }

    private IEnumerator HideStatusAfterDelay()
    {
        float elapsed = 0f;
        while (elapsed < statusVisibleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        const float fadeDuration = 0.28f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (statusCanvasGroup != null)
            {
                statusCanvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
            }

            yield return null;
        }

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = 0f;
        }

        if (statusText != null)
        {
            statusText.text = string.Empty;
        }

        statusAnimation = null;
    }
}
