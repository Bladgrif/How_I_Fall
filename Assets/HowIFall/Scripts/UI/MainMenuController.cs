using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;
    private const int SaveLoadPageSize = 8;
    private const int SaveLoadTotalPages = 20;

    private enum SaveLoadMode
    {
        Auto,
        Load,
        Save
    }

    public SettingsPanelController settingsPanel;
    public SaveLoadPanelController saveLoadPanel;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private GameObject loadPanel;
    [SerializeField] private GameObject[] objectsToHideWhenLoadOpen;
    [SerializeField] private Button autoSavesButton;
    [SerializeField] private Button manualSavesButton;
    [SerializeField] private Button saveModeButton;
    [SerializeField] private TextMeshProUGUI autoSavesLabel;
    [SerializeField] private TextMeshProUGUI manualSavesLabel;
    [SerializeField] private TextMeshProUGUI saveModeLabel;
    [SerializeField] private Button previousSavePageButton;
    [SerializeField] private Button nextSavePageButton;
    [SerializeField] private TextMeshProUGUI savePageText;
    [SerializeField] private SaveLoadSlotButton[] saveSlotButtons;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private GameObject notificationPanel;

    private Coroutine notificationCoroutine;
    private SaveLoadMode saveLoadMode = SaveLoadMode.Load;
    private int saveLoadPage = 1;

    public void StartGame()
    {
        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueGame()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.HasAnySave())
        {
            SceneFlowManager.EnsureInstance().ContinueGame();
            return;
        }

        ShowNotification("Нет сохранения");
        Debug.LogWarning("No save file found.");
    }

    public void OpenLoadPanel()
    {
        if (saveLoadPanel != null)
        {
            saveLoadPanel.OpenLoad();
            return;
        }

        if (loadPanel == null)
        {
            ShowNotification("Экран загрузки недоступен");
            return;
        }

        saveLoadMode = SaveLoadMode.Load;
        saveLoadPage = 1;
        SetLoadHiddenObjectsActive(false);
        loadPanel.SetActive(true);
        RefreshSaveLoadPanel();
    }

    public void CloseLoadPanel()
    {
        if (loadPanel != null)
        {
            loadPanel.SetActive(false);
        }

        SetLoadHiddenObjectsActive(true);
    }

    public void ShowAutoSaves()
    {
        saveLoadMode = SaveLoadMode.Auto;
        saveLoadPage = 1;
        RefreshSaveLoadPanel();
    }

    public void ShowManualSaves()
    {
        saveLoadMode = SaveLoadMode.Load;
        saveLoadPage = 1;
        RefreshSaveLoadPanel();
    }

    public void ShowSaveMode()
    {
        ShowNotification("Сохранение доступно из игры");
    }

    public void PreviousSavePage()
    {
        saveLoadPage = Mathf.Max(1, saveLoadPage - 1);
        RefreshSaveLoadPanel();
    }

    public void NextSavePage()
    {
        saveLoadPage = Mathf.Min(SaveLoadTotalPages, saveLoadPage + 1);
        RefreshSaveLoadPanel();
    }

    public void OnSaveLoadSlotClicked(int visibleSlotIndex)
    {
        if (saveLoadMode == SaveLoadMode.Save)
        {
            ShowNotification("Сохранение доступно из игры");
            return;
        }

        int slotIndex = (saveLoadPage - 1) * SaveLoadPageSize + visibleSlotIndex + 1;
        bool isAuto = saveLoadMode == SaveLoadMode.Auto;

        if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveSlot(slotIndex, isAuto))
        {
            ShowNotification("Пустой слот");
            return;
        }

        if (SaveManager.Instance.LoadSlot(slotIndex, isAuto))
        {
            SceneFlowManager.EnsureInstance().LoadLoadedGameScene();
            return;
        }

        ShowNotification("Пустой слот");
    }

    public void DeleteSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("SaveManager.Instance is missing.");
            return;
        }

        SaveManager.Instance.DeleteSave();
    }

    private void RefreshSaveLoadPanel()
    {
        RefreshSaveLoadModeVisuals();
        RefreshSaveLoadPage();
        RefreshSaveLoadSlots();
    }

    private void RefreshSaveLoadModeVisuals()
    {
        SetModeLabel(autoSavesLabel, saveLoadMode == SaveLoadMode.Auto, true);
        SetModeLabel(manualSavesLabel, saveLoadMode == SaveLoadMode.Load, true);
        SetModeLabel(saveModeLabel, false, false);

        if (autoSavesButton != null)
        {
            autoSavesButton.interactable = true;
        }

        if (manualSavesButton != null)
        {
            manualSavesButton.interactable = true;
        }

        if (saveModeButton != null)
        {
            saveModeButton.interactable = false;
        }
    }

    private void RefreshSaveLoadPage()
    {
        if (savePageText != null)
        {
            savePageText.text = $"{saveLoadPage} / {SaveLoadTotalPages}";
        }

        if (previousSavePageButton != null)
        {
            previousSavePageButton.interactable = saveLoadPage > 1;
        }

        if (nextSavePageButton != null)
        {
            nextSavePageButton.interactable = saveLoadPage < SaveLoadTotalPages;
        }
    }

    private void RefreshSaveLoadSlots()
    {
        if (saveSlotButtons == null)
        {
            return;
        }

        List<SaveSlotInfo> slots = saveLoadMode == SaveLoadMode.Auto
            ? SaveManager.Instance?.GetAutoSaveSlots(saveLoadPage, SaveLoadPageSize)
            : SaveManager.Instance?.GetManualSaveSlots(saveLoadPage, SaveLoadPageSize);

        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            SaveLoadSlotButton slotButton = saveSlotButtons[i];
            if (slotButton == null)
            {
                continue;
            }

            SaveSlotInfo slot = slots != null && i < slots.Count ? slots[i] : null;
            slotButton.SetSlotInfo(slot, i);
        }
    }

    private void SetModeLabel(TextMeshProUGUI label, bool active, bool enabled)
    {
        if (label == null)
        {
            return;
        }

        if (!enabled)
        {
            label.color = new Color(1f, 1f, 1f, 0.35f);
            return;
        }

        label.color = active ? Color.white : new Color(1f, 1f, 1f, 0.72f);
    }

    private void SetLoadHiddenObjectsActive(bool isActive)
    {
        if (objectsToHideWhenLoadOpen == null)
        {
            return;
        }

        foreach (GameObject target in objectsToHideWhenLoadOpen)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        Debug.LogWarning("SettingsPanelController is not assigned.");
    }

    public void OpenAbout()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(true);
        }
    }

    public void OpenHelp()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
    }

    public void CloseAbout()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(false);
        }
    }

    public void CloseHelp()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }

    public void OpenExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(true);
            return;
        }

        ExitGame();
    }

    public void CloseExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(false);
        }
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

    public void OpenGallery()
    {
        Debug.Log("Gallery is not implemented yet");
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}

