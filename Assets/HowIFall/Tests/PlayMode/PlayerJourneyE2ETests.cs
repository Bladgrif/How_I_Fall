using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    [Category("PlayerJourneyE2E")]
    public sealed class PlayerJourneyE2ETests
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string GameplaySceneName = "VNPrototype";
        private const float DefaultTimeoutSeconds = 15f;

        private static readonly PrefSpec[] PreferenceSpecs =
        {
            PrefSpec.Float("hif_master_volume"),
            PrefSpec.Float("hif_music_volume"),
            PrefSpec.Float("hif_sfx_volume"),
            PrefSpec.Int("hif_mute_all"),
            PrefSpec.Float("hif_ambient_volume"),
            PrefSpec.Int("hif_music_during_pause"),
            PrefSpec.String("hif_screen_mode"),
            PrefSpec.String("hif_resolution"),
            PrefSpec.String("hif_refresh_rate"),
            PrefSpec.String("hif_game_look"),
            PrefSpec.String("hif_interface_style"),
            PrefSpec.Int("hif_rewind_vhs_filter"),
            PrefSpec.Int("hif_run_in_background"),
            PrefSpec.Int("hif_character_animations"),
            PrefSpec.Int("hif_background_animations"),
            PrefSpec.String("hif_language"),
            PrefSpec.String("hif_font_size_mode"),
            PrefSpec.String("hif_skip_mode"),
            PrefSpec.String("hif_skip_behavior"),
            PrefSpec.Float("hif_text_speed"),
            PrefSpec.Float("hif_dialogue_text_scale"),
            PrefSpec.Float("hif_textbox_opacity"),
            PrefSpec.Float("hif_auto_forward_delay"),
            PrefSpec.Int("hif_skip_after_choices"),
            PrefSpec.Int("hif_auto_forward"),
            PrefSpec.Int("hif_auto_save"),
            PrefSpec.Int("hif_show_hints"),
            PrefSpec.Int("hif_show_quick_menu"),
            PrefSpec.Int("hif_fullscreen"),
            PrefSpec.String("hif_dialogue_read_history_v1")
        };

        private readonly Dictionary<string, PrefValue> originalPreferences = new Dictionary<string, PrefValue>();
        private string temporaryRoot;
        private float originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            CapturePreferences();

            temporaryRoot = Path.Combine(Path.GetTempPath(), "HowIFall_PlayerJourneyE2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);

            yield return LoadScene(MainMenuSceneName);
            yield return WaitForCondition(
                () => FindObject<MainMenuController>() != null && SaveManager.Instance != null && SettingsManager.Instance != null,
                "Main Menu runtime services did not become ready.");

            SaveManager.Instance.ConfigureSaveDirectoryForTests(Path.Combine(temporaryRoot, "Saves"));
            SaveManager.ScreenshotCaptureOverrideForTests = CreatePreviewTexture;
            SettingsManager.Instance.SetAutoSave(false);
            SettingsManager.Instance.SetShowQuickMenu(true);
            FindObject<MainMenuController>().RefreshContinueAvailability();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;

            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                yield return LoadScene(MainMenuSceneName);
            }

            RestorePreferences();
            SaveManager.ScreenshotCaptureOverrideForTests = null;
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.LoadSettings();
            }

            DestroyAll<SaveManager>();
            DestroyAll<SceneFlowManager>();
            DestroyAll<GameState>();
            DestroyAll<SettingsManager>();
            DestroyAll<AudioManager>();
            yield return null;

            if (!string.IsNullOrEmpty(temporaryRoot) && Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }

            Time.timeScale = originalTimeScale;
        }

        [UnityTest]
        public IEnumerator MainMenuJourney_PreferencesQuitAndEmptyLoadReturnToMenu()
        {
            MainMenuController menu = FindObject<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.PlayerFacingActionButtons.Count, Is.EqualTo(5));

            Click(menu.PlayerFacingActionButtons[3], "Main Menu Preferences");
            yield return WaitForCondition(
                () => menu.settingsPanel.SharedController.IsOpen,
                "Preferences did not open from Main Menu.");
            SharedPreferencesView preferences = FindPreferencesView("MainMenu");
            Assert.That(preferences, Is.Not.Null.And.Property("IsVisible").True);
            TMP_Dropdown screenModeSelector = preferences.GetDropdown(SharedPreferencesView.ScreenModeId);
            Assert.That(screenModeSelector, Is.Not.Null.And.Property("interactable").True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(screenModeSelector.gameObject),
                "Main Menu Preferences did not assign deterministic dropdown focus.");
            string previousScreenMode = SettingsManager.Instance.settings.screenMode;
            screenModeSelector.value = (screenModeSelector.value + 1) % screenModeSelector.options.Count;
            yield return null;
            Assert.That(SettingsManager.Instance.settings.screenMode, Is.Not.EqualTo(previousScreenMode),
                "Screen Mode dropdown did not apply its selected setting.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null,
                "Screen Mode selector interaction left an invalid selected object.");
            Toggle quickMenuToggle = preferences.GetToggle(SharedPreferencesView.ShowQuickMenuId);
            Assert.That(quickMenuToggle, Is.Not.Null);
            quickMenuToggle.isOn = false;
            yield return null;
            Assert.That(SettingsManager.Instance.settings.showQuickMenu, Is.False, "The player-facing setting did not reach its runtime consumer.");
            Click(preferences.GetButton("back"), "Preferences Back");
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.PlayerFacingActionButtons[3].gameObject),
                "Closing Main Menu Preferences did not restore Settings focus.");
            Hover(menu.PlayerFacingActionButtons[1], "Main Menu New Game after Preferences");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.PlayerFacingActionButtons[1].gameObject),
                "Mouse hover retained the Settings selection alongside another Main Menu action.");

            Click(menu.PlayerFacingActionButtons[3], "Reopen Main Menu Preferences");
            yield return null;
            Assert.That(preferences.IsVisible, Is.True);
            Assert.That(preferences.GetToggle(SharedPreferencesView.ShowQuickMenuId).isOn, Is.False,
                "Preferences did not retain the changed value after reopen.");
            Click(preferences.GetButton("reset"), "Preferences Reset");
            yield return null;
            Assert.That(preferences.GetToggle(SharedPreferencesView.ShowQuickMenuId).isOn, Is.True,
                "Preferences Reset did not restore the expected default.");
            Click(preferences.GetButton("back"), "Preferences Back after Reset");
            yield return null;

            Click(menu.PlayerFacingActionButtons[4], "Main Menu Quit");
            yield return null;
            GameObject exitPanel = FindSceneObject("Exit Confirm Panel");
            Assert.That(exitPanel, Is.Not.Null.And.Property("activeSelf").True, "Quit confirmation did not open.");
            Assert.That(exitPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Any(text => text.GetComponentInParent<Button>(true) == null
                        && text.text == "Вы действительно хотите выйти из игры?"),
                Is.True, "Main Menu Quit must not warn about unsaved gameplay progress.");
            Button quitConfirm = FindButtonWithRoute(menu, nameof(MainMenuController.ConfirmExit));
            Hover(quitConfirm, "Quit confirmation Yes");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quitConfirm.gameObject),
                "Quit confirmation mouse hover left the safe default and destructive action active together.");
            Click(FindButtonWithRoute(menu, nameof(MainMenuController.CloseExitConfirm)), "Quit Cancel");
            yield return null;
            Assert.That(exitPanel.activeSelf, Is.False, "Quit Cancel did not return to Main Menu.");
            Assert.That(menu.PlayerFacingActionButtons[1].interactable, Is.True, "Main Menu stopped responding after Quit Cancel.");

            Click(menu.PlayerFacingActionButtons[2], "Main Menu Load");
            yield return WaitForCondition(() => menu.manualSaveLoadPanel.IsOpen, "Load did not open from Main Menu.");
            Assert.That(menu.manualSaveLoadPanel.slotViews, Has.Length.EqualTo(SaveManager.SlotCount));
            Assert.That(menu.manualSaveLoadPanel.slotViews.All(slot => slot.emptyText != null && slot.emptyText.gameObject.activeSelf),
                Is.True, "Empty Load did not render every slot as empty.");
            Assert.That(menu.manualSaveLoadPanel.slotViews.All(slot => slot.button != null && !slot.button.interactable),
                Is.True, "Empty Load exposed an invalid load action.");
            Click(menu.manualSaveLoadPanel.closeButton, "Load Back");
            yield return WaitForCondition(() => !menu.manualSaveLoadPanel.IsOpen, "Load Back did not return to Main Menu.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuSceneName));
        }

        [UnityTest]
        public IEnumerator GameplayNavigationJourney_NewGameAndModalBackStackRestoreDialogueShell()
        {
            VNDialogueController dialogue = null;
            yield return StartGameplay(result => dialogue = result);
            VNQuickMenu quickMenu = FindObject<VNQuickMenu>(true);
            AssertGameplayShell(dialogue, quickMenu);
            AssertReadingQuickMenuContract(quickMenu);

            Click(quickMenu.autoButton, "Quick Menu Auto");
            yield return null;
            Assert.That(dialogue.IsAutoForwardEnabledState, Is.True, "Quick Menu Auto did not enable the existing Auto state.");
            Click(quickMenu.autoButton, "Quick Menu Auto off");
            yield return null;
            Assert.That(dialogue.IsAutoForwardEnabledState, Is.False, "Quick Menu Auto did not restore its existing inactive state.");

            string skipModeBefore = SettingsManager.Instance.settings.skipMode;
            SettingsManager.Instance.settings.skipMode = "Всё";
            Click(quickMenu.skipButton, "Quick Menu Skip");
            yield return null;
            Assert.That(dialogue.IsSkipEnabled, Is.True, "Quick Menu Skip did not enable the existing seen-aware Skip state.");
            Click(quickMenu.skipButton, "Quick Menu Skip off");
            yield return null;
            Assert.That(dialogue.IsSkipEnabled, Is.False, "Quick Menu Skip did not restore its existing inactive state.");
            SettingsManager.Instance.settings.skipMode = skipModeBefore;

            int lineBeforeAdvance = GameState.Instance.currentLineIndex;
            string sceneBeforeAdvance = GameState.Instance.currentSceneId;
            Click(dialogue.nextButton, "Dialogue Next");
            yield return null;
            Click(dialogue.nextButton, "Dialogue Next after type completion");
            yield return WaitForCondition(
                () => GameState.Instance.currentLineIndex != lineBeforeAdvance
                    || GameState.Instance.currentSceneId != sceneBeforeAdvance
                    || (dialogue.choicePanel != null && dialogue.choicePanel.activeSelf),
                "New Game reached gameplay, but dialogue could not be advanced.");

            int stableLineIndex = GameState.Instance.currentLineIndex;
            string stableSceneId = GameState.Instance.currentSceneId;
            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Ordinary gameplay Esc did not open the Game Menu.");
            yield return WaitForCondition(() => dialogue.IsGameMenuOpen, "Game Menu did not open from ordinary gameplay Esc.");
            VNGameMenuController gameMenu = dialogue.GameMenuController;
            Assert.That(gameMenu.IsPresentationVisible, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(gameMenu.View.GetButton(VNGameMenuAction.Return).gameObject),
                "Game Menu did not assign deterministic default focus.");
            Assert.That(dialogue.dialogueUiRoot.activeSelf, Is.False, "Game Menu did not hide the dialogue shell.");
            Assert.That(quickMenu.IsEffectivelyVisible, Is.False, "Game Menu did not block Quick Menu presentation.");

            Click(gameMenu.View.GetButton(VNGameMenuAction.Preferences), "Game Menu Preferences");
            yield return WaitForCondition(() => dialogue.IsPreferencesOpen, "Preferences did not open from Game Menu.");
            Assert.That(gameMenu.IsPresentationVisible, Is.False);
            SharedPreferencesView gameplayPreferences = FindPreferencesView("Gameplay");
            Assert.That(gameplayPreferences.IsVisible, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(gameplayPreferences.GetDropdown(SharedPreferencesView.ScreenModeId).gameObject),
                "Gameplay Preferences did not assign deterministic default focus.");
            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Preferences Back/Esc was not handled.");
            yield return WaitForCondition(() => gameMenu.IsPresentationVisible, "Preferences Back did not restore Game Menu.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(gameMenu.View.GetButton(VNGameMenuAction.Return).gameObject),
                "Closing Gameplay Preferences did not restore a valid Game Menu focus target.");

            Click(gameMenu.View.GetButton(VNGameMenuAction.MainMenu), "Game Menu Main Menu confirmation");
            yield return WaitForCondition(
                () => dialogue.confirmExitPanel != null && dialogue.confirmExitPanel.activeSelf,
                "Game Menu confirmation did not open.");
            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Confirmation Cancel/Esc was not handled.");
            yield return WaitForCondition(() => gameMenu.IsPresentationVisible, "Confirmation Cancel did not restore Game Menu.");

            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Game Menu Back/Esc was not handled.");
            yield return WaitForCondition(() => !dialogue.IsGameMenuOpen, "Game Menu Back did not return to gameplay.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null,
                "Closing Game Menu left a stale selected object in the gameplay EventSystem.");
            AssertGameplayShell(dialogue, quickMenu);

            Click(quickMenu.historyButton, "Quick Menu History");
            yield return WaitForCondition(
                () => dialogue.backlogPanel != null && dialogue.backlogPanel.activeSelf,
                "History did not open from Quick Menu.");
            Click(dialogue.nextButton, "Dialogue Next while History is open");
            yield return null;
            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo(stableSceneId));
            Assert.That(GameState.Instance.currentLineIndex, Is.EqualTo(stableLineIndex),
                "Closing/opening History leaked a dialogue advance.");
            Assert.That(dialogue.HandleEscapePressed(), Is.True, "History Back/Esc was not handled.");
            yield return WaitForCondition(() => !dialogue.backlogPanel.activeSelf, "History Back did not return to gameplay.");
            AssertGameplayShell(dialogue, quickMenu);

            Assert.That(quickMenu.IsCharacterHubLauncherVisible, Is.False,
                "Character Hub technical launcher must remain hidden from the current player-facing demo.");
            Assert.That(quickMenu.charactersButton, Is.Not.Null,
                "Character Hub technical runtime object must remain available to its foundation tests.");
            Assert.That(quickMenu.charactersButton.gameObject.activeSelf, Is.False,
                "Character Hub technical launcher must remain inactive in ordinary gameplay.");

            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo(stableSceneId));
            Assert.That(GameState.Instance.currentLineIndex, Is.EqualTo(stableLineIndex),
                "The modal Back stack advanced dialogue unexpectedly.");
        }

        [UnityTest]
        public IEnumerator ManualSaveLoadJourney_FilledSlotRestoresStateAndGameplay()
        {
            VNDialogueController dialogue = null;
            yield return StartGameplay(result => dialogue = result);
            VNQuickMenu quickMenu = FindObject<VNQuickMenu>(true);
            const int savedLust = 17;
            GameState.Instance.lust = savedLust;

            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Esc did not open Game Menu for Manual Save.");
            yield return WaitForCondition(() => dialogue.IsGameMenuOpen, "Game Menu did not open for Manual Save.");
            VNGameMenuController gameMenu = dialogue.GameMenuController;
            Click(gameMenu.View.GetButton(VNGameMenuAction.Save), "Game Menu Save");
            ManualSaveLoadPanel panel = dialogue.manualSaveLoadPanel;
            yield return WaitForCondition(() => panel.IsOpen && panel.IsSaveMode, "Manual Save screen did not open.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.slotViews[0].button.gameObject),
                "Manual Save did not assign deterministic default focus.");
            panel.SelectManualPage(2);
            yield return null;
            Assert.That(panel.CurrentManualPage, Is.EqualTo(2));
            Click(panel.slotViews[0].button, "Manual Save page 2 slot 1");
            yield return WaitForCondition(
                () => !panel.IsOperationInProgress && SaveManager.Instance.GetSlot(SaveSlotType.Manual, 7).IsLoadable,
                "Manual Save did not produce a filled page-2 loadable slot.");

            SaveSlotInfo savedSlot = SaveManager.Instance.GetSlot(SaveSlotType.Manual, 7);
            Assert.That(savedSlot.Data.lust, Is.EqualTo(savedLust));
            Assert.That(File.Exists(savedSlot.JsonPath), Is.True, "Manual Save JSON was not written.");
            Assert.That(File.Exists(savedSlot.PreviewPath), Is.True, "Manual Save preview was not written.");
            Assert.That(new FileInfo(savedSlot.PreviewPath).Length, Is.GreaterThan(0), "Manual Save preview is empty.");
            Assert.That(panel.slotViews[0].sceneNameText.gameObject.activeSelf, Is.True, "Filled slot did not appear in the UI.");

            GameState.Instance.lust = 93;
            Assert.That(panel.HandleEscape(), Is.True, "Manual Save Back/Esc was not handled.");
            yield return WaitForCondition(
                () => !panel.IsOpen && gameMenu.IsPresentationVisible,
                "Closing Manual Save did not restore Game Menu.");
            Click(gameMenu.View.GetButton(VNGameMenuAction.Load), "Game Menu Load");
            yield return WaitForCondition(() => panel.IsOpen && !panel.IsSaveMode, "Manual Load screen did not open.");
            Assert.That(panel.CurrentManualPage, Is.EqualTo(1), "Opening Load must deterministically start on page 1.");
            panel.SelectManualPage(2);
            yield return null;
            Click(panel.slotViews[0].button, "Manual Load page 2 slot 1");
            yield return WaitForCondition(() => panel.IsConfirmationOpen, "Manual Load confirmation did not open.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.confirmationNoButton.gameObject),
                "Manual Load confirmation did not focus the safe Cancel action.");
            Assert.That(panel.HandleEscape(), Is.True, "Manual Load confirmation cancel was not handled.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(panel.slotViews[0].button.gameObject),
                "Cancelling Manual Load confirmation did not restore the slot focus.");
            Click(panel.slotViews[0].button, "Reopen Manual Load confirmation");
            yield return WaitForCondition(() => panel.IsConfirmationOpen, "Manual Load confirmation did not reopen.");
            Click(panel.confirmationYesButton, "Confirm Manual Load");
            yield return WaitForCondition(
                () => GameState.Instance.lust == savedLust && !panel.IsOperationInProgress,
                "Manual Load did not restore the saved GameState.");
            yield return WaitForCondition(
                () => !panel.IsOpen && gameMenu.IsPresentationVisible,
                "Manual Load did not return to Game Menu.");
            Click(gameMenu.View.GetButton(VNGameMenuAction.Return), "Return to gameplay after Manual Load");
            yield return WaitForCondition(() => !dialogue.IsGameMenuOpen, "Manual Load journey did not return to gameplay.");
            AssertGameplayShell(dialogue, quickMenu);
        }

        [UnityTest]
        public IEnumerator QuickSaveLoadJourney_PlayerActionsRestoreStateAndRuntimeShell()
        {
            VNDialogueController dialogue = null;
            yield return StartGameplay(result => dialogue = result);
            VNQuickMenu quickMenu = FindObject<VNQuickMenu>(true);
            const int savedTrust = 24;
            GameState.Instance.trustMasha = savedTrust;

            Click(quickMenu.quickSaveButton, "Quick Save");
            yield return WaitForCondition(
                () => SaveManager.Instance.GetAllSlots(SaveSlotType.Quick).Any(slot => slot.IsLoadable),
                "Quick Save player action did not create a loadable slot.");
            GameState.Instance.trustMasha = 81;

            Assert.That(quickMenu.quickLoadButton.gameObject.activeSelf, Is.False,
                "Quick Load must remain available only through its existing input/API route.");
            dialogue.RequestQuickLoad();
            ManualSaveLoadPanel panel = dialogue.manualSaveLoadPanel;
            yield return WaitForCondition(
                () => panel.IsOpen && panel.IsConfirmationOpen && panel.CurrentSlotType == SaveSlotType.Quick,
                "Quick Load did not reach its confirmation.");
            Click(panel.confirmationYesButton, "Confirm Quick Load");
            yield return WaitForCondition(
                () => GameState.Instance.trustMasha == savedTrust && !panel.IsOperationInProgress,
                "Quick Load did not restore the Quick Save state.");
            yield return WaitForCondition(() => !panel.IsOpen, "Quick Load panel did not close after restore.");
            AssertGameplayShell(dialogue, quickMenu);
        }

        [UnityTest]
        public IEnumerator ContinueJourney_LoadsNewestValidSaveThroughMainMenu()
        {
            VNDialogueController dialogue = null;
            yield return StartGameplay(result => dialogue = result);
            Texture2D preview = CreatePreviewTexture();
            try
            {
                GameState.Instance.romance = 11;
                Assert.That(SaveManager.Instance.SaveSlot(SaveSlotType.Manual, 1, preview), Is.True);
                SetSaveTimestamp(SaveSlotType.Manual, 1, "2026-08-13T08:00:00.0000000Z");

                GameState.Instance.romance = 22;
                Assert.That(SaveManager.Instance.SaveSlot(SaveSlotType.Quick, 1, preview), Is.True);
                SetSaveTimestamp(SaveSlotType.Quick, 1, "2026-08-13T09:00:00.0000000Z");
                GameState.Instance.romance = 99;
            }
            finally
            {
                UnityEngine.Object.Destroy(preview);
            }

            yield return ReturnToMainMenuThroughGameMenu(dialogue);
            MainMenuController menu = FindObject<MainMenuController>();
            menu.RefreshContinueAvailability();
            Assert.That(menu.continueButton.interactable, Is.True, "Continue stayed disabled with valid saves.");
            Click(menu.continueButton, "Continue newest valid");
            yield return WaitForGameplayReady();
            Assert.That(GameState.Instance.romance, Is.EqualTo(22), "Continue did not restore the newest valid save.");
        }

        [UnityTest]
        public IEnumerator ContinueJourney_IgnoresNewerInvalidCandidate()
        {
            VNDialogueController dialogue = null;
            yield return StartGameplay(result => dialogue = result);
            Texture2D preview = CreatePreviewTexture();
            try
            {
                GameState.Instance.selfControl = 31;
                Assert.That(SaveManager.Instance.SaveSlot(SaveSlotType.Manual, 1, preview), Is.True);
                SetSaveTimestamp(SaveSlotType.Manual, 1, "2026-08-13T08:00:00.0000000Z");

                GameState.Instance.selfControl = 44;
                Assert.That(SaveManager.Instance.SaveSlot(SaveSlotType.Quick, 1, preview), Is.True);
                SetSaveTimestamp(SaveSlotType.Quick, 1, "2026-08-13T09:00:00.0000000Z");

                GameState.Instance.selfControl = 77;
                Assert.That(SaveManager.Instance.SaveSlot(SaveSlotType.Auto, 1, preview), Is.True);
                CorruptSavePositionWithFutureTimestamp(SaveSlotType.Auto, 1);
                Assert.That(SaveManager.Instance.GetSlot(SaveSlotType.Auto, 1).IsLoadable, Is.False,
                    "The test fixture failed to create an invalid newest candidate.");
                GameState.Instance.selfControl = 5;
            }
            finally
            {
                UnityEngine.Object.Destroy(preview);
            }

            yield return ReturnToMainMenuThroughGameMenu(dialogue);
            MainMenuController menu = FindObject<MainMenuController>();
            menu.RefreshContinueAvailability();
            Assert.That(menu.continueButton.interactable, Is.True);
            Click(menu.continueButton, "Continue with invalid newest candidate");
            yield return WaitForGameplayReady();
            Assert.That(GameState.Instance.selfControl, Is.EqualTo(44),
                "Continue selected an invalid newer candidate instead of the newest valid save.");
        }

        private IEnumerator StartGameplay(Action<VNDialogueController> completed)
        {
            MainMenuController menu = FindObject<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Click(menu.PlayerFacingActionButtons[1], "New Game");
            yield return WaitForGameplayReady();
            VNDialogueController dialogue = FindObject<VNDialogueController>();
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(dialogue.IsRuntimeReady, Is.True);
            Assert.That(dialogue.dialogueUiRoot, Is.Not.Null.And.Property("activeSelf").True);
            Assert.That(dialogue.IsPreferencesOpen, Is.False);
            Assert.That(dialogue.IsGameMenuOpen, Is.False);
            Assert.That(dialogue.IsCharacterHubOpen, Is.False);
            Assert.That(dialogue.manualSaveLoadPanel == null || !dialogue.manualSaveLoadPanel.IsOpen, Is.True);
            completed(dialogue);
        }

        private static IEnumerator WaitForGameplayReady()
        {
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == GameplaySceneName
                    && VNDialogueController.Instance != null
                    && VNDialogueController.Instance.IsRuntimeReady,
                "Gameplay/VN runtime did not become ready after navigation.");
        }

        private static IEnumerator ReturnToMainMenuThroughGameMenu(VNDialogueController dialogue)
        {
            Assert.That(dialogue.HandleEscapePressed(), Is.True, "Esc did not open Game Menu before returning to Main Menu.");
            yield return WaitForCondition(() => dialogue.IsGameMenuOpen, "Game Menu did not open before returning to Main Menu.");
            Click(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.MainMenu), "Game Menu Main Menu");
            yield return WaitForCondition(
                () => dialogue.confirmExitPanel != null && dialogue.confirmExitPanel.activeSelf,
                "Return to Main Menu confirmation did not open.");
            Click(dialogue.confirmExitYesButton, "Confirm Return to Main Menu");
            yield return WaitForCondition(
                () => SceneManager.GetActiveScene().name == MainMenuSceneName && FindObject<MainMenuController>() != null,
                "Confirmed Main Menu route did not complete.");
            yield return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Scene '{sceneName}' could not start loading.");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, string failureMessage, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(failureMessage + $" Timeout: {timeoutSeconds:0.#} seconds.");
        }

        private static void AssertGameplayShell(VNDialogueController dialogue, VNQuickMenu quickMenu)
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameplaySceneName));
            Assert.That(dialogue.IsRuntimeReady, Is.True);
            Assert.That(dialogue.dialogueUiRoot.activeSelf, Is.True, "Dialogue shell is not visible in ordinary gameplay.");
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "A modal left dialogue-shell ownership behind.");
            Assert.That(dialogue.IsPreferencesOpen, Is.False);
            Assert.That(dialogue.IsGameMenuOpen, Is.False);
            Assert.That(dialogue.IsCharacterHubOpen, Is.False);
            Assert.That(quickMenu.IsEffectivelyVisible, Is.True, "Quick Menu did not restore the enabled player preference.");
        }

        private static void AssertReadingQuickMenuContract(VNQuickMenu quickMenu)
        {
            Button[] visibleButtons = quickMenu.root.GetComponentsInChildren<Button>(true)
                .Where(button => button.transform.parent == quickMenu.root.transform && button.gameObject.activeSelf)
                .OrderBy(button => button.transform.GetSiblingIndex())
                .ToArray();
            Assert.That(visibleButtons, Is.EqualTo(new[]
            {
                quickMenu.historyButton, quickMenu.skipButton, quickMenu.autoButton, quickMenu.quickSaveButton
            }), "Ordinary reading must expose only History / Skip / Auto / Quick Save in Quick Menu order.");
            Assert.That(quickMenu.saveButton.gameObject.activeSelf, Is.False);
            Assert.That(quickMenu.quickLoadButton.gameObject.activeSelf, Is.False);
            Assert.That(quickMenu.loadButton.gameObject.activeSelf, Is.False);
            Assert.That(quickMenu.settingsButton.gameObject.activeSelf, Is.False);
            Assert.That(quickMenu.mainMenuButton.gameObject.activeSelf, Is.False);
            Assert.That(FindSceneObject("How I Fall Logo")?.activeSelf, Is.False,
                "Temporary title logo must be hidden from ordinary reading.");
            Assert.That(FindSceneObject("Chapter Info")?.activeSelf, Is.False,
                "Temporary chapter label must be hidden from ordinary reading.");
            Assert.That(FindSceneObject("Top Left Soft Shade")?.activeSelf, Is.False,
                "Temporary title backing surface must be hidden from ordinary reading.");
        }

        private static void AssertAboutBodyInsideWindow(GameObject aboutPanel)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform window = aboutPanel.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.parent == aboutPanel.transform && rect.name.Contains("Window"));
            TextMeshProUGUI body = aboutPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(text => text.GetComponentInParent<Button>(true) == null && text.text.Contains("How I Fall"));
            Assert.That(window, Is.Not.Null, "About window is missing.");
            Assert.That(body, Is.Not.Null, "About body is missing.");
            Bounds bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, body.rectTransform);
            Rect windowRect = window.rect;
            const float tolerance = 1f;
            Assert.That(bodyBounds.min.x, Is.GreaterThanOrEqualTo(windowRect.xMin - tolerance));
            Assert.That(bodyBounds.max.x, Is.LessThanOrEqualTo(windowRect.xMax + tolerance));
            Assert.That(bodyBounds.min.y, Is.GreaterThanOrEqualTo(windowRect.yMin - tolerance));
            Assert.That(bodyBounds.max.y, Is.LessThanOrEqualTo(windowRect.yMax + tolerance));
        }

        private static SharedPreferencesView FindPreferencesView(string contextId)
        {
            return UnityEngine.Object.FindObjectsByType<SharedPreferencesView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(view => view.ContextId == contextId);
        }

        private static Button FindButtonWithRoute(UnityEngine.Object target, string methodName)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                    .Any(index => button.onClick.GetPersistentTarget(index) == target
                        && button.onClick.GetPersistentMethodName(index) == methodName));
        }

        private static GameObject FindSceneObject(string name)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(transform => transform.gameObject.scene == SceneManager.GetActiveScene())
                .FirstOrDefault(transform => transform.name == name)
                ?.gameObject;
        }

        private static T FindObject<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        private static void Click(Button button, string actionName)
        {
            Assert.That(button, Is.Not.Null, $"Player-facing button is missing: {actionName}.");
            Assert.That(button.gameObject.activeInHierarchy, Is.True, $"Player-facing button is hidden: {actionName}.");
            Assert.That(button.interactable, Is.True, $"Player-facing button is disabled: {actionName}.");
            button.onClick.Invoke();
        }

        private static void Hover(Button button, string actionName)
        {
            Assert.That(button, Is.Not.Null, $"Player-facing button is missing: {actionName}.");
            ExecuteEvents.Execute<IPointerEnterHandler>(
                button.gameObject,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerEnterHandler);
        }

        private static Texture2D CreatePreviewTexture()
        {
            var texture = new Texture2D(16, 9, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Repeat(new Color(0.08f, 0.16f, 0.24f, 1f), 16 * 9).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void SetSaveTimestamp(SaveSlotType type, int slotIndex, string timestamp)
        {
            SaveSlotInfo slot = SaveManager.Instance.GetSlot(type, slotIndex);
            Assert.That(slot.IsLoadable, Is.True, $"Cannot timestamp invalid {type} slot {slotIndex}.");
            slot.Data.createdAtUtc = timestamp;
            File.WriteAllText(slot.JsonPath, JsonUtility.ToJson(slot.Data, true));
            Assert.That(SaveManager.Instance.GetSlot(type, slotIndex).IsLoadable, Is.True);
        }

        private static void CorruptSavePositionWithFutureTimestamp(SaveSlotType type, int slotIndex)
        {
            SaveSlotInfo slot = SaveManager.Instance.GetSlot(type, slotIndex);
            Assert.That(slot.IsLoadable, Is.True);
            slot.Data.createdAtUtc = "2099-12-31T23:59:59.0000000Z";
            slot.Data.lineId = "missing_technical_test_line";
            File.WriteAllText(slot.JsonPath, JsonUtility.ToJson(slot.Data, true));
        }

        private void CapturePreferences()
        {
            originalPreferences.Clear();
            foreach (PrefSpec spec in PreferenceSpecs)
            {
                originalPreferences[spec.Key] = PrefValue.Capture(spec);
            }
        }

        private void RestorePreferences()
        {
            foreach (PrefSpec spec in PreferenceSpecs)
            {
                PrefValue value = originalPreferences[spec.Key];
                if (!value.Existed)
                {
                    PlayerPrefs.DeleteKey(spec.Key);
                    continue;
                }

                switch (spec.Type)
                {
                    case PrefType.Float:
                        PlayerPrefs.SetFloat(spec.Key, value.FloatValue);
                        break;
                    case PrefType.Int:
                        PlayerPrefs.SetInt(spec.Key, value.IntValue);
                        break;
                    default:
                        PlayerPrefs.SetString(spec.Key, value.StringValue);
                        break;
                }
            }

            PlayerPrefs.Save();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component != null)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                }
            }
        }

        private enum PrefType
        {
            Float,
            Int,
            String
        }

        private readonly struct PrefSpec
        {
            private PrefSpec(string key, PrefType type)
            {
                Key = key;
                Type = type;
            }

            public string Key { get; }
            public PrefType Type { get; }
            public static PrefSpec Float(string key) => new PrefSpec(key, PrefType.Float);
            public static PrefSpec Int(string key) => new PrefSpec(key, PrefType.Int);
            public static PrefSpec String(string key) => new PrefSpec(key, PrefType.String);
        }

        private readonly struct PrefValue
        {
            private PrefValue(bool existed, float floatValue, int intValue, string stringValue)
            {
                Existed = existed;
                FloatValue = floatValue;
                IntValue = intValue;
                StringValue = stringValue;
            }

            public bool Existed { get; }
            public float FloatValue { get; }
            public int IntValue { get; }
            public string StringValue { get; }

            public static PrefValue Capture(PrefSpec spec)
            {
                bool existed = PlayerPrefs.HasKey(spec.Key);
                return spec.Type switch
                {
                    PrefType.Float => new PrefValue(existed, PlayerPrefs.GetFloat(spec.Key), 0, string.Empty),
                    PrefType.Int => new PrefValue(existed, 0f, PlayerPrefs.GetInt(spec.Key), string.Empty),
                    _ => new PrefValue(existed, 0f, 0, PlayerPrefs.GetString(spec.Key, string.Empty))
                };
            }
        }
    }
}
