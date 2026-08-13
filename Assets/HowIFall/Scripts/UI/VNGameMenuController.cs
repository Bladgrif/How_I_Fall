using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local navigation owner. Existing gameplay systems retain their own data and behavior.</summary>
public sealed class VNGameMenuController : MonoBehaviour
{
    private enum ChildContext
    {
        None,
        Preferences,
        History,
        Characters,
        SaveLoad,
        MainMenuConfirmation
    }

    private enum LocalConfirmationAction
    {
        None,
        EndReplay,
        Quit
    }

    private VNDialogueController dialogueController;
    private VNGameMenuView view;
    private readonly VNGameMenuSaveLoadAdapter saveLoadAdapter = new VNGameMenuSaveLoadAdapter();
    private ChildContext childContext;
    private LocalConfirmationAction localConfirmationAction;

    public bool IsOpen { get; private set; }
    public bool IsPresentationVisible => IsOpen && view != null && view.IsVisible;
    public bool IsLocalConfirmationOpen => IsOpen && view != null && view.IsConfirmationVisible;
    public VNGameMenuView View => view;

    public static VNGameMenuController TryCreateRuntime(VNDialogueController dialogueController)
    {
        if (dialogueController == null)
        {
            return null;
        }

        VNGameMenuController existing = dialogueController.GetComponent<VNGameMenuController>();
        if (existing != null)
        {
            return existing;
        }

        VNGameMenuView runtimeView = VNGameMenuView.Create(dialogueController.transform);
        if (runtimeView == null)
        {
            return null;
        }

        VNGameMenuController controller = dialogueController.gameObject.AddComponent<VNGameMenuController>();
        controller.InitializeRuntime(dialogueController, runtimeView);
        return controller;
    }

    public bool Open()
    {
        if (IsOpen)
        {
            RestorePresentation();
            return true;
        }

        if (dialogueController == null || view == null || !dialogueController.CanOpenGameMenu)
        {
            return false;
        }

        IsOpen = true;
        childContext = ChildContext.None;
        localConfirmationAction = LocalConfirmationAction.None;
        view.SetReplayMode(SceneFlowManager.IsReplayModeActive);
        view.SetVisible(true);
        dialogueController.OnGameMenuOpened();
        return true;
    }

    public bool Close()
    {
        if (!IsOpen || childContext != ChildContext.None)
        {
            return false;
        }

        view?.HideConfirmation();
        view?.SetVisible(false);
        localConfirmationAction = LocalConfirmationAction.None;
        IsOpen = false;
        dialogueController?.OnGameMenuClosed();
        return true;
    }

    public bool TryHandleEscape()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (IsLocalConfirmationOpen)
        {
            CancelLocalConfirmation();
            return true;
        }

