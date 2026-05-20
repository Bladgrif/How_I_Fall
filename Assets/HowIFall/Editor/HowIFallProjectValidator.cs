using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HowIFallProjectValidator
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Validate Project")]
    public static void ValidateProject()
    {
        int issueCount = 0;

        issueCount += ValidateBuildSettings();
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
        }

        issues += ValidateRequiredObject(sceneName, FindAny<SettingsPanelController>(), nameof(SettingsPanelController));
        issues += ValidateRequiredObject(sceneName, FindAny<SceneFlowManager>(), nameof(SceneFlowManager));
        issues += ValidateRequiredObject(sceneName, FindAny<SaveManager>(), nameof(SaveManager));
        issues += ValidateRequiredObject(sceneName, FindAny<GameState>(), nameof(GameState));
        issues += ValidateRequiredObject(sceneName, FindAny<SettingsManager>(), nameof(SettingsManager));
        issues += ValidateRequiredObject(sceneName, FindAny<AudioManager>(), nameof(AudioManager));

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
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choicePanel", controller.choicePanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceMashaButton", controller.choiceMashaButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceArtemButton", controller.choiceArtemButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.choiceLeraButton", controller.choiceLeraButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogPanel", controller.backlogPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogText", controller.backlogText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.backlogCloseButton", controller.backlogCloseButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.notificationPanel", controller.notificationPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.notificationText", controller.notificationText);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitPanel", controller.confirmExitPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitYesButton", controller.confirmExitYesButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.confirmExitNoButton", controller.confirmExitNoButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsPanel", controller.vnSettingsPanel);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnMasterVolumeSlider", controller.vnMasterVolumeSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnMusicVolumeSlider", controller.vnMusicVolumeSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSfxVolumeSlider", controller.vnSfxVolumeSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnTextSpeedSlider", controller.vnTextSpeedSlider);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnFullscreenToggle", controller.vnFullscreenToggle);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsCloseButton", controller.vnSettingsCloseButton);
        issues += ValidateRequiredReference(sceneName, "VNDialogueController.vnSettingsResetButton", controller.vnSettingsResetButton);

        return issues;
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
