using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class HowIFallProjectValidator
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Validate Project")]
    public static void ValidateProject()
    {
        int issueCount = 0;

        issueCount += ValidateBuildSettings();
        issueCount += DialogueContentValidator.Validate();
        issueCount += ValidateMainMenuScene();
        issueCount += ValidateVNPrototypeScene();

        if (issueCount == 0)
        {
            Debug.Log("How I Fall validation passed.");
        }
        else
        {
            Debug.LogError($"How I Fall validation found {issueCount} issue(s).");
        }
    }

    private static int ValidateBuildSettings()
    {
        int issues = 0;
        int mainMenuIndex = FindBuildSettingsSceneIndex(MainMenuScenePath);
        int vnPrototypeIndex = FindBuildSettingsSceneIndex(VNPrototypeScenePath);

        if (mainMenuIndex < 0)
        {
            issues += LogError("Build Settings: MainMenu scene is missing.");
        }

        if (vnPrototypeIndex < 0)
        {
            issues += LogError("Build Settings: VNPrototype scene is missing.");
        }

        if (mainMenuIndex >= 0 && vnPrototypeIndex >= 0 && mainMenuIndex > vnPrototypeIndex)
        {
            issues += LogError("Build Settings: MainMenu must be listed before VNPrototype.");
        }

        return issues;
    }

    private static int ValidateMainMenuScene()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath);

        int issues = 0;
        const string sceneName = "MainMenu";

        issues += ValidateCommonSceneRequirements(sceneName);

        MainMenuController mainMenuController = FindAny<MainMenuController>();
        issues += ValidateRequiredObject(sceneName, mainMenuController, nameof(MainMenuController));

        if (mainMenuController != null)
        {
            issues += ValidateRequiredReference(sceneName, "MainMenuController.settingsPanel", mainMenuController.settingsPanel);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.manualSaveLoadPanel", mainMenuController.manualSaveLoadPanel);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.dialogueRegistry", mainMenuController.dialogueRegistry);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.continueButton", mainMenuController.continueButton);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.helpText", mainMenuController.HelpText);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.galleryPanel", mainMenuController.GalleryPanel);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.galleryReplayButton", mainMenuController.GalleryReplayButton);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.galleryReplayTitle", mainMenuController.GalleryReplayTitle);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.galleryReplayState", mainMenuController.GalleryReplayState);
            issues += ValidateRequiredReference(sceneName, "MainMenuController.galleryLockedOverlay", mainMenuController.GalleryLockedOverlay);
            issues += ValidateGalleryReplayDefinitions(sceneName, mainMenuController);
        }

        issues += ValidateRequiredObject(sceneName, FindAny<SettingsPanelController>(), nameof(SettingsPanelController));
        issues += ValidateRequiredObject(sceneName, FindAny<SceneFlowManager>(), nameof(SceneFlowManager));
        issues += ValidateRequiredObject(sceneName, FindAny<SaveManager>(), nameof(SaveManager));
        issues += ValidateRequiredObject(sceneName, FindAny<GameState>(), nameof(GameState));
        issues += ValidateRequiredObject(sceneName, FindAny<SettingsManager>(), nameof(SettingsManager));
        issues += ValidateRequiredObject(sceneName, FindAny<AudioManager>(), nameof(AudioManager));

        ManualSaveLoadPanel saveLoadPanel = FindAny<ManualSaveLoadPanel>();
        issues += ValidateRequiredObject(sceneName, saveLoadPanel, nameof(ManualSaveLoadPanel));
        if (saveLoadPanel != null && (saveLoadPanel.slotViews == null || saveLoadPanel.slotViews.Length != SaveManager.SlotCount))
        {
            issues += LogError($"{sceneName}: ManualSaveLoadPanel must contain exactly {SaveManager.SlotCount} slot views.");
        }

        return issues;
    }

    private static int ValidateVNPrototypeScene()
    {
        EditorSceneManager.OpenScene(VNPrototypeScenePath);

        int issues = 0;
        const string sceneName = "VNPrototype";

        issues += ValidateCommonSceneRequirements(sceneName);

        VNDialogueController controller = FindAny<VNDialogueController>();
        issues += ValidateRequiredObject(sceneName, controller, nameof(VNDialogueController));

        if (controller == null)
        {
            return issues;
        }

        issues += ValidateRequiredReference(sceneName, "VNDialogueController.sceneData", controller.sceneData);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.sceneRegistry", controller.sceneRegistry);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.speakerText", controller.speakerText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.dialogueText", controller.dialogueText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backgroundImage", controller.backgroundImage);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.characterImage", controller.characterImage);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.nameBox", controller.nameBox);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.nextButton", controller.nextButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.dialogueUiRoot", controller.dialogueUiRoot);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choicePanel", controller.choicePanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceMashaButton", controller.choiceMashaButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceArtemButton", controller.choiceArtemButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceLeraButton", controller.choiceLeraButton);
        issues += ValidateChoiceButtonCapacity(sceneName, controller);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogPanel", controller.backlogPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogText", controller.backlogText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogCloseButton", controller.backlogCloseButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.notificationPanel", controller.notificationPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.notificationText", controller.notificationText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitPanel", controller.confirmExitPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitYesButton", controller.confirmExitYesButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitNoButton", controller.confirmExitNoButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsPanel", controller.vnSettingsPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnMusicVolumeSlider", controller.vnMusicVolumeSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSfxVolumeSlider", controller.vnSfxVolumeSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnTextSpeedSlider", controller.vnTextSpeedSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnAutoForwardToggle", controller.vnAutoForwardToggle);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnAutoForwardDelaySlider", controller.vnAutoForwardDelaySlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnFullscreenToggle", controller.vnFullscreenToggle);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsCloseButton", controller.vnSettingsCloseButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsResetButton", controller.vnSettingsResetButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.manualSaveLoadPanel", controller.manualSaveLoadPanel);

        ReplayEntryDefinition testReplay = AssetDatabase.LoadAssetAtPath<ReplayEntryDefinition>("Assets/HowIFall/Data/Replays/test_replay_v1.asset");
        if (testReplay != null && (controller.sceneData == testReplay.startScene
            || (testReplay.startScene != null && controller.sceneData == testReplay.startScene.defaultNextScene)))
        {
            issues += LogError($"{sceneName}: TECH DEMO replay scene must not be the normal VN start route.");
        }

        TimedNarrativeBeatController timedBeat = FindAny<TimedNarrativeBeatController>();
        issues += ValidateRequiredObject(sceneName, timedBeat, nameof(TimedNarrativeBeatController));
        if (timedBeat != null)
        {
            issues += ValidateTimedNarrativeBeatDemo(sceneName, controller, timedBeat);
        }

        issues += ValidateRequiredObject(sceneName, FindAny<SaveManager>(), nameof(SaveManager));

        VNQuickMenu quickMenu = FindAny<VNQuickMenu>();
        issues += ValidateRequiredObject(sceneName, quickMenu, nameof(VNQuickMenu));
        if (quickMenu != null)
        {
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.root", quickMenu.root);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.dialogueController", quickMenu.dialogueController);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.historyButton", quickMenu.historyButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.skipButton", quickMenu.skipButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.autoButton", quickMenu.autoButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.saveButton", quickMenu.saveButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.quickSaveButton", quickMenu.quickSaveButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.quickLoadButton", quickMenu.quickLoadButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.loadButton", quickMenu.loadButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.settingsButton", quickMenu.settingsButton);
            issues += ValidateRequiredReference(sceneName, "VNQuickMenu.mainMenuButton", quickMenu.mainMenuButton);
        }

        ManualSaveLoadPanel saveLoadPanel = FindAny<ManualSaveLoadPanel>();
        issues += ValidateRequiredObject(sceneName, saveLoadPanel, nameof(ManualSaveLoadPanel));
        if (saveLoadPanel != null && (saveLoadPanel.slotViews == null || saveLoadPanel.slotViews.Length != SaveManager.SlotCount))
        {
            issues += LogError($"{sceneName}: ManualSaveLoadPanel must contain exactly {SaveManager.SlotCount} slot views.");
        }

        return issues;
    }

    private static int ValidateTimedNarrativeBeatDemo(
        string sceneName,
        VNDialogueController dialogueController,
        TimedNarrativeBeatController timedBeat)
    {
        int issues = 0;
        TimedNarrativeBeatDefinition definition = timedBeat.demoDefinition;
        if (definition == null)
        {
            return LogError($"{sceneName}: TimedNarrativeBeatController.demoDefinition is missing.");
        }

        if (string.IsNullOrWhiteSpace(definition.promptText))
        {
            issues += LogError($"{sceneName}: timed beat prompt text is empty.");
        }

        if (string.IsNullOrWhiteSpace(definition.actionText))
        {
            issues += LogError($"{sceneName}: timed beat action text is empty.");
        }

        if (definition.durationSeconds < TimedNarrativeBeatController.MinimumAuthoredDurationSeconds)
        {
            issues += LogError($"{sceneName}: timed beat duration must be at least {TimedNarrativeBeatController.MinimumAuthoredDurationSeconds:0} seconds.");
        }

        issues += ValidateRequiredReference(sceneName, "TimedNarrativeBeatController.rootPanel", timedBeat.rootPanel);
        issues += ValidateRequiredReference(sceneName, "TimedNarrativeBeatController.promptText", timedBeat.promptText);
        issues += ValidateRequiredReference(sceneName, "TimedNarrativeBeatController.actionButton", timedBeat.actionButton);
        issues += ValidateRequiredReference(sceneName, "TimedNarrativeBeatController.remainingTimeText", timedBeat.remainingTimeText);
        issues += ValidateRequiredReference(sceneName, "TimedNarrativeBeatController.progressSlider", timedBeat.progressSlider);

        if (!dialogueController.IsRegisteredDialogueScene(definition.successNextScene))
        {
            issues += LogError($"{sceneName}: timed beat success target must be a registered valid dialogue scene.");
        }

        if (!dialogueController.IsRegisteredDialogueScene(definition.timeoutNextScene))
        {
            issues += LogError($"{sceneName}: timed beat timeout target must be a registered valid dialogue scene.");
        }

        return issues;
    }

    private static int ValidateGalleryReplayDefinitions(string sceneName, MainMenuController controller)
    {
        if (controller.ReplayEntries == null || controller.ReplayEntries.Count != 1)
        {
            return LogError($"{sceneName}: Gallery must contain exactly one TEST replay definition.");
        }

        ReplayEntryDefinition definition = controller.ReplayEntries[0];
        int issues = 0;
        if (!SceneFlowManager.TryValidateReplayDefinition(definition, controller.ReplayEntries, controller.dialogueRegistry, out string error))
        {
            return LogError($"{sceneName}: Gallery replay definition is invalid. {error}");
        }

        if (definition.replayId != "test_replay_v1" || definition.displayName != "TEST REPLAY")
        {
            issues += LogError($"{sceneName}: TEST replay must use replayId 'test_replay_v1' and displayName 'TEST REPLAY'.");
        }

        DialogueSceneData start = definition.startScene;
        if (start.sceneId != "replay_demo_start" || start.displayName != "TECH DEMO ONLY - NOT CANON"
            || start.lines == null || start.lines.Count != 1 || start.lines[0] == null
            || start.lines[0].text != "TEST REPLAY START")
        {
            issues += LogError($"{sceneName}: replay_demo_start is not the approved TECH DEMO fixture.");
        }

        DialogueSceneData end = start.defaultNextScene;
        if (end == null || end.sceneId != "replay_demo_end" || end.displayName != "TECH DEMO ONLY - NOT CANON"
            || end.lines == null || end.lines.Count != 1 || end.lines[0] == null
            || end.lines[0].text != "TEST REPLAY END" || end.defaultNextScene != null)
        {
            issues += LogError($"{sceneName}: replay_demo_end is not the approved terminal TECH DEMO fixture.");
        }

        return issues;
    }
    private static int ValidateChoiceButtonCapacity(string sceneName, VNDialogueController controller)
    {
        var assignedButtons = new HashSet<Button>();
        if (controller.choiceMashaButton != null)
        {
            assignedButtons.Add(controller.choiceMashaButton);
        }

        if (controller.choiceArtemButton != null)
        {
            assignedButtons.Add(controller.choiceArtemButton);
        }

        if (controller.choiceLeraButton != null)
        {
            assignedButtons.Add(controller.choiceLeraButton);
        }

        if (assignedButtons.Count == VNDialogueController.SupportedChoiceButtonCapacity)
        {
            return 0;
        }

        return LogError(
            $"{sceneName}: VNDialogueController must have {VNDialogueController.SupportedChoiceButtonCapacity} distinct assigned choice buttons; found {assignedButtons.Count}.");
    }

    private static int ValidateCommonSceneRequirements(string sceneName)
    {
        int issues = 0;

        Camera mainCamera = Camera.main;
        issues += ValidateRequiredObject(sceneName, mainCamera, "Main Camera");

        int audioListenerCount = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
        if (audioListenerCount != 1)
        {
            issues += LogError($"{sceneName}: AudioListener count is {audioListenerCount}.");
        }

        issues += ValidateRequiredObject(sceneName, FindAny<EventSystem>(), nameof(EventSystem));
        issues += ValidateRequiredObject(sceneName, FindAny<Canvas>(), nameof(Canvas));

        GameObject managers = GameObject.Find("Managers");
        if (managers == null)
        {
            issues += LogWarning($"{sceneName}: GameObject Managers was not found.");
        }

        return issues;
    }

    private static int ValidateRequiredObject<T>(string sceneName, T value, string objectName)
        where T : Object
    {
        if (value != null)
        {
            return 0;
        }

        return LogError($"{sceneName}: {objectName} was not found.");
    }

    private static T FindAny<T>()
        where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static int ValidateRequiredReference(string sceneName, string referenceName, Object value)
    {
        if (value != null)
        {
            return 0;
        }

        return LogError($"{sceneName}: {referenceName} is not assigned.");
    }

    private static int FindBuildSettingsSceneIndex(string path)
    {
        IList<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].enabled && scenes[i].path == path)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LogError(string message)
    {
        Debug.LogError(message);
        return 1;
    }

    private static int LogWarning(string message)
    {
        Debug.LogWarning(message);
        return 1;
    }
}
