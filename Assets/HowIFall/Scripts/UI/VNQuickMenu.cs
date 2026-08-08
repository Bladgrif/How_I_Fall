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
    public Button mainMenuButton;

    private static readonly Color NormalColor = new Color(0.035f, 0.07f, 0.11f, 0.82f);
    private static readonly Color ActiveColor = new Color(0.12f, 0.31f, 0.43f, 0.96f);

    private void Awake()
    {
        Bind(historyButton, () => TryInvokeQuickMenuAction(() => dialogueController.ShowBacklog()));
        Bind(skipButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleSkip()));
        Bind(autoButton, () => TryInvokeQuickMenuAction(() => dialogueController.ToggleAutoForward()));
        Bind(saveButton, () => TryInvokeQuickMenuAction(() => dialogueController.manualSaveLoadPanel?.OpenSave()));
        Bind(quickSaveButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickSave()));
        Bind(quickLoadButton, () => TryInvokeQuickMenuAction(() => dialogueController.RequestQuickLoad()));
        Bind(loadButton, () => TryInvokeQuickMenuAction(() => dialogueController.manualSaveLoadPanel?.OpenLoad()));
        Bind(settingsButton, () => TryInvokeQuickMenuAction(() => dialogueController.OpenSettings()));
        Bind(mainMenuButton, () => TryInvokeQuickMenuAction(() => dialogueController.ShowConfirmExit()));
    }

    private bool hiddenBySpecialMode;

    private void Update()
    {
        RefreshSpecialModeVisibility();
        UpdateActiveState(skipButton, dialogueController != null && dialogueController.IsSkipEnabled);
        UpdateActiveState(autoButton, dialogueController != null && dialogueController.IsAutoForwardEnabledState);
    }

    public void RefreshSpecialModeVisibility()
    {
        bool canOpenQuickMenu = dialogueController == null || dialogueController.CanOpenQuickMenu;
        if (!canOpenQuickMenu && root != null && root.activeSelf)
        {
            root.SetActive(false);
            hiddenBySpecialMode = true;
        }
        else if (canOpenQuickMenu && hiddenBySpecialMode && root != null)
        {
            root.SetActive(true);
            hiddenBySpecialMode = false;
        }
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
}
