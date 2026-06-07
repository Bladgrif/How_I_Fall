using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private GameObject loadPanel;
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
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            SceneFlowManager.EnsureInstance().ContinueGame();
            return;
        }

        ShowNotification("Нет сохранения");
        Debug.LogWarning("No save file found.");
    }

    public void OpenLoadPanel()
    {
        if (loadPanel == null)
        {
            ContinueGame();
            return;
        }

        saveLoadMode = SaveLoadMode.Load;
        saveLoadPage = 1;
        loadPanel.SetActive(true);
        RefreshSaveLoadPanel();
    }

    public void CloseLoadPanel()
    {
        if (loadPanel != null)
        {
            loadPanel.SetActive(false);
        }
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
            SceneFlowManager.EnsureInstance().ContinueGame();
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
    public Button button;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI previewText;
    public Image previewImage;

    private int visibleSlotIndex;

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
            previewText.text = hasSave ? "Превью недоступно" : string.Empty;
        }

        if (previewImage != null)
        {
            previewImage.color = hasSave
                ? new Color(0.05f, 0.08f, 0.13f, 0.9f)
                : new Color(0f, 0f, 0f, 0f);
        }
    }

    public void Click()
    {
        if (controller != null)
        {
            controller.OnSaveLoadSlotClicked(visibleSlotIndex);
        }
    }
}
