using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CharacterHubSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Character Hub Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("Character Hub smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        CharacterHubTechnicalConfig config = Resources.Load<CharacterHubTechnicalConfig>(CharacterHubTechnicalConfig.ResourcesPath);
        Require(config != null, "Technical config must be present at its fixed Resources path.");
        Require(CharacterHubController.TryCreateFixtures(config, out CharacterHubFixture[] fixtures, out _), "Technical config fixture references must be valid.");
        string visibleProfileId = config.visibleProfile.characterId;
        string visibleProfileBio = config.visibleProfile.biography;
        string lockedProfileId = config.lockedProfile.characterId;
        string lockedProfileBio = config.lockedProfile.biography;
        Require(fixtures.Length == 2 && fixtures[0].definition.characterId == "test_character_a" && !fixtures[0].locked, "TEST CHARACTER A must be visible.");
        Require(fixtures[1].definition.characterId == "test_character_b" && fixtures[1].locked, "TEST CHARACTER B must be locked.");

        var catalogue = new List<CharacterHubFixture>();
        Require(CharacterHubController.TryBuildValidFixtures(fixtures, catalogue, out _), "Fixture IDs must be valid and unique.");
        Require(!CharacterHubController.TryBuildValidFixtures(new[] { fixtures[0], fixtures[0] }, catalogue, out _), "Duplicate profile IDs must be rejected.");

        EditorSceneManager.OpenScene("Assets/HowIFall/Scenes/VNPrototype.unity");
        VNQuickMenu quickMenu = UnityEngine.Object.FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include);
        Require(quickMenu != null, "VNPrototype must provide the existing Quick Menu for runtime augmentation.");
        Require(quickMenu.EnsureCharactersButton(), "Runtime initialization must add the dedicated Character Hub launcher once.");
        Button runtimeCharactersButton = quickMenu.charactersButton;
        Require(runtimeCharactersButton != null
            && runtimeCharactersButton.name == "Character Hub Launcher"
            && !runtimeCharactersButton.transform.IsChildOf(quickMenu.root.transform),
            "Characters access must be a dedicated original HIF launcher outside the Quick Menu strip.");
        Require(!quickMenu.EnsureCharactersButton(), "Repeated launcher initialization must not add duplicates.");
        UnityEngine.Object.DestroyImmediate(runtimeCharactersButton.gameObject);
        quickMenu.charactersButton = null;

        GameObject canvasObject = new GameObject("CharacterHubSmokeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject eventSystemObject = new GameObject("CharacterHubSmokeEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        GameObject stateObject = new GameObject("CharacterHubSmokeState");
        GameObject controllerObject = new GameObject("CharacterHubSmokeController");
        try
        {
            GameState state = GameState.EnsureInstance();
            state.trustMasha = 4;
            state.trustArtem = 5;
            state.leraInterest = 6;
            VNDialogueController dialogue = controllerObject.AddComponent<VNDialogueController>();
            dialogue.dialogueUiRoot = new GameObject("CharacterHubSmokeDialogueRoot");
            dialogue.dialogueUiRoot.transform.SetParent(controllerObject.transform, false);
            CharacterHubController hub = CharacterHubController.TryCreateRuntime(dialogue);
            Require(hub != null && hub.dialogueController == dialogue, "Runtime bootstrap must create one Character Hub.");
            dialogue.characterHubController = hub;
            GameObject quickMenuObject = new GameObject("CharacterHubSmokeQuickMenu");
            GameObject quickMenuRoot = new GameObject("CharacterHubSmokeQuickMenuRoot");
            VNQuickMenu runtimeQuickMenu = quickMenuObject.AddComponent<VNQuickMenu>();
            runtimeQuickMenu.dialogueController = dialogue;
            runtimeQuickMenu.root = quickMenuRoot;
            Require(CharacterHubController.TryCreateRuntime(dialogue) == hub, "Repeated initialization must reuse the existing hub.");
            Require(UnityEngine.Object.FindObjectsByType<CharacterHubController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Runtime bootstrap must create exactly one hub.");
            Require(dialogue.OpenCharacterHub(), "Hub must open with the technical config.");
            Require(dialogue.IsDialogueShellSuppressed && !dialogue.dialogueUiRoot.activeSelf,
                "Character Hub left the ordinary dialogue shell visible beneath its presentation.");
            Require(!dialogue.CanAdvanceDialogue && !dialogue.CanSave && !dialogue.CanLoad, "Open Hub must block dialogue advance and save/load eligibility.");
            Require(hub.centralPanel != null && hub.centralPanel.transform.parent == hub.panel.transform, "Runtime Hub must use a central content panel above the dim overlay.");
            Require(hub.portraitPlaceholder != null && hub.portraitPlaceholder.transform.parent == hub.centralPanel.transform, "Runtime Hub must use its own portrait placeholder frame.");
            Require(hub.selectedNameText.text == "TEST CHARACTER A" && hub.biographyText.text == "TEST BIO A", "TEST CHARACTER A must display its technical bio.");
            Require(hub.relationshipText.text == "Relationship: 4", "Visible relationship must use current GameState.");
            state.trustMasha = 9;
            hub.Refresh();
            Require(hub.relationshipText.text == "Relationship: 9", "Relationship must refresh after GameState changes.");
            Require(hub.SelectFixture(1), "Locked TEST CHARACTER B must remain selectable for locked presentation.");
            Require(hub.selectedNameText.text == "LOCKED" && hub.biographyText.text == "Biography: LOCKED" && hub.relationshipText.text == "Relationship: LOCKED", "Locked profile must hide biography and relationship.");
            dialogue.CloseCharacterHub();
            Require(!hub.IsOpen && dialogue.CanAdvanceDialogue, "Hub close must restore normal eligibility.");
            Require(!dialogue.IsDialogueShellSuppressed && dialogue.dialogueUiRoot.activeSelf,
                "Character Hub close did not restore the ordinary dialogue shell.");
            VerifyQuickMenuVisibilityOwnership(dialogue, hub, runtimeQuickMenu, quickMenuRoot);
            Require(state.trustMasha == 9 && state.trustArtem == 5 && state.leraInterest == 6, "Hub must not mutate GameState.");
            Require(hub.panel != null && hub.closeButton != null && hub.selectedNameText != null && hub.biographyText != null && hub.relationshipText != null, "Runtime Hub UI references are required.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(GameObject.Find("CharacterHubSmokeQuickMenuRoot"));
            UnityEngine.Object.DestroyImmediate(GameObject.Find("CharacterHubSmokeQuickMenu"));
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(stateObject);
            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }

        Require(SaveData.CurrentVersion == 3, "Character Hub must preserve SaveData v3.");
        Require(config.visibleProfile.characterId == visibleProfileId && config.visibleProfile.biography == visibleProfileBio
            && config.lockedProfile.characterId == lockedProfileId && config.lockedProfile.biography == lockedProfileBio,
            "Character Hub must not mutate technical profile assets.");
        Require(!typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(f => f.Name.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("hub", StringComparison.OrdinalIgnoreCase) >= 0), "SaveData must not receive Character Hub fields.");
    }

    private static void VerifyQuickMenuVisibilityOwnership(
        VNDialogueController dialogue,
        CharacterHubController hub,
        VNQuickMenu quickMenu,
        GameObject quickMenuRoot)
    {
        Require(quickMenuRoot.activeSelf, "Quick Menu must start visible for Character Hub visibility regression.");

        Require(dialogue.OpenCharacterHub(), "Quick Menu close-path fixture must open Character Hub.");
        quickMenu.RefreshSpecialModeVisibility();
        Require(quickMenuRoot.activeSelf, "Ordinary Character Hub must not hide Quick Menu through special-mode ownership.");
        dialogue.CloseCharacterHub();
        quickMenu.RefreshSpecialModeVisibility();
        Require(quickMenuRoot.activeSelf, "Close path must retain the Quick Menu visible state.");

        Require(dialogue.OpenCharacterHub(), "Escape-path fixture must reopen Character Hub.");
        dialogue.CloseCharacterHub(); // VNDialogueController routes Escape through this exact close method.
        quickMenu.RefreshSpecialModeVisibility();
        Require(quickMenuRoot.activeSelf, "Escape close path must retain the Quick Menu visible state.");

        for (int index = 0; index < 2; index++)
        {
            Require(dialogue.OpenCharacterHub(), "Repeated Character Hub cycle must open.");
            dialogue.CloseCharacterHub();
            quickMenu.RefreshSpecialModeVisibility();
            Require(quickMenuRoot.activeSelf, "Repeated Character Hub close must retain Quick Menu visibility.");
            Require(UnityEngine.Object.FindObjectsByType<CharacterHubController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Repeated Character Hub cycles must not create duplicates.");
        }

        quickMenu.SetPlayerInterfaceHidden(true);
        Require(!quickMenuRoot.activeSelf, "Hide UI owner must hide Quick Menu.");
        dialogue.CloseCharacterHub();
        quickMenu.RefreshSpecialModeVisibility();
        Require(!quickMenuRoot.activeSelf, "Character Hub cleanup must not force-show a Hide UI-owned Quick Menu.");
        quickMenu.SetPlayerInterfaceHidden(false);
        Require(quickMenuRoot.activeSelf, "Hide UI owner must restore its previous Quick Menu state.");

        GameObject specialOwner = new GameObject("CharacterHubSmokeSpecialOwner");
        try
        {
            Require(dialogue.TryEnterSpecialMode(specialOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "Special-mode visibility fixture must enter BlockingExclusive.");
            quickMenu.RefreshSpecialModeVisibility();
            Require(!quickMenuRoot.activeSelf, "BlockingExclusive must retain its Quick Menu visibility ownership.");
            dialogue.CloseCharacterHub();
            quickMenu.RefreshSpecialModeVisibility();
            Require(!quickMenuRoot.activeSelf, "Character Hub cleanup must not force-show a special-mode-owned Quick Menu.");
            Require(dialogue.ExitSpecialMode(lease), "Special-mode visibility fixture must exit.");
            quickMenu.RefreshSpecialModeVisibility();
            Require(quickMenuRoot.activeSelf, "Quick Menu must restore after the special owner exits.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(specialOwner);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
