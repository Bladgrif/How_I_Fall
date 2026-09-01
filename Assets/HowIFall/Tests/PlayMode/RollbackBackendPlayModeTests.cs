using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class RollbackBackendPlayModeTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
    private readonly List<string> playerPrefsKeys = new List<string>();
    private string saveDirectory;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        VNDialogueController.RollbackRestoreFailureInjectionForTests = null;
        DestroyExistingSingletons();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        VNDialogueController.RollbackRestoreFailureInjectionForTests = null;
        foreach (string key in playerPrefsKeys)
        {
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();

        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object item = createdObjects[index];
            if (item != null)
            {
                UnityEngine.Object.Destroy(item);
            }
        }
        createdObjects.Clear();
        DestroyExistingSingletons();
        yield return null;

        if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
        {
            Directory.Delete(saveDirectory, true);
        }
        saveDirectory = null;
    }

    [UnityTest]
    public IEnumerator StableLineRollback_RestoresAAndRemovesFutureBacklog()
    {
        TestContext context = CreateContext(CreateLinearScene("linear", "A", "B"));
        yield return null;

        CompleteCurrentLine(context.Controller); // A stable
        AdvanceAndComplete(context.Controller);  // B stable
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(2));

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);

        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));
        Assert.That(context.Controller.dialogueText.text, Is.EqualTo("A"));
        AssertBacklog(context.Controller, "A");
        Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.False);
    }

    [UnityTest]
    public IEnumerator PartialFutureRollback_CancelsTypingWithoutCompletingFuture()
    {
        TestContext context = CreateContext(CreateLinearScene("partial", "A", "Future B"));
        yield return null;

        CompleteCurrentLine(context.Controller);
        context.Controller.AdvanceDialogue();
        Assert.That(GetPrivate<bool>(context.Controller, "isTyping"), Is.True);
        Assert.That(context.ReadHistory.IsSeen("partial", "line-1"), Is.False);

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);

        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));
        Assert.That(context.Controller.dialogueText.text, Is.EqualTo("A"));
        Assert.That(context.ReadHistory.IsSeen("partial", "line-1"), Is.False);
        AssertBacklog(context.Controller, "A");
    }

    [UnityTest]
    public IEnumerator RepeatedRollback_IsOrdered_AndSeenHistoryRemainsMonotonic()
    {
        TestContext context = CreateContext(CreateLinearScene("repeat", "A", "B", "C"));
        yield return null;

        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        AdvanceAndComplete(context.Controller);
        Assert.That(context.ReadHistory.IsSeen("repeat", "line-2"), Is.True);

        Assert.That(context.Controller.TryRollback(out string firstError), Is.True, firstError);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"));
        AssertBacklog(context.Controller, "A", "B");

        Assert.That(context.Controller.TryRollback(out string secondError), Is.True, secondError);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));
        AssertBacklog(context.Controller, "A");
        Assert.That(context.Controller.CanRollback, Is.False);
        Assert.That(context.ReadHistory.IsSeen("repeat", "line-2"), Is.True);
    }

    [UnityTest]
    public IEnumerator ChoiceRollback_RestoresAllState_ThenAppliesAlternativeExactlyOnce()
    {
        DialogueSceneData targetA = CreateLinearScene("target-a", "Target A");
        DialogueSceneData targetB = CreateLinearScene("target-b", "Target B");
        DialogueSceneData choiceScene = CreateLinearScene("choice", "Choose");
        choiceScene.choices = new List<DialogueChoice>
        {
            CreateChoice("A", "Result A", targetA, 10, 20, 30, 40, 50, 60, 70, 80, 90),
            CreateChoice("B", "Result B", targetB, -1, -2, -3, -4, -5, -6, -7, -8, -9)
        };
        TestContext context = CreateContext(choiceScene, targetA, targetB);
        SetAllNumericState(context.GameState, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        yield return null;

        CompleteCurrentLine(context.Controller);
        context.Controller.AdvanceDialogue();
        Assert.That(context.Controller.choicePanel.activeSelf, Is.True);
        InvokePrivate(context.Controller, "Choose", 0);
        Assert.That(context.Controller.IsRelationshipCueVisible, Is.True);
        AssertAllNumericState(context.GameState, 11, 22, 33, 44, 55, 66, 77, 88, 99);

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);

        AssertAllNumericState(context.GameState, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        Assert.That(context.GameState.selectedChoiceIndex, Is.EqualTo(-1));
        Assert.That(context.GameState.choiceResultActive, Is.False);
        Assert.That(context.GameState.pendingNextSceneId, Is.Empty);
        Assert.That(context.Controller.choicePanel.activeSelf, Is.True);
        Assert.That(context.Controller.IsRelationshipCueVisible, Is.False);
        AssertBacklog(context.Controller, "Choose");

        InvokePrivate(context.Controller, "Choose", 1);
        AssertAllNumericState(context.GameState, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.That(context.GameState.selectedChoiceIndex, Is.EqualTo(1));
        Assert.That(context.GameState.choiceResultActive, Is.True);
        Assert.That(context.GameState.pendingNextSceneId, Is.EqualTo("target-b"));
        AssertBacklog(context.Controller, "Choose", "Result B");
    }

    [UnityTest]
    public IEnumerator GameMenuRollbackRoute_ClosesShellAndRestoresReadingAndChoiceFocus()
    {
        GameObject eventSystemObject = new GameObject("Rollback Route EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        createdObjects.Add(eventSystemObject);

        TestContext linear = CreateContext(CreateLinearScene("menu-linear", "A", "B"));
        yield return null;
        CompleteCurrentLine(linear.Controller);
        AdvanceAndComplete(linear.Controller);
        Assert.That(linear.Controller.OpenGameMenu(), Is.True);
        VNGameMenuView linearView = linear.Controller.GameMenuController.View;
        Assert.That(linearView.IsActionVisible(VNGameMenuAction.Rollback), Is.True);
        Assert.That(linearView.GetButton(VNGameMenuAction.Rollback).interactable, Is.True);
        linearView.GetButton(VNGameMenuAction.Rollback).onClick.Invoke();
        Assert.That(linear.Controller.IsGameMenuOpen, Is.False);
        Assert.That(linear.Controller.dialogueUiRoot.activeInHierarchy, Is.True);
        Assert.That(linear.GameState.currentLineId, Is.EqualTo("line-0"));
        AssertBacklog(linear.Controller, "A");

        DestroyExistingSingletons();
        yield return null;

        DialogueSceneData target = CreateLinearScene("menu-choice-target", "Target");
        DialogueSceneData choiceScene = CreateLinearScene("menu-choice", "Choose");
        choiceScene.choices = new List<DialogueChoice> { CreateChoice("A", "Result A", target, 0, 0, 0, 0, 0, 0, 1, 0, 0) };
        TestContext choice = CreateContext(choiceScene, target);
        yield return null;
        CompleteCurrentLine(choice.Controller);
        choice.Controller.AdvanceDialogue();
        choice.Controller.choiceMashaButton.onClick.Invoke();
        Assert.That(choice.GameState.trustMasha, Is.EqualTo(1));
        Assert.That(choice.Controller.OpenGameMenu(), Is.True);
        VNGameMenuView choiceView = choice.Controller.GameMenuController.View;
        Assert.That(choiceView.GetButton(VNGameMenuAction.Rollback).interactable, Is.True);
        choiceView.GetButton(VNGameMenuAction.Rollback).onClick.Invoke();
        Assert.That(choice.Controller.IsGameMenuOpen, Is.False);
        Assert.That(choice.Controller.choicePanel.activeInHierarchy, Is.True);
        Assert.That(choice.GameState.trustMasha, Is.Zero);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(choice.Controller.choiceMashaButton.gameObject));
        AssertBacklog(choice.Controller, "Choose");
    }

    [UnityTest]
    public IEnumerator GameMenuSaveLoadReturn_PreservesRollbackSessionAvailability()
    {
        TestContext unavailable = CreateContext(CreateLinearScene("menu-unavailable", "A"));
        yield return null;
        CompleteCurrentLine(unavailable.Controller);
        Assert.That(unavailable.Controller.OpenGameMenu(), Is.True);
        VNGameMenuView unavailableView = unavailable.Controller.GameMenuController.View;
        Button unavailableRollback = unavailableView.GetButton(VNGameMenuAction.Rollback);
        Assert.That(unavailableRollback.interactable, Is.False);
        string unavailableLine = unavailable.GameState.currentLineId;
        SimulateSaveLoadReturn(unavailable.Controller, unavailableView);
        Assert.That(unavailableRollback.interactable, Is.False, "Save/Load return must retain disabled session Rollback.");
        unavailableRollback.onClick.Invoke();
        Assert.That(unavailable.Controller.IsGameMenuOpen, Is.True, "Disabled Rollback must not close the Game Menu.");
        Assert.That(unavailable.GameState.currentLineId, Is.EqualTo(unavailableLine), "Disabled Rollback must not mutate dialogue state.");

        unavailable.Controller.GameMenuController.Close();
        DestroyExistingSingletons();
        yield return null;

        TestContext available = CreateContext(CreateLinearScene("menu-available", "A", "B"));
        yield return null;
        CompleteCurrentLine(available.Controller);
        AdvanceAndComplete(available.Controller);
        Assert.That(available.Controller.OpenGameMenu(), Is.True);
        VNGameMenuView availableView = available.Controller.GameMenuController.View;
        Button availableRollback = availableView.GetButton(VNGameMenuAction.Rollback);
        Assert.That(availableRollback.interactable, Is.True);
        SimulateSaveLoadReturn(available.Controller, availableView);
        Assert.That(availableRollback.interactable, Is.True, "Save/Load return must retain enabled session Rollback.");
        availableRollback.onClick.Invoke();
        Assert.That(available.Controller.IsGameMenuOpen, Is.False);
        Assert.That(available.GameState.currentLineId, Is.EqualTo("line-0"));
    }

    [UnityTest]
    public IEnumerator RegisteredSceneTransition_RestoresExactPresentationAndMusic()
    {
        Sprite backgroundA = CreateSprite(Color.red);
        Sprite backgroundB = CreateSprite(Color.blue);
        Sprite characterA = CreateSprite(Color.green);
        Sprite characterB = CreateSprite(Color.yellow);
        AudioClip musicA = CreateAudioClip("music-a");

        DialogueSceneData sceneB = CreateLinearScene("presentation-b", "B");
        sceneB.stopMusicOnStart = true;
        sceneB.lines[0].background = backgroundB;
        sceneB.lines[0].characterSprite = characterB;
        sceneB.lines[0].characterPosition = CharacterPosition.Right;

        DialogueSceneData sceneA = CreateLinearScene("presentation-a", "A");
        sceneA.backgroundMusic = musicA;
        sceneA.defaultNextScene = sceneB;
        sceneA.lines[0].background = backgroundA;
        sceneA.lines[0].characterSprite = characterA;
        sceneA.lines[0].characterPosition = CharacterPosition.Left;

        TestContext context = CreateContext(sceneA, sceneB);
        yield return null;

        CompleteCurrentLine(context.Controller);
        Color checkpointBackgroundColor = new Color(0.7f, 0.8f, 0.9f, 0.6f);
        context.Controller.backgroundImage.color = checkpointBackgroundColor;
        Vector2 checkpointPosition = new Vector2(123f, 456f);
        Vector2 checkpointSize = new Vector2(321f, 654f);
        context.Controller.characterImage.rectTransform.anchoredPosition = checkpointPosition;
        context.Controller.characterImage.rectTransform.sizeDelta = checkpointSize;
        // Refresh the same stable position so the exact runtime presentation is the stored state.
        InvokePrivate(context.Controller, "CaptureStableCheckpoint", RollbackCheckpointKind.StableLine);

        context.Controller.AdvanceDialogue();
        Assert.That(context.GameState.currentSceneId, Is.EqualTo("presentation-b"));
        Assert.That(context.Controller.backgroundImage.sprite, Is.EqualTo(backgroundB));
        Assert.That(context.Controller.characterImage.sprite, Is.EqualTo(characterB));
        Assert.That(context.Audio.musicSource.isPlaying, Is.False);

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);

        Assert.That(context.GameState.currentSceneId, Is.EqualTo("presentation-a"));
        Assert.That(context.Controller.backgroundImage.sprite, Is.EqualTo(backgroundA));
        Assert.That(context.Controller.backgroundImage.enabled, Is.True);
        Assert.That(context.Controller.backgroundImage.color, Is.EqualTo(checkpointBackgroundColor));
        Assert.That(context.Controller.characterImage.sprite, Is.EqualTo(characterA));
        Assert.That(context.Controller.characterImage.enabled, Is.True);
        Assert.That(context.Controller.characterImage.rectTransform.anchoredPosition, Is.EqualTo(checkpointPosition));
        Assert.That(context.Controller.characterImage.rectTransform.sizeDelta, Is.EqualTo(checkpointSize));
        Assert.That(context.Audio.musicSource.clip, Is.EqualTo(musicA));
        Assert.That(context.Audio.musicSource.isPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator CarryOverPresentation_IsRemovedWhenTargetLineHasNoDirectives()
    {
        Sprite carriedBackground = CreateSprite(Color.magenta);
        Sprite carriedCharacter = CreateSprite(Color.cyan);
        Sprite futureBackground = CreateSprite(Color.black);
        Sprite futureCharacter = CreateSprite(Color.white);
        DialogueSceneData scene = CreateLinearScene("carry", "A", "B");
        scene.lines[1].background = futureBackground;
        scene.lines[1].characterSprite = futureCharacter;
        TestContext context = CreateContext(scene);
        context.Controller.backgroundImage.sprite = carriedBackground;
        context.Controller.backgroundImage.enabled = true;
        context.Controller.characterImage.sprite = carriedCharacter;
        context.Controller.characterImage.enabled = true;
        yield return null;

        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        Assert.That(context.Controller.backgroundImage.sprite, Is.EqualTo(futureBackground));
        Assert.That(context.Controller.characterImage.sprite, Is.EqualTo(futureCharacter));

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);
        Assert.That(context.Controller.backgroundImage.sprite, Is.EqualTo(carriedBackground));
        Assert.That(context.Controller.characterImage.sprite, Is.EqualTo(carriedCharacter));
    }

    [UnityTest]
    public IEnumerator Rollback_PausesAutoAndSkip_WithoutMutatingPersistentAutoPreference()
    {
        TestContext context = CreateContext(CreateLinearScene("automation", "A", "B"));
        context.Settings.settings.autoForward = true;
        context.Settings.settings.autoForwardDelay = 50f;
        yield return null;

        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        context.Controller.SetSkip(true);
        Assert.That(context.Controller.IsSkipEnabled, Is.True);

        Assert.That(context.Controller.TryRollback(out string error), Is.True, error);
        Assert.That(context.Settings.settings.autoForward, Is.True, "Rollback must not rewrite the persisted preference value.");
        Assert.That(context.Controller.IsAutoForwardEnabledState, Is.False);
        Assert.That(context.Controller.IsSkipEnabled, Is.False);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"));

        yield return new WaitForSecondsRealtime(0.75f);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-0"), "Paused Auto/Skip must not advance after rollback.");
    }

    [UnityTest]
    public IEnumerator SpecialModeAndCharacterHub_BarriersClearOrPreserveAsContracted()
    {
        TestContext context = CreateContext(CreateLinearScene("barriers", "A", "B"));
        yield return null;
        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(2));

        Assert.That(context.Controller.TryEnterSpecialMode(null, SpecialModePolicy.BlockingExclusive, out _), Is.False);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(2));

        CharacterHubController hub = context.Controller.characterHubController;
        if (hub == null)
        {
            hub = context.Controller.gameObject.AddComponent<CharacterHubController>();
            hub.panel = CreateGameObject("Character Hub Test Panel");
            context.Controller.characterHubController = hub;
        }
        hub.panel.SetActive(true);
        Assert.That(context.Controller.CanRollback, Is.False);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(2));
        hub.panel.SetActive(false);
        Assert.That(context.Controller.CanRollback, Is.True);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(2));

        GameObject owner = CreateGameObject("Special Mode Owner");
        Assert.That(context.Controller.TryEnterSpecialMode(owner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), Is.True);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.Zero);
        Assert.That(context.Controller.ExitSpecialMode(lease), Is.True);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator SavePreservesBuffer_AndAcceptedLoadClearsIt()
    {
        Scene testScene = SceneManager.CreateScene("VNPrototype");
        SceneManager.SetActiveScene(testScene);
        DialogueSceneData scene = CreateLinearScene("save-load", "A", "B", "C");
        TestContext context = CreateContext(scene);
        saveDirectory = Path.Combine(Application.temporaryCachePath, "hif-rollback-" + Guid.NewGuid().ToString("N"));
        yield return null;

        SaveManager manager = SaveManager.Instance;
        manager.ConfigureSaveDirectoryForTests(saveDirectory);
        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        int beforeSaveCount = context.Controller.RollbackCheckpointCount;
        Texture2D preview = new Texture2D(2, 2);
        createdObjects.Add(preview);

        Assert.That(manager.SaveSlot(SaveSlotType.Manual, 1, preview), Is.True);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.EqualTo(beforeSaveCount));

        AdvanceAndComplete(context.Controller);
        Assert.That(context.Controller.RollbackCheckpointCount, Is.GreaterThan(beforeSaveCount));
        Assert.That(manager.LoadSlot(SaveSlotType.Manual, 1), Is.True);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"));
        Assert.That(context.Controller.RollbackCheckpointCount, Is.Zero);
        Assert.That(SaveData.CurrentVersion, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator RestoreFailureAfterMutation_RestoresFallbackAtomicallyAndFailsClosed()
    {
        Sprite backgroundA = CreateSprite(Color.red);
        Sprite backgroundB = CreateSprite(Color.blue);
        DialogueSceneData scene = CreateLinearScene("atomic", "A", "B");
        scene.lines[0].background = backgroundA;
        scene.lines[1].background = backgroundB;
        TestContext context = CreateContext(scene);
        yield return null;

        SetAllNumericState(context.GameState, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        CompleteCurrentLine(context.Controller);
        AdvanceAndComplete(context.Controller);
        SetAllNumericState(context.GameState, 9, 8, 7, 6, 5, 4, 3, 2, 1);
        List<DialogueBacklogEntry> fallbackBacklog = context.Controller.CaptureBacklogSnapshot();

        VNDialogueController.RollbackRestoreFailureInjectionForTests = () =>
        {
            VNDialogueController.RollbackRestoreFailureInjectionForTests = null;
            throw new InvalidOperationException("Injected post-mutation restore failure.");
        };

        Assert.That(context.Controller.TryRollback(out string error), Is.False);
        Assert.That(error, Does.Contain("Injected post-mutation restore failure"));
        AssertAllNumericState(context.GameState, 9, 8, 7, 6, 5, 4, 3, 2, 1);
        Assert.That(context.GameState.currentLineId, Is.EqualTo("line-1"));
        Assert.That(context.Controller.backgroundImage.sprite, Is.EqualTo(backgroundB));
        List<DialogueBacklogEntry> restoredBacklog = context.Controller.CaptureBacklogSnapshot();
        Assert.That(restoredBacklog.Count, Is.EqualTo(fallbackBacklog.Count));
        for (int index = 0; index < fallbackBacklog.Count; index++)
        {
            Assert.That(restoredBacklog[index].speaker, Is.EqualTo(fallbackBacklog[index].speaker));
            Assert.That(restoredBacklog[index].text, Is.EqualTo(fallbackBacklog[index].text));
        }
        Assert.That(context.Controller.RollbackCheckpointCount, Is.Zero);
        Assert.That(context.Controller.CanRollback, Is.False);
    }

    private TestContext CreateContext(DialogueSceneData startScene, params DialogueSceneData[] additionalScenes)
    {
        SettingsManager settings = new GameObject("Rollback SettingsManager").AddComponent<SettingsManager>();
        createdObjects.Add(settings.gameObject);
        settings.settings.autoSave = false;
        settings.settings.autoForward = false;
        settings.settings.textSpeed = 20f;

        AudioManager audio = new GameObject("Rollback AudioManager").AddComponent<AudioManager>();
        createdObjects.Add(audio.gameObject);

        GameState gameState = new GameObject("Rollback GameState").AddComponent<GameState>();
        createdObjects.Add(gameState.gameObject);

        GameObject canvasObject = new GameObject("Rollback Canvas", typeof(Canvas));
        createdObjects.Add(canvasObject);
        GameObject controllerObject = new GameObject("Rollback VNDialogueController");
        controllerObject.transform.SetParent(canvasObject.transform, false);
        VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();

        controller.speakerText = CreateTmp("Speaker", controllerObject.transform);
        controller.dialogueText = CreateTmp("Dialogue", controllerObject.transform);
        controller.backgroundImage = CreateImage("Background", controllerObject.transform);
        controller.characterImage = CreateImage("Character", controllerObject.transform);
        controller.nameBox = CreateGameObject("Name Box", controllerObject.transform);
        controller.nextButton = CreateButton("Next", controllerObject.transform);
        controller.dialogueUiRoot = CreateGameObject("Dialogue UI Root", controllerObject.transform);
        controller.choicePanel = CreateGameObject("Choice Panel", controllerObject.transform);
        controller.choiceDimOverlay = CreateGameObject("Choice Overlay", controllerObject.transform);
        controller.choiceMashaButton = CreateButton("Choice 1", controller.choicePanel.transform);
        controller.choiceArtemButton = CreateButton("Choice 2", controller.choicePanel.transform);
        controller.choiceLeraButton = CreateButton("Choice 3", controller.choicePanel.transform);
        controller.vnSettingsDimOverlay = CreateGameObject("Settings Overlay", controllerObject.transform);
        controller.vnSettingsPanel = CreateGameObject("Settings Panel", controller.vnSettingsDimOverlay.transform);

        DialogueSceneRegistry registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
        createdObjects.Add(registry);
        registry.scenes.Add(startScene);
        foreach (DialogueSceneData scene in additionalScenes)
        {
            registry.scenes.Add(scene);
        }
        controller.sceneData = startScene;
        controller.sceneRegistry = registry;

        string readKey = "hif_rollback_test_seen_" + Guid.NewGuid().ToString("N");
        playerPrefsKeys.Add(readKey);
        var readHistory = new DialogueReadHistory(readKey);
        SetPrivate(controller, "readHistory", readHistory);

        return new TestContext(controller, gameState, settings, audio, readHistory);
    }

    private DialogueSceneData CreateLinearScene(string sceneId, params string[] texts)
    {
        DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
        createdObjects.Add(scene);
        scene.sceneId = sceneId;
        scene.displayName = sceneId;
        scene.lines = new List<DialogueLine>();
        for (int index = 0; index < texts.Length; index++)
        {
            scene.lines.Add(new DialogueLine
            {
                lineId = $"line-{index}",
                speaker = index % 2 == 0 ? "Narrator" : "Masha",
                text = texts[index]
            });
        }
        scene.choices = new List<DialogueChoice>();
        return scene;
    }

    private static DialogueChoice CreateChoice(
        string text,
        string resultText,
        DialogueSceneData nextScene,
        int lust,
        int romance,
        int purity,
        int corruption,
        int selfControl,
        int suspicion,
        int trustMasha,
        int trustArtem,
        int leraInterest)
    {
        return new DialogueChoice
        {
            text = text,
            resultText = resultText,
            nextScene = nextScene,
            lustDelta = lust,
            romanceDelta = romance,
            purityDelta = purity,
            corruptionDelta = corruption,
            selfControlDelta = selfControl,
            suspicionDelta = suspicion,
            trustMashaDelta = trustMasha,
            trustArtemDelta = trustArtem,
            leraInterestDelta = leraInterest
        };
    }

    private Sprite CreateSprite(Color color)
    {
        var texture = new Texture2D(2, 2);
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        createdObjects.Add(sprite);
        createdObjects.Add(texture);
        return sprite;
    }

    private AudioClip CreateAudioClip(string name)
    {
        AudioClip clip = AudioClip.Create(name, 44100, 1, 44100, false);
        createdObjects.Add(clip);
        return clip;
    }

    private TextMeshProUGUI CreateTmp(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<TextMeshProUGUI>();
    }

    private Image CreateImage(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<Image>();
    }

    private Button CreateButton(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        CreateTmp("Label", obj.transform).text = name;
        return obj.GetComponent<Button>();
    }

    private GameObject CreateGameObject(string name, Transform parent = null)
    {
        var obj = new GameObject(name);
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }
        createdObjects.Add(obj);
        return obj;
    }

    private static void CompleteCurrentLine(VNDialogueController controller)
    {
        Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.True);
        controller.AdvanceDialogue();
        Assert.That(GetPrivate<bool>(controller, "isTyping"), Is.False);
    }

    private static void AdvanceAndComplete(VNDialogueController controller)
    {
        controller.AdvanceDialogue();
        CompleteCurrentLine(controller);
    }

    private static void AssertBacklog(VNDialogueController controller, params string[] expectedTexts)
    {
        List<DialogueBacklogEntry> backlog = controller.CaptureBacklogSnapshot();
        Assert.That(backlog.Count, Is.EqualTo(expectedTexts.Length));
        for (int index = 0; index < expectedTexts.Length; index++)
        {
            Assert.That(backlog[index].text, Is.EqualTo(expectedTexts[index]));
        }
    }

    private static void SetAllNumericState(
        GameState state,
        int lust,
        int romance,
        int purity,
        int corruption,
        int selfControl,
        int suspicion,
        int trustMasha,
        int trustArtem,
        int leraInterest)
    {
        state.lust = lust;
        state.romance = romance;
        state.purity = purity;
        state.corruptionLevel = corruption;
        state.selfControl = selfControl;
        state.suspicion = suspicion;
        state.trustMasha = trustMasha;
        state.trustArtem = trustArtem;
        state.leraInterest = leraInterest;
    }

    private static void AssertAllNumericState(
        GameState state,
        int lust,
        int romance,
        int purity,
        int corruption,
        int selfControl,
        int suspicion,
        int trustMasha,
        int trustArtem,
        int leraInterest)
    {
        Assert.That(state.lust, Is.EqualTo(lust));
        Assert.That(state.romance, Is.EqualTo(romance));
        Assert.That(state.purity, Is.EqualTo(purity));
        Assert.That(state.corruptionLevel, Is.EqualTo(corruption));
        Assert.That(state.selfControl, Is.EqualTo(selfControl));
        Assert.That(state.suspicion, Is.EqualTo(suspicion));
        Assert.That(state.trustMasha, Is.EqualTo(trustMasha));
        Assert.That(state.trustArtem, Is.EqualTo(trustArtem));
        Assert.That(state.leraInterest, Is.EqualTo(leraInterest));
    }

    private static void SimulateSaveLoadReturn(VNDialogueController controller, VNGameMenuView view)
    {
        view.SetSaveLoadSection(VNGameMenuAction.Save);
        FieldInfo contextField = typeof(VNGameMenuController).GetField("childContext", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(contextField, Is.Not.Null);
        contextField.SetValue(controller.GameMenuController, System.Enum.Parse(contextField.FieldType, "SaveLoad"));
        InvokePrivate(controller.GameMenuController, "CloseSaveLoadSection");
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
        return method.Invoke(target, arguments);
    }

    private static T GetPrivate<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
        field.SetValue(target, value);
    }

    private static void DestroyExistingSingletons()
    {
        DestroyAll<VNDialogueController>();
        DestroyAll<GameState>();
        DestroyAll<SettingsManager>();
        DestroyAll<AudioManager>();
        DestroyAll<SaveManager>();
        DestroyAll<SceneFlowManager>();
    }

    private static void DestroyAll<T>() where T : UnityEngine.Object
    {
        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item != null)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }

    private sealed class TestContext
    {
        public TestContext(
            VNDialogueController controller,
            GameState gameState,
            SettingsManager settings,
            AudioManager audio,
            DialogueReadHistory readHistory)
        {
            Controller = controller;
            GameState = gameState;
            Settings = settings;
            Audio = audio;
            ReadHistory = readHistory;
        }

        public VNDialogueController Controller { get; }
        public GameState GameState { get; }
        public SettingsManager Settings { get; }
        public AudioManager Audio { get; }
        public DialogueReadHistory ReadHistory { get; }
    }
}
