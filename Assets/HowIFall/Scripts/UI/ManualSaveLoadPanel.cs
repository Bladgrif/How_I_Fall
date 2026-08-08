using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
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
    private ConfirmationAction pendingConfirmationAction;
    private SaveSlotType? pendingConfirmationSlotType;
    private int pendingConfirmationSlot;
    private bool saveInProgress;
    private bool loadInProgress;
    private Coroutine panelAnimation;
    private Coroutine confirmationAnimation;
    private Coroutine statusAnimation;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsConfirmationOpen => confirmationRoot != null && confirmationRoot.activeSelf;
    public SaveSlotType CurrentSlotType => currentSlotType;
    public bool IsSaveMode => mode == PanelMode.Save;
    public bool LoadInProgress => loadInProgress;
    public SaveSlotType? PendingConfirmationSlotType => pendingConfirmationSlotType;
    public int PendingConfirmationSlot => pendingConfirmationSlot;

    private void Awake()
    {
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

        SetConfirmationVisible(false, true);
        ApplySlotTypePresentation();
    }

    private void Update()
    {
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
        if (IsOperationInProgress() || !gameObject.activeSelf)
        {
            return;
        }

        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
        StartPanelAnimation(false);
    }

    public bool HandleEscape()
    {
        if (!gameObject.activeSelf || IsOperationInProgress())
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

        if (IsOperationInProgress() || IsConfirmationOpen)
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
            SetStatus(string.IsNullOrEmpty(slotToLoad.Error) ? "Не удалось загрузить слот" : slotToLoad.Error, true);
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
            SetStatus(string.IsNullOrEmpty(slot.Error) ? "Не удалось загрузить слот" : slot.Error, true);
            Refresh();
            return;
        }
    }

    public void OnDeleteRequested(int slotIndex)
    {
        if (IsOperationInProgress() || IsConfirmationOpen)
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
        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
        SetStatus(string.Empty, false);

        if (titleText != null)
        {
            titleText.text = mode == PanelMode.Save ? "Сохранение" : "Загрузка";
        }

        ApplySlotTypePresentation();
        Refresh();
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
            windowRect.localScale = Vector3.one;
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
        Vector3 startScale = windowRect != null
            ? windowRect.localScale
            : Vector3.one * (opening ? 0.985f : 1f);
        Vector3 targetScale = Vector3.one * (opening ? 1f : 0.985f);
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

            SetContentInteractive(true);
            yield break;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
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
        ClearPendingConfirmation();
        SetConfirmationVisible(false, true);
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
            windowRect.localScale = Vector3.one;
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

        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
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

    private void Refresh()
    {
        SaveManager saveManager = ResolveSaveManager();
        if (slotViews == null)
        {
            return;
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            SaveSlotInfo slot = saveManager != null
                ? saveManager.GetSlot(currentSlotType, i + 1)
                : new SaveSlotInfo { SlotType = currentSlotType, SlotIndex = i + 1 };
            slotViews[i]?.Render(slot, mode == PanelMode.Save);
        }
    }

    private void SelectSlotType(SaveSlotType slotType)
    {
        if (IsOperationInProgress() || IsConfirmationOpen || currentSlotType == slotType)
        {
            return;
        }

        currentSlotType = slotType;
        ApplySlotTypePresentation();
        Refresh();
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

        if (slotTypeHintText != null)
        {
            string hint = mode == PanelMode.Save
                ? currentSlotType switch
                {
                    SaveSlotType.Auto => "Автосохранения создаются игрой автоматически",
                    SaveSlotType.Quick => "Быстрые сохранения создаются отдельной командой",
                    _ => string.Empty
                }
                : string.Empty;
            slotTypeHintText.text = hint;
            slotTypeHintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }

        SetTabVisual(manualTabButton, currentSlotType == SaveSlotType.Manual);
        SetTabVisual(autoTabButton, currentSlotType == SaveSlotType.Auto);
        SetTabVisual(quickTabButton, currentSlotType == SaveSlotType.Quick);
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
            return;
        }

        confirmationRoot.SetActive(true);
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

    private bool IsOperationInProgress()
    {
        return saveInProgress || loadInProgress;
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

    private static void SetTabVisual(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        if (button.targetGraphic is Image background)
        {
            background.color = active
                ? new Color(0.075f, 0.145f, 0.22f, 0.96f)
                : new Color(0.032f, 0.055f, 0.085f, 0.72f);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.color = active
                ? new Color(0.88f, 0.95f, 1f, 1f)
                : new Color(0.48f, 0.59f, 0.7f, 0.82f);
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
