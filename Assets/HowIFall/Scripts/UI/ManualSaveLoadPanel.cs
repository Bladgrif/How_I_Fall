using System.Collections;
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

    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI statusText;
    public Button closeButton;
    public ManualSaveSlotView[] slotViews;
    public GameObject confirmationRoot;
    public TextMeshProUGUI confirmationText;
    public Button confirmationYesButton;
    public Button confirmationNoButton;

    private PanelMode mode;
    private int pendingOverwriteSlot;
    private bool saveInProgress;

    public bool IsOpen => gameObject.activeSelf;

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

        if (confirmationYesButton != null)
        {
            confirmationYesButton.onClick.RemoveListener(ConfirmOverwrite);
            confirmationYesButton.onClick.AddListener(ConfirmOverwrite);
        }

        if (confirmationNoButton != null)
        {
            confirmationNoButton.onClick.RemoveListener(CancelOverwrite);
            confirmationNoButton.onClick.AddListener(CancelOverwrite);
        }

        if (slotViews != null)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i]?.Initialize(this, i + 1);
            }
        }

        SetConfirmationVisible(false);
    }

    public void OpenSave()
    {
        if (VNDialogueController.Instance == null)
        {
            Debug.LogWarning("[SAVE UI] Save panel can only be opened from VNPrototype.", this);
            return;
        }

        mode = PanelMode.Save;
        Open();
    }

    public void OpenLoad()
    {
        mode = PanelMode.Load;
        Open();
    }

    public void Close()
    {
        if (saveInProgress)
        {
            return;
        }

        pendingOverwriteSlot = 0;
        SetConfirmationVisible(false);
        gameObject.SetActive(false);
    }

    public void OnSlotSelected(int slotIndex)
    {
        if (saveInProgress)
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
            ManualSaveSlotInfo slot = saveManager.GetSlot(slotIndex);
            if (slot.IsOccupied)
            {
                pendingOverwriteSlot = slotIndex;
                if (confirmationText != null)
                {
                    confirmationText.text = $"Перезаписать слот {slotIndex}?";
                }

                SetConfirmationVisible(true);
                return;
            }

            StartCoroutine(CaptureAndSave(slotIndex));
            return;
        }

        bool loadInsideVn = VNDialogueController.Instance != null;
        if (!saveManager.LoadSlot(slotIndex))
        {
            ManualSaveSlotInfo slot = saveManager.GetSlot(slotIndex);
            SetStatus(string.IsNullOrEmpty(slot.Error) ? "Не удалось загрузить слот" : slot.Error, true);
            Refresh();
            return;
        }

        if (loadInsideVn && this != null)
        {
            Close();
        }
    }

    private void Open()
    {
        gameObject.SetActive(true);
        pendingOverwriteSlot = 0;
        SetConfirmationVisible(false);
        SetStatus(string.Empty, false);

        if (titleText != null)
        {
            titleText.text = mode == PanelMode.Save ? "Сохранение" : "Загрузка";
        }

        Refresh();
    }

    private void ConfirmOverwrite()
    {
        int slotIndex = pendingOverwriteSlot;
        pendingOverwriteSlot = 0;
        SetConfirmationVisible(false);

        if (slotIndex > 0)
        {
            StartCoroutine(CaptureAndSave(slotIndex));
        }
    }

    private void CancelOverwrite()
    {
        pendingOverwriteSlot = 0;
        SetConfirmationVisible(false);
    }

    private IEnumerator CaptureAndSave(int slotIndex)
    {
        saveInProgress = true;
        SetStatus("Создание сохранения...", false);

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
        bool saved = saveManager != null && saveManager.SaveSlot(slotIndex, screenshot);

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
            ManualSaveSlotInfo slot = saveManager != null
                ? saveManager.GetSlot(i + 1)
                : new ManualSaveSlotInfo { SlotIndex = i + 1 };
            slotViews[i]?.Render(slot, mode == PanelMode.Save);
        }
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

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(visible);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = isError
            ? new Color(1f, 0.45f, 0.45f, 1f)
            : new Color(0.85f, 0.9f, 1f, 1f);
    }
}