public class SaveLoadSlotButton : MonoBehaviour
{
    public MainMenuController controller;
    public SaveLoadPanelController panelController;
    public Button button;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI previewText;
    public Image previewImage;

    private int visibleSlotIndex;
    private Sprite previewSprite;

    public void SetSlotInfo(SaveSlotInfo slot, int visibleIndex)
    {
        visibleSlotIndex = visibleIndex;

        bool hasSave = slot != null && slot.HasSave;
        int slotIndex = slot == null ? visibleIndex + 1 : slot.SlotIndex;

        if (titleText != null)
        {
            titleText.text = hasSave ? $"Слот {slotIndex}" : string.Empty;
        }

        if (dateText != null)
        {
            dateText.text = hasSave ? slot.SaveDateText : string.Empty;
        }

        if (previewText != null)
        {
            previewText.text = string.Empty;
        }

        if (previewImage != null)
        {
            ApplyPreview(slot, hasSave);
        }
    }

    public void Click()
    {
        if (panelController != null)
        {
            panelController.OnSlotClicked(visibleSlotIndex);
            return;
        }

        if (controller != null)
        {
            controller.OnSaveLoadSlotClicked(visibleSlotIndex);
        }
    }

    private void ApplyPreview(SaveSlotInfo slot, bool hasSave)
    {
        if (previewSprite != null)
        {
            Destroy(previewSprite.texture);
            Destroy(previewSprite);
            previewSprite = null;
        }

        previewImage.sprite = null;
        previewImage.color = hasSave ? new Color(0.05f, 0.08f, 0.13f, 0.9f) : new Color(0f, 0f, 0f, 0f);

        if (!hasSave || slot == null || string.IsNullOrEmpty(slot.PreviewPath) || !File.Exists(slot.PreviewPath))
        {
            return;
        }

        byte[] bytes = File.ReadAllBytes(slot.PreviewPath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            Destroy(texture);
            return;
        }

        previewSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        previewImage.sprite = previewSprite;
        previewImage.preserveAspect = true;
        previewImage.color = Color.white;
    }
}

