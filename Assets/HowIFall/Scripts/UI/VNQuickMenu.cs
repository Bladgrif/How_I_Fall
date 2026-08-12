using System.Collections.Generic;
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

    private static readonly Color NormalColor = new Color(0.035f, 0.07f, 0.11f, 0.82f);
    private static readonly Color ActiveColor = new Color(0.12f, 0.31f, 0.43f, 0.96f);
    private string normalMainMenuLabel;

    private void Awake()
    {
        EnsureCharactersButton();
        CacheNormalMainMenuLabel();
        Bind(historyButton, () => TryInvokeQuickMenuAction(() => dialogueController.ShowBacklog()));
        Bind(skipButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleSkip()));
        Bind(autoButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleAutoForward()));
        Bind(saveButton, () => TryInvokeQuickMenuAction(() => dialogueController.manualSaveLoadPanel?.OpenSave()));
        Bind(quickSaveButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickSave()));
        Bind(quickLoadButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickLoad()));
        Bind(loadButton, () => TryInvokeQuickMenuAction(() => dialogueController.manualSaveLoadPanel?.OpenLoad()));
        Bind(settingsButton, () => TryInvokeQuickMenuAction(() => dialogueController.OpenSettings()));
        Bind(charactersButton, () => TryInvokeQuickMenuAction(() => dialogueController.OpenCharacterHub()));
        Bind(mainMenuButton, () => TryInvokeQuickMenuAction(HandleMainMenuAction));
        RefreshReplayPresentation();
        RefreshEffectiveVisibility();
    }

    public bool EnsureCharactersButton()
    {
        if (charactersButton != null)
        {
            return false;
        }

        if (settingsButton == null || settingsButton.transform.parent == null)
        {
            return false;
        }

        GameObject clone = Instantiate(settingsButton.gameObject, settingsButton.transform.parent);
        clone.name = "Characters Runtime Button";
        charactersButton = clone.GetComponent<Button>();
        charactersButton.onClick.RemoveAllListeners();
        TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = "Characters";
        }

        clone.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex() + 1);
        return true;
    }

    private bool hiddenBySpecialMode;
    private bool hiddenByPlayer;
    private bool hiddenByPreferencesModal;
    private bool hiddenByGameMenuModal;

    private void Update()
    {
        RefreshEffectiveVisibility();
        RefreshReplayPresentation();
        UpdateActiveState(skipButton, dialogueController != null && dialogueController.IsSkipEnabled);
        UpdateActiveState(autoButton, dialogueController != null && dialogueController.IsAutoForwardEnabledState);
    }

    public void RefreshSpecialModeVisibility()
    {
        RefreshEffectiveVisibility();
    }

    private void RefreshEffectiveVisibility()
    {
        // Each modal owner contributes only its own temporary blocker.
        hiddenBySpecialMode = dialogueController != null
            && dialogueController.HasActiveSpecialMode
            && !dialogueController.CanOpenQuickMenu;

        bool visible = !hiddenByPlayer
            && !hiddenBySpecialMode
            && !hiddenByPreferencesModal
            && !hiddenByGameMenuModal;
        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    /// <summary>
    /// Temporary visual ownership for gameplay Preferences. This never changes
    /// persistent Quick Menu data or any Quick Menu action semantics.
    /// </summary>
    public void SetPreferencesModalHidden(bool hidden)
    {
        hiddenByPreferencesModal = hidden;
        RefreshEffectiveVisibility();
    }

    /// <summary>Temporary Game Menu blocker. It never mutates clean-view or other visibility state.</summary>
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
        CacheNormalMainMenuLabel();
        SetButtonVisible(saveButton, !replay);
        SetButtonVisible(quickSaveButton, !replay);
        SetButtonVisible(quickLoadButton, !replay);
        SetButtonVisible(loadButton, !replay);
        SetButtonLabel(mainMenuButton, replay ? "End Replay" : normalMainMenuLabel);
    }

    private void CacheNormalMainMenuLabel()
    {
        string currentLabel = GetButtonLabel(mainMenuButton);
        if (!string.IsNullOrWhiteSpace(currentLabel) && currentLabel != "End Replay")
        {
            normalMainMenuLabel = currentLabel;
        }
    }

    private void HandleMainMenuAction()
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

    private static string GetButtonLabel(Button button)
    {
        TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        return text != null ? text.text : string.Empty;
    }
}