        return childContext == ChildContext.None && Close();
    }

    public void NotifyPreferencesClosed()
    {
        RestoreFromChild(ChildContext.Preferences);
    }

    public void NotifyHistoryClosed()
    {
        RestoreFromChild(ChildContext.History);
    }

    public void NotifyCharactersClosed()
    {
        RestoreFromChild(ChildContext.Characters);
    }

    public void NotifyMainMenuConfirmationClosed()
    {
        RestoreFromChild(ChildContext.MainMenuConfirmation);
    }

    private void InitializeRuntime(VNDialogueController controller, VNGameMenuView runtimeView)
    {
        dialogueController = controller;
        view = runtimeView;
        Bind(VNGameMenuAction.Return, HandleReturn);
        Bind(VNGameMenuAction.Save, OpenSave);
        Bind(VNGameMenuAction.Load, OpenLoad);
        Bind(VNGameMenuAction.Preferences, OpenPreferences);
        Bind(VNGameMenuAction.History, OpenHistory);
        Bind(VNGameMenuAction.Characters, OpenCharacters);
        Bind(VNGameMenuAction.MainMenu, OpenMainMenuConfirmation);
        Bind(VNGameMenuAction.EndReplay, ConfirmEndReplay);
        Bind(VNGameMenuAction.Quit, ConfirmQuit);

        if (view.ConfirmationYesButton != null)
        {
            view.ConfirmationYesButton.onClick.AddListener(AcceptLocalConfirmation);
        }

        if (view.ConfirmationNoButton != null)
        {
            view.ConfirmationNoButton.onClick.AddListener(CancelLocalConfirmation);
        }

        view.SetVisible(false);
    }

    private void Update()
    {
        if (!IsOpen || childContext != ChildContext.SaveLoad)
        {
            return;
        }

        ManualSaveLoadPanel panel = dialogueController != null ? dialogueController.manualSaveLoadPanel : null;
        if (panel == null || !panel.IsOpen)
        {
            CloseSaveLoadSection();
            return;
        }

        view.SetSaveLoadSection(panel.IsSaveMode ? VNGameMenuAction.Save : VNGameMenuAction.Load, panel.IsConfirmationOpen);
    }

    private void LateUpdate()
    {
        if (IsOpen && childContext == ChildContext.SaveLoad)
        {
            saveLoadAdapter.RefreshLayout();
        }
    }

    private void OpenPreferences()
    {
        if (!HideForChild(ChildContext.Preferences))
        {
            return;
        }

        dialogueController.OpenSettings();
        if (!dialogueController.IsPreferencesOpen)
        {
            RestoreFromChild(ChildContext.Preferences);
        }
    }

    private void OpenHistory()
    {
        if (!HideForChild(ChildContext.History))
        {
            return;
        }

        dialogueController.ShowBacklog();
        if (dialogueController.backlogPanel == null || !dialogueController.backlogPanel.activeSelf)
        {
            RestoreFromChild(ChildContext.History);
        }
    }

    private void OpenCharacters()
    {
        if (!HideForChild(ChildContext.Characters))
        {
            return;
        }

        if (!dialogueController.OpenCharacterHub())
        {
            RestoreFromChild(ChildContext.Characters);
        }
    }

    private void OpenSave()
    {
        OpenSaveLoad(save: true);
    }

    private void OpenLoad()
    {
        OpenSaveLoad(save: false);
    }

    private void OpenSaveLoad(bool save)
    {
        ManualSaveLoadPanel panel = dialogueController != null ? dialogueController.manualSaveLoadPanel : null;
        if (panel == null || view == null || view.SaveLoadContentHost == null)
        {
            return;
        }

        if (childContext != ChildContext.None && childContext != ChildContext.SaveLoad)
        {
            return;
        }

        if (childContext == ChildContext.None)
        {
            if (!IsPresentationVisible || IsLocalConfirmationOpen || !saveLoadAdapter.Mount(panel, view.SaveLoadContentHost))
            {
                return;
            }

            childContext = ChildContext.SaveLoad;
            view.SetSaveLoadSection(save ? VNGameMenuAction.Save : VNGameMenuAction.Load);
        }

        if (save)
        {
            panel.OpenSave();
        }
        else
        {
            panel.OpenLoad();
        }

        if (!panel.IsOpen)
        {
            CloseSaveLoadSection();
            return;
        }

        view.SetSaveLoadSection(save ? VNGameMenuAction.Save : VNGameMenuAction.Load, panel.IsConfirmationOpen);
    }

    private void HandleReturn()
    {
        if (childContext == ChildContext.SaveLoad)
        {
            ManualSaveLoadPanel panel = saveLoadAdapter.Panel;
            if (panel != null && panel.IsOpen)
            {
                panel.HandleEscape();
            }
            else
            {
                CloseSaveLoadSection();
            }

            return;
        }

        Close();
    }

    private void CloseSaveLoadSection()
    {
        if (childContext != ChildContext.SaveLoad)
        {
            return;
        }

        saveLoadAdapter.Unmount();
        childContext = ChildContext.None;
        view?.SetSaveLoadSection(null);
    }

    private void OpenMainMenuConfirmation()
    {
        if (!HideForChild(ChildContext.MainMenuConfirmation))
        {
            return;
        }

        dialogueController.ShowConfirmExitFromGameMenu();
        if (dialogueController.confirmExitPanel == null || !dialogueController.confirmExitPanel.activeSelf)
        {
            RestoreFromChild(ChildContext.MainMenuConfirmation);
        }
    }

    private void ConfirmEndReplay()
    {
        if (!SceneFlowManager.IsReplayModeActive || !IsPresentationVisible)
        {
            return;
        }

        localConfirmationAction = LocalConfirmationAction.EndReplay;
        view.ShowConfirmation("Завершить повтор и вернуться в главное меню?");
    }

    private void ConfirmQuit()
    {
        if (!IsPresentationVisible)
        {
            return;
        }

        localConfirmationAction = LocalConfirmationAction.Quit;
        view.ShowConfirmation("Выйти из игры?");
    }

    private void AcceptLocalConfirmation()
    {
        LocalConfirmationAction accepted = localConfirmationAction;
        CancelLocalConfirmation();
        if (accepted == LocalConfirmationAction.EndReplay)
        {
            dialogueController?.ReturnToMainMenu();
        }
        else if (accepted == LocalConfirmationAction.Quit)
        {
            SceneFlowManager.EnsureInstance().QuitGame();
        }
    }

    private void CancelLocalConfirmation()
    {
        localConfirmationAction = LocalConfirmationAction.None;
        view?.HideConfirmation();
    }

    private bool HideForChild(ChildContext context)
    {
        if (!IsPresentationVisible || IsLocalConfirmationOpen || childContext != ChildContext.None)
        {
            return false;
        }

        childContext = context;
        view.SetVisible(false);
        return true;
    }

    private void RestoreFromChild(ChildContext expected)
    {
        if (!IsOpen || childContext != expected)
        {
            return;
        }

        childContext = ChildContext.None;
        RestorePresentation();
    }

    private void RestorePresentation()
    {
        if (!IsOpen || childContext != ChildContext.None || view == null)
        {
            return;
        }

        view.SetReplayMode(SceneFlowManager.IsReplayModeActive);
        view.SetVisible(true);
    }

    private void OnDestroy()
    {
        saveLoadAdapter.Unmount();
    }

    private void Bind(VNGameMenuAction action, UnityEngine.Events.UnityAction callback)
    {
        Button button = view != null ? view.GetButton(action) : null;
        button?.onClick.AddListener(callback);
    }
}