public class SaveLoadPanelController : MonoBehaviour
{
    private const int PageSize = 8;
    private const int TotalPages = 20;

    private enum Mode
    {
        Auto,
        Load,
        Save
    }

    public GameObject root;
    public bool saveEnabled;
    public MainMenuController mainMenuController;
    public VNDialogueController vnController;
    public GameObject[] objectsToHideWhenOpen;
    public Button autoButton;
    public Button loadButton;
    public Button saveButton;
    public TextMeshProUGUI autoLabel;
    public TextMeshProUGUI loadLabel;
    public TextMeshProUGUI saveLabel;
    public Button previousPageButton;
    public Button nextPageButton;
    public TextMeshProUGUI pageText;
    public SaveLoadSlotButton[] slotButtons;

    private Mode mode = Mode.Load;
    private int page = 1;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }
    }

    public void OpenAuto()
    {
        Open(Mode.Auto);
    }

    public void OpenLoad()
    {
        Open(Mode.Load);
    }

    public void OpenSave()
    {
        if (!saveEnabled)
        {
            ShowToast("Сохранение доступно из игры");
            return;
        }

        Open(Mode.Save);
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        SetHiddenObjectsActive(true);
    }

    public void ShowAuto()
    {
        mode = Mode.Auto;
        Refresh();
    }

    public void ShowLoad()
    {
        mode = Mode.Load;
        Refresh();
    }

    public void ShowSave()
    {
        if (!saveEnabled)
        {
            ShowToast("Сохранение доступно из игры");
            return;
        }

        mode = Mode.Save;
        Refresh();
    }

    public void PreviousPage()
    {
        page = Mathf.Max(1, page - 1);
        Refresh();
    }

    public void NextPage()
    {
        page = Mathf.Min(TotalPages, page + 1);
        Refresh();
    }

    public void OnSlotClicked(int visibleSlotIndex)
    {
        int slotIndex = (page - 1) * PageSize + visibleSlotIndex + 1;
        bool isAuto = mode == Mode.Auto;

        if (mode == Mode.Save)
        {
            if (!saveEnabled)
            {
                ShowToast("Сохранение доступно из игры");
                return;
            }

            if (vnController != null)
            {
                StartCoroutine(SaveVnSlotWithPreview(slotIndex));
                return;
            }

            if (SaveManager.Instance == null || !SaveManager.Instance.SaveToSlot(slotIndex, false, GetLinePreview()))
            {
                ShowToast("Не удалось сохранить");
                return;
            }

            ShowToast(SaveManager.Instance.HasSaveSlot(slotIndex, false) ? "Сохранено" : "Слот перезаписан");
            StartCoroutine(RefreshAfterPreviewCapture());
            return;
        }

        if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveSlot(slotIndex, isAuto))
        {
            ShowToast("Пустой слот");
            return;
        }

        if (!SaveManager.Instance.LoadFromSlot(slotIndex, isAuto))
        {
            ShowToast("Не удалось загрузить");
            return;
        }

        if (vnController != null)
        {
            vnController.RestoreLoadedSaveFromPanel();
            Close();
            ShowToast("Загружено");
            return;
        }

        SceneFlowManager.EnsureInstance().LoadLoadedGameScene();
    }

    private IEnumerator SaveVnSlotWithPreview(int slotIndex)
    {
        CanvasGroup group = root != null ? root.GetComponent<CanvasGroup>() : null;
        if (root != null && group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        float previousAlpha = group != null ? group.alpha : 1f;
        bool previousInteractable = group == null || group.interactable;
        bool previousBlocksRaycasts = group == null || group.blocksRaycasts;

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        yield return new WaitForEndOfFrame();

        Texture2D previewTexture = null;
        try
        {
            previewTexture = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"SaveLoadPanelController: preview capture failed. {exception.Message}");
        }

        if (group != null)
        {
            group.alpha = previousAlpha;
            group.interactable = previousInteractable;
            group.blocksRaycasts = previousBlocksRaycasts;
        }

        bool saved = SaveManager.Instance != null
            && SaveManager.Instance.SaveToSlot(slotIndex, false, GetLinePreview(), previewTexture);

        if (previewTexture != null)
        {
            Destroy(previewTexture);
        }

        if (!saved)
        {
            ShowToast("Не удалось сохранить");
            Refresh();
            yield break;
        }

        ShowToast("Сохранено");
        Refresh();
    }

    private void Open(Mode openMode)
    {
        mode = openMode;
        page = 1;
        SetHiddenObjectsActive(false);

        if (root != null)
        {
            root.SetActive(true);
        }

        Refresh();
    }

    private void Refresh()
    {
        SetModeLabel(autoLabel, mode == Mode.Auto, true);
        SetModeLabel(loadLabel, mode == Mode.Load, true);
        SetModeLabel(saveLabel, mode == Mode.Save, saveEnabled);

        if (autoButton != null)
        {
            autoButton.interactable = true;
        }

        if (loadButton != null)
        {
            loadButton.interactable = true;
        }

        if (saveButton != null)
        {
            saveButton.interactable = saveEnabled;
        }

        if (pageText != null)
        {
            pageText.text = $"{page} / {TotalPages}";
        }

        if (previousPageButton != null)
        {
            previousPageButton.interactable = page > 1;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = page < TotalPages;
        }

        List<SaveSlotInfo> slots = mode == Mode.Auto
            ? SaveManager.Instance?.GetAutoSaveSlots(page, PageSize)
            : SaveManager.Instance?.GetManualSaveSlots(page, PageSize);

        if (slotButtons == null)
        {
            return;
        }

        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null)
            {
                continue;
            }

            SaveSlotInfo slot = slots != null && i < slots.Count ? slots[i] : null;
            slotButtons[i].SetSlotInfo(slot, i);
        }
    }

    private IEnumerator RefreshAfterPreviewCapture()
    {
        yield return new WaitForEndOfFrame();
        yield return null;
        Refresh();
    }

    private void SetModeLabel(TextMeshProUGUI label, bool active, bool enabled)
    {
        if (label == null)
        {
            return;
        }

        if (!enabled)
        {
            label.color = new Color(1f, 1f, 1f, 0.35f);
            return;
        }

        label.color = active ? Color.white : new Color(1f, 1f, 1f, 0.72f);
    }

    private void SetHiddenObjectsActive(bool isActive)
    {
        if (objectsToHideWhenOpen == null)
        {
            return;
        }

        foreach (GameObject target in objectsToHideWhenOpen)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }
    }

    private string GetLinePreview()
    {
        return vnController == null ? string.Empty : vnController.GetCurrentLinePreview();
    }

    private void ShowToast(string message)
    {
        if (vnController != null)
        {
            vnController.ShowToastMessage(message);
            return;
        }

        if (mainMenuController != null)
        {
            mainMenuController.ShowNotification(message);
            return;
        }

        Debug.Log(message);
    }
}
