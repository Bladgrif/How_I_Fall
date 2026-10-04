using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    [Category("InteractiveHotspot")]
    public sealed class InteractiveHotspotPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;

        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160), new Vector2Int(1024, 768)
        };

        private const string AutoSavePreferenceKey = "hif_auto_save";
        private bool originalAutoSavePreferenceExisted;
        private int originalAutoSavePreferenceValue;

        public override void Setup()
        {
            // InputTestFixture.Setup() resets the whole input runtime, so the virtual
            // keyboard/gamepad/mouse must be added after it — the same convention as
            // RMBOneLevelRuntimePlayModeTests. These devices let the tests drive the
            // real scene InputSystemUIInputModule instead of ExecuteEvents shortcuts.
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            gamepad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalAutoSavePreferenceExisted = PlayerPrefs.HasKey(AutoSavePreferenceKey);
            originalAutoSavePreferenceValue = PlayerPrefs.GetInt(AutoSavePreferenceKey, 1);

            // VNPrototype can autosave during startup, before the test body runs.
            SaveManager.ScreenshotCaptureOverrideForTests = CreatePreviewTexture;
            PlayerPrefs.SetInt(AutoSavePreferenceKey, 0);
            PlayerPrefs.Save();
            SettingsManager.Instance?.SetAutoSave(false);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveManager.ScreenshotCaptureOverrideForTests = null;

            if (originalAutoSavePreferenceExisted)
            {
                PlayerPrefs.SetInt(AutoSavePreferenceKey, originalAutoSavePreferenceValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(AutoSavePreferenceKey);
            }
            PlayerPrefs.Save();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.LoadSettings();
            }
            if (!originalAutoSavePreferenceExisted)
            {
                PlayerPrefs.DeleteKey(AutoSavePreferenceKey);
                PlayerPrefs.Save();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TechnicalRoom_LaptopUnlocksDoor_WindowIsOneShot_AndDoorRestoresNarrative()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            int initialTrustMasha = state.trustMasha;
            EnsureEventSystem();

            Assert.That(dialogue.TryStartInteractiveScene(room, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
            Assert.That(interactive, Is.Not.Null.And.Property("IsRunning").True);
            Assert.That(dialogue.CanAdvanceDialogue, Is.False, "Interactive scene must block underlying dialogue input.");
            Assert.That(interactive.IsHotspotAvailable("test_laptop"), Is.True);
            Assert.That(interactive.IsHotspotAvailable("test_door"), Is.False);
            Assert.That(interactive.GetHotspotButton("test_door").interactable, Is.False);

            Click(interactive.GetHotspotButton("test_laptop"));
            yield return null;
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(interactive.IsHotspotCompleted("test_laptop"), Is.True);
            Assert.That(interactive.IsHotspotAvailable("test_door"), Is.True);

            Click(interactive.GetHotspotButton("test_window"));
            yield return null;
            int activationsAfterWindow = interactive.ActivationCount;
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
            Assert.That(interactive.IsHotspotCompleted("test_window"), Is.True);
            Assert.That(interactive.GetHotspotButton("test_window").interactable, Is.False);

            ExecuteEvents.Execute<IPointerClickHandler>(interactive.GetHotspotButton("test_window").gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(interactive.ActivationCount, Is.EqualTo(activationsAfterWindow), "A disabled one-shot hotspot must not run a second outcome.");

        Assert.That(dialogue.OpenGameMenu(), Is.True, "Interactive mode must permit the existing Game Menu round-trip.");
        Assert.That(dialogue.IsGameMenuOpen, Is.True);
        Assert.That(dialogue.CanSave, Is.False);
        Assert.That(dialogue.CanLoad, Is.False);
        Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.False);
        Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.False);
        Assert.That(dialogue.GameMenuController.Close(), Is.True);
            Assert.That(interactive.IsRunning, Is.True, "Game Menu close must return to the active room.");

            foreach (Vector2Int resolution in Resolutions)
            {
                Screen.SetResolution(resolution.x, resolution.y, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertHotspotsInsideImage(interactive);
            }

            Click(interactive.GetHotspotButton("test_door"));
            yield return null;
            Assert.That(interactive.IsRunning, Is.False);
            Assert.That(dialogue.CanAdvanceDialogue, Is.True, "Door completion must restore normal dialogue eligibility.");
            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo("interactive_hotspot_complete"));
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
            Assert.That(dialogue.OpenGameMenu(), Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.Close(), Is.True);

            Assert.That(dialogue.TryStartInteractiveScene(room, out failure), Is.True, failure);
            interactive = dialogue.ActiveInteractiveSceneController;
            Assert.That(interactive.IsHotspotAvailable("test_laptop"), Is.True);
            Assert.That(interactive.IsHotspotAvailable("test_door"), Is.False);
            Assert.That(interactive.IsHotspotAvailable("test_window"), Is.True);
            Click(interactive.GetHotspotButton("test_laptop"));
            Click(interactive.GetHotspotButton("test_door"));
            yield return null;
            Assert.That(interactive.IsRunning, Is.False);
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
        }

        [UnityTest]
        public IEnumerator AuthoredBackground_IsDisplayed_AndNullBackgroundFallsBackOnRestart()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            EnsureEventSystem();

            Texture2D authoredTexture = CreateDistinctBackgroundTexture();
            Sprite authoredSprite = Sprite.Create(authoredTexture, new Rect(0f, 0f, authoredTexture.width, authoredTexture.height), new Vector2(0.5f, 0.5f), 36f);
            authoredSprite.name = authoredTexture.name;
            InteractiveSceneData authoredScene = CreateAuthoredBackgroundScene(authoredSprite);
            try
            {
                Assert.That(dialogue.TryStartInteractiveScene(authoredScene, out string failure), Is.True, failure);
                InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
                Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
                Assert.That(displayed.sprite, Is.EqualTo(authoredSprite),
                    "A non-null authored scene.background must be displayed instead of the technical fallback.");

                Click(interactive.GetHotspotButton("tech_exit"));
                yield return null;
                Assert.That(interactive.IsRunning, Is.False, "The completeScene fixture must close the runtime without extra routing.");
            }
            finally
            {
                UnityEngine.Object.Destroy(authoredScene);
                UnityEngine.Object.Destroy(authoredSprite);
                UnityEngine.Object.Destroy(authoredTexture);
            }

            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            Assert.That(room.background, Is.Null, "The committed TECH room fixture must keep exercising the fallback path.");
            Assert.That(dialogue.TryStartInteractiveScene(room, out string restartFailure), Is.True, restartFailure);
            InteractiveSceneController restarted = dialogue.ActiveInteractiveSceneController;
            Image restartedImage = restarted.DisplayedImageRect.GetComponent<Image>();
            Assert.That(restartedImage.sprite.name, Is.EqualTo("InteractiveHotspotTechnicalRoom"),
                "A null scene.background must fall back to the existing technical background on (re)start.");

            ExecuteEvents.Execute<IPointerClickHandler>(restarted.GetHotspotButton("test_door").gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(restarted.IsRunning, Is.True, "The locked door must not complete the scene.");
            Click(restarted.GetHotspotButton("test_laptop"));
            Click(restarted.GetHotspotButton("test_door"));
            yield return null;
            Assert.That(restarted.IsRunning, Is.False, "Door completion must close the runtime after the fallback restart.");
        }

        [UnityTest]
        public IEnumerator KeyboardFocus_ContinuesThroughGameMenuReturn_AndReentryIsClean()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            int initialTrustMasha = state.trustMasha;
            EnsureEventSystem();

            Assert.That(dialogue.TryStartInteractiveScene(room, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
            Button laptop = interactive.GetHotspotButton("test_laptop");
            Button door = interactive.GetHotspotButton("test_door");
            Button window = interactive.GetHotspotButton("test_window");
            Button[] ownedButtons = { laptop, door, window };
            Assert.That(laptop, Is.Not.Null.And.Property("interactable").True, "Laptop must start available.");
            Assert.That(door.interactable, Is.False, "Precondition: door starts locked behind the laptop prerequisite.");

            // Initial focus: exactly one usable keyboard/controller owner — the first
            // available hotspot, never the locked one.
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(laptop.gameObject),
                "Hotspot entry must give keyboard/controller a deterministic usable focus owner on the first available hotspot.");

            // Hotspot navigation is an explicit authored-order chain, so one Right
            // press reaches the locked door (visible LOCKED state), and Submit on it
            // must not activate anything.
            yield return PressFrame(keyboard.rightArrowKey);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "Directional navigation must reach the locked hotspot instead of hidden scene UI.");
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.ActivationCount, Is.EqualTo(0), "Submit on a locked hotspot must not run its outcome.");
            Assert.That(interactive.IsHotspotCompleted("test_door"), Is.False);

            // Directional navigation reaches available hotspot buttons and never leaves the Hotspot controls.
            yield return NavigateUntilSelected(window, ownedButtons, new[] { keyboard.rightArrowKey });
            yield return NavigateUntilSelected(laptop, ownedButtons, new[] { keyboard.leftArrowKey });

            // Submit activates the selected available hotspot exactly once.
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.IsHotspotCompleted("test_laptop"), Is.True, "Keyboard Submit must activate the selected available hotspot.");
            Assert.That(interactive.ActivationCount, Is.EqualTo(1));
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion), "Local hotspot completion must not touch canonical GameState.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "A selected hotspot that became disabled/completed must hand focus to the next valid hotspot.");

            // Game Menu round-trip: keyboard/controller must be able to continue immediately.
            Assert.That(dialogue.OpenGameMenu(), Is.True, "Interactive mode must permit the existing Game Menu round-trip.");
            Assert.That(dialogue.IsGameMenuOpen, Is.True);
            Assert.That(dialogue.CanSave, Is.False);
            Assert.That(dialogue.CanLoad, Is.False);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.False);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.False);
            Assert.That(dialogue.GameMenuController.Close(), Is.True);
            Assert.That(interactive.IsRunning, Is.True, "Game Menu close must return to the active room.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "Game Menu return must restore a usable Hotspot focus owner without a mouse click.");

            // Completion straight from the keyboard: door routes to the completion scene.
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.IsRunning, Is.False, "Door completion must close the interactive runtime.");
            Assert.That(dialogue.CanAdvanceDialogue, Is.True, "Completion must restore normal dialogue eligibility.");
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "Completion must leave the Reading dialogue shell unsuppressed.");
            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo("interactive_hotspot_complete"));
            GameObject staleSelection = EventSystem.current.currentSelectedGameObject;
            Assert.That(staleSelection == null || (staleSelection != laptop.gameObject && staleSelection != door.gameObject && staleSelection != window.gameObject),
                "Stale EventSystem selection must not keep pointing at hidden Hotspot controls.");

            Assert.That(dialogue.OpenGameMenu(), Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.Close(), Is.True);

            // Re-entry starts clean and restores the initial focus owner.
            Assert.That(dialogue.TryStartInteractiveScene(room, out failure), Is.True, failure);
            interactive = dialogue.ActiveInteractiveSceneController;
            laptop = interactive.GetHotspotButton("test_laptop");
            door = interactive.GetHotspotButton("test_door");
            window = interactive.GetHotspotButton("test_window");
            Assert.That(interactive.ActivationCount, Is.EqualTo(0), "Repeated entry must start a clean run.");
            Assert.That(interactive.IsHotspotAvailable("test_laptop"), Is.True, "One-shot hotspots must reset for the new run.");
            Assert.That(interactive.IsHotspotAvailable("test_door"), Is.False, "Local prerequisites must reset for the new run.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(laptop.gameObject),
                "Repeated entry must restore the initial usable focus owner.");
            Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
            Assert.That(displayed.sprite.name, Is.EqualTo("InteractiveHotspotTechnicalRoom"),
                "A null-background restart must keep the technical fallback.");

            // Pointer interaction still works after keyboard use.
            Click(window);
            yield return null;
            Assert.That(interactive.IsHotspotCompleted("test_window"), Is.True);
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.IsHotspotCompleted("test_laptop"), Is.True);
            Click(door);
            yield return null;
            Assert.That(interactive.IsRunning, Is.False);
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
        }

        [UnityTest]
        public IEnumerator GamepadNavigation_ReachesHotspots_AndSubmitActivatesExactlyOnce()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            EnsureEventSystem();

            Assert.That(dialogue.TryStartInteractiveScene(room, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
            Button laptop = interactive.GetHotspotButton("test_laptop");
            Button door = interactive.GetHotspotButton("test_door");
            Button window = interactive.GetHotspotButton("test_window");
            Button[] ownedButtons = { laptop, door, window };
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(laptop.gameObject),
                "Controller entry must find the available hotspot, not the locked one, as the initial focus owner.");

            yield return PressFrame(gamepad.dpad.right);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "Controller navigation must reach the locked hotspot instead of hidden scene UI.");
            AssertFocusInsideHotspots(ownedButtons);
            yield return NavigateUntilSelected(window, ownedButtons, new[] { gamepad.dpad.right });
            yield return NavigateUntilSelected(laptop, ownedButtons, new[] { gamepad.dpad.left });

            yield return PressFrame(gamepad.buttonSouth);
            Assert.That(interactive.IsHotspotCompleted("test_laptop"), Is.True, "Controller Submit must activate the selected available hotspot.");
            Assert.That(interactive.ActivationCount, Is.EqualTo(1), "Controller Submit must activate exactly once.");
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion), "Local hotspot completion must not touch canonical GameState.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "A completed one-shot must hand controller focus to the next valid hotspot.");
            Assert.That(interactive.IsHotspotAvailable("test_door"), Is.True);

            Click(door);
            yield return null;
            Assert.That(interactive.IsRunning, Is.False, "Pointer path must still complete the scene after controller use.");
        }

        [UnityTest]
        public IEnumerator Showcase_LaptopAndNotesUnlockDoor_MenuRoundTrip_AndCleanReentry()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData showcase = Resources.Load<InteractiveSceneData>("InteractiveHotspot/HotspotShowcaseRoom");
            Assert.That(showcase, Is.Not.Null, "Hotspot showcase resource is missing.");

            // Data contract: the approved background sprite, exactly three showcase
            // hotspots with Russian labels, and the door behind both local prerequisites.
            Assert.That(showcase.background, Is.Not.Null, "The showcase must reference the approved background sprite.");
            Assert.That(showcase.background.name, Is.EqualTo("HotspotShowcaseRoom"));
            Assert.That(showcase.hotspots.Count, Is.EqualTo(3), "The showcase contains exactly three visible actions.");
            Assert.That(showcase.hotspots.Select(hotspot => hotspot.hotspotId), Is.EquivalentTo(new[] { "showcase_laptop", "showcase_notes", "showcase_door" }));
            Assert.That(showcase.hotspots.Select(hotspot => hotspot.displayName), Is.EquivalentTo(new[] { "Ноутбук", "Записки", "Дверь" }));
            InteractiveHotspotData doorData = showcase.hotspots.First(hotspot => hotspot.hotspotId == "showcase_door");
            Assert.That(doorData.requiredCompletedHotspotIds, Is.EquivalentTo(new[] { "showcase_laptop", "showcase_notes" }),
                "The Door must require both the Laptop and the Notes as local prerequisites.");
            Assert.That(showcase.hotspots.Where(hotspot => hotspot.hotspotId != "showcase_door").All(hotspot => hotspot.outcome.stateChanges.Count == 0),
                Is.True, "TECH showcase outcomes must not mutate canonical GameState.");

            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            int initialTrustMasha = state.trustMasha;
            EnsureEventSystem();

            Assert.That(dialogue.TryStartInteractiveScene(showcase, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
            Button laptop = interactive.GetHotspotButton("showcase_laptop");
            Button notes = interactive.GetHotspotButton("showcase_notes");
            Button door = interactive.GetHotspotButton("showcase_door");
            Button[] ownedButtons = { laptop, notes, door };

            // Presentation: the approved sprite is displayed, and the runtime surface
            // carries compact markers only — hotspot feedback is presented by the accepted
            // Reading dialogue shell instead of a private Menu button or feedback panel.
            Image displayed = interactive.DisplayedImageRect.GetComponent<Image>();
            Assert.That(displayed.sprite, Is.EqualTo(showcase.background), "The approved showcase background must be displayed instead of the fallback.");
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "Hotspot mode must not suppress the accepted Reading dialogue shell.");
            AssertHotspotViewBelowDialogueBox(dialogue, interactive);
            Transform view = interactive.DisplayedImageRect.parent.parent;
            Assert.That(view.Find("Hotspot Menu Button"), Is.Null, "Hotspot mode must not present its own Menu button; Game Menu access stays on the established Esc/RMB path.");
            Assert.That(view.Find("Feedback Panel"), Is.Null, "Hotspot mode must not present its own feedback panel.");
            yield return WaitFor(() => dialogue.dialogueText.text == showcase.initialFeedback, "Showcase initial feedback did not reach the Reading dialogue shell.");
            Assert.That(dialogue.speakerText.text, Is.EqualTo(showcase.feedbackSpeaker), "Initial showcase feedback must present the scene speaker through the Reading shell.");
            Assert.That(dialogue.nameBox.activeSelf, Is.True, "The showcase speaker must be presented with the accepted Reading name box.");
            AssertMarkerPresentation(ownedButtons);

            Assert.That(laptop.interactable, Is.True, "Ноутбук must start available.");
            Assert.That(notes.interactable, Is.True, "Записки must start available.");
            Assert.That(door.interactable, Is.False, "Дверь must start locked behind both prerequisites.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(laptop.gameObject),
                "Initial focus must be the first available showcase hotspot.");

            // Markers keep sitting over the artwork at standard QA resolutions.
            foreach (Vector2Int resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                Screen.SetResolution(resolution.x, resolution.y, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertShowcaseMarkersInsideImage(interactive);
            }

            // Laptop alone is insufficient to unlock the Door.
            Click(laptop);
            yield return null;
            Assert.That(interactive.IsHotspotCompleted("showcase_laptop"), Is.True);
            Assert.That(interactive.IsHotspotAvailable("showcase_door"), Is.False, "Laptop alone must not unlock the Door.");
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion), "Local showcase completion must not touch canonical GameState.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(notes.gameObject),
                "A completed one-shot must hand focus to the next authored hotspot.");
            InteractiveHotspotOutcome laptopOutcome = showcase.hotspots.First(hotspot => hotspot.hotspotId == "showcase_laptop").outcome;
            yield return WaitFor(() => dialogue.dialogueText.text == laptopOutcome.feedbackText, "Laptop outcome feedback did not reach the Reading dialogue shell.");
            Assert.That(dialogue.speakerText.text, Is.EqualTo(showcase.feedbackSpeaker), "Outcome showcase feedback must keep presenting the scene speaker.");

            // Keyboard navigation stays inside the showcase controls; the locked Door is reachable and safe.
            yield return PressFrame(keyboard.rightArrowKey);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject), "Navigation must reach the locked Door.");
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.ActivationCount, Is.EqualTo(1), "Submit on the locked Door must be a no-op.");
            Assert.That(interactive.IsHotspotCompleted("showcase_door"), Is.False);
            yield return PressFrame(keyboard.leftArrowKey);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(notes.gameObject));

            // Game Menu round-trip: Save/Load stay blocked and focus is restored on close.
            Assert.That(dialogue.OpenGameMenu(), Is.True, "Interactive mode must permit the existing Game Menu round-trip.");
            Assert.That(dialogue.IsGameMenuOpen, Is.True);
            Assert.That(dialogue.CanSave, Is.False);
            Assert.That(dialogue.CanLoad, Is.False);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.False);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.False);
            Assert.That(dialogue.GameMenuController.Close(), Is.True);
            Assert.That(interactive.IsRunning, Is.True, "Game Menu close must return to the active showcase.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(notes.gameObject),
                "Game Menu return must restore the usable Hotspot focus owner without a mouse click.");

            // Notes completes the prerequisite pair and unlocks the Door.
            yield return PressFrame(keyboard.enterKey);
            Assert.That(interactive.IsHotspotCompleted("showcase_notes"), Is.True);
            Assert.That(interactive.IsHotspotAvailable("showcase_door"), Is.True, "Laptop + Notes must unlock the Door.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject),
                "Focus must move to the unlocked Door.");

            // Door completes the scene exactly once and routes back to Reading.
            int activationsBeforeDoor = interactive.ActivationCount;
            Click(door);
            yield return null;
            Assert.That(interactive.IsRunning, Is.False, "Door completion must close the interactive runtime.");
            Assert.That(interactive.ActivationCount, Is.EqualTo(activationsBeforeDoor + 1), "Door must activate exactly once.");
            Assert.That(dialogue.CanAdvanceDialogue, Is.True, "Completion must restore normal dialogue eligibility.");
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "Completion must leave the Reading dialogue shell unsuppressed.");
            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo("interactive_hotspot_complete"));
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
            GameObject staleSelection = EventSystem.current.currentSelectedGameObject;
            Assert.That(staleSelection == null || ownedButtons.All(button => button.gameObject != staleSelection),
                "Stale EventSystem selection must not keep pointing at hidden showcase controls.");

            // Save/Load are restored after completion.
            Assert.That(dialogue.OpenGameMenu(), Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Load).interactable, Is.True);
            Assert.That(dialogue.GameMenuController.Close(), Is.True);

            // Clean re-entry with the approved background.
            Assert.That(dialogue.TryStartInteractiveScene(showcase, out failure), Is.True, failure);
            interactive = dialogue.ActiveInteractiveSceneController;
            laptop = interactive.GetHotspotButton("showcase_laptop");
            notes = interactive.GetHotspotButton("showcase_notes");
            door = interactive.GetHotspotButton("showcase_door");
            ownedButtons = new[] { laptop, notes, door };
            Assert.That(interactive.ActivationCount, Is.EqualTo(0), "Repeated entry must start a clean run.");
            Assert.That(interactive.IsHotspotAvailable("showcase_laptop"), Is.True, "One-shot hotspots must reset for the new run.");
            Assert.That(interactive.IsHotspotAvailable("showcase_door"), Is.False, "Local prerequisites must reset for the new run.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(laptop.gameObject),
                "Repeated entry must restore the initial focus owner.");
            Image restarted = interactive.DisplayedImageRect.GetComponent<Image>();
            Assert.That(restarted.sprite, Is.EqualTo(showcase.background), "Re-entry keeps the approved background.");

            Click(laptop);
            Click(notes);
            Click(door);
            yield return null;
            Assert.That(interactive.IsRunning, Is.False, "Door completion must close the runtime on re-entry too.");
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
            Assert.That(state.trustMasha, Is.EqualTo(initialTrustMasha));
        }

        [UnityTest]
        public IEnumerator Hotspot_FeedbackUsesReadingShell_NoPrivateChrome_AndEscRmbKeepGameMenuAccess()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            EnsureEventSystem();

            Assert.That(dialogue.TryStartInteractiveScene(room, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;

            // The accepted Reading dialogue shell stays in charge of presentation: never
            // suppressed, rendered above the Hotspot view, with no private Hotspot chrome.
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "Hotspot mode must not suppress the accepted Reading dialogue shell.");
            AssertHotspotViewBelowDialogueBox(dialogue, interactive);
            Transform view = interactive.DisplayedImageRect.parent.parent;
            Assert.That(view.Find("Hotspot Menu Button"), Is.Null, "Hotspot mode must not present its own Menu button; Game Menu access stays on the established Esc/RMB path.");
            Assert.That(view.Find("Feedback Panel"), Is.Null, "Hotspot mode must not present its own feedback panel.");

            // Initial feedback appears in the shell (TECH room: narration without a speaker).
            yield return WaitFor(() => dialogue.dialogueText.text == room.initialFeedback, "Initial Hotspot feedback did not reach the Reading dialogue shell.");
            Assert.That(dialogue.nameBox.activeSelf, Is.False, "A null scene.feedbackSpeaker must present as narration without a name box.");

            // The established Escape path still opens the existing Game Menu over the Hotspot.
            yield return PressFrame(keyboard.escapeKey);
            Assert.That(dialogue.IsGameMenuOpen, Is.True, "Esc must open the existing Game Menu while the Hotspot owns interaction.");
            Assert.That(dialogue.CanSave, Is.False, "Hotspot mode must keep Save blocked while the Game Menu is open.");
            Assert.That(dialogue.CanLoad, Is.False, "Hotspot mode must keep Load blocked while the Game Menu is open.");
            yield return PressFrame(keyboard.escapeKey);
            Assert.That(dialogue.IsGameMenuOpen, Is.False, "Esc must close the Game Menu back into the active Hotspot.");
            Assert.That(interactive.IsRunning, Is.True, "Game Menu close must return to the active room.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(interactive.GetHotspotButton("test_laptop").gameObject),
                "Game Menu return must restore a usable Hotspot focus owner.");

            // RMB keeps the same single-owner Game Menu access path.
            yield return PressFrame(mouse.rightButton);
            Assert.That(dialogue.IsGameMenuOpen, Is.True, "RMB must open the existing Game Menu while the Hotspot owns interaction.");
            yield return PressFrame(mouse.rightButton);
            Assert.That(dialogue.IsGameMenuOpen, Is.False, "RMB must close the Game Menu back into the active Hotspot.");

            // Outcome feedback is presented through the same shell.
            Click(interactive.GetHotspotButton("test_laptop"));
            yield return WaitFor(() => dialogue.dialogueText.text == "TEST:computer_checked = true", "Hotspot outcome feedback did not reach the Reading dialogue shell.");

            // Save/Load blocking stays correct while the Hotspot owns interaction.
            Assert.That(dialogue.CanSave, Is.False, "Hotspot mode must keep Save blocked.");
            Assert.That(dialogue.CanLoad, Is.False, "Hotspot mode must keep Load blocked.");
        }

        /// <summary>Hands-on correction: the Hotspot view renders inside the Reading shell layer, directly below the ordinary Dialogue Box.</summary>
        private static void AssertHotspotViewBelowDialogueBox(VNDialogueController dialogue, InteractiveSceneController interactive)
        {
            Transform shellBox = dialogue.dialogueUiRoot.transform;
            Transform view = interactive.DisplayedImageRect.parent.parent;
            Assert.That(view.name, Is.EqualTo("Interactive Hotspot Runtime View"));
            Assert.That(view.parent, Is.EqualTo(shellBox.parent),
                "The Hotspot view must live in the Reading shell layer, not as a private top-most canvas layer.");
            Assert.That(view.GetSiblingIndex(), Is.LessThan(shellBox.GetSiblingIndex()),
                "The Hotspot view must render below the ordinary Dialogue Box so shell feedback stays visible.");
            Assert.That(shellBox.gameObject.activeInHierarchy, Is.True, "The Reading dialogue shell must stay visible during Hotspot mode.");
        }

        private static void AssertMarkerPresentation(Button[] ownedButtons)
        {
            foreach (Button button in ownedButtons)
            {
                Transform marker = button.transform.Find("Marker");
                Assert.That(marker, Is.Not.Null, button.name + " must render a compact marker instead of a giant overlay rectangle.");
                Image hitArea = button.GetComponent<Image>();
                Assert.That(hitArea, Is.Not.Null);
                Assert.That(hitArea.color.a, Is.LessThan(0.02f), "The hotspot hit region must stay invisible over the artwork.");
                Assert.That(marker.Find("Chip Fill"), Is.Null, "Target 09 has no filled circular chip.");
                Assert.That(marker.Find("Chip Ring"), Is.Null, "Target 09 uses open corner brackets, not a ring.");
                Assert.That(marker.Find("Label/Label Fill"), Is.Null, "Labels must not regain a pill background.");
                Assert.That(marker.Find("Label/Label Edge"), Is.Null);
                foreach (string name in new[] { "Open Brackets", "Focus Glow", "Hollow Diamond", "Focus Chevron", "Locked Indicator", "Glyph" })
                    Assert.That(marker.Find(name)?.GetComponent<Image>()?.sprite, Is.Not.Null, button.name + " is missing " + name);
                RectTransform line = marker.Find("Attachment Line").GetComponent<RectTransform>();
                Assert.That(line.sizeDelta.x, Is.InRange(1f, 2f), "Attachment stays thin.");
                Assert.That(line.sizeDelta.y, Is.GreaterThan(0f));
                foreach (Image child in marker.GetComponentsInChildren<Image>(true))
                    Assert.That(child.raycastTarget, Is.False, button.name + " marker visuals must not intercept pointer input.");
            }
        }

        private static void AssertMarkerState(Button button, bool focused, bool locked, bool completed = false)
        {
            Transform marker = button.transform.Find("Marker");
            foreach (string name in new[] { "Focus Glow", "Focus Chevron" })
                Assert.That(marker.Find(name).gameObject.activeSelf, Is.EqualTo(focused && !completed), name);
            foreach (string name in new[] { "Open Brackets", "Attachment Line", "Hollow Diamond", "Label" })
                Assert.That(marker.Find(name).gameObject.activeSelf, Is.EqualTo(!completed), name);
            Assert.That(marker.Find("Locked Indicator").gameObject.activeSelf, Is.EqualTo(locked && !completed), "Locked focus must keep its padlock.");
            Assert.That(marker.localScale, Is.EqualTo(Vector3.one), "Focus must not resize the semantic position or attachment.");
            if (completed) Assert.That(marker.Find("Glyph").GetComponent<Image>().color.a, Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator Target09_FocusHoverLockAndCompletion_KeepAuthoredHitRegions()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            EnsureEventSystem();
            InteractiveSceneData showcase = Resources.Load<InteractiveSceneData>("InteractiveHotspot/HotspotShowcaseRoom");
            Assert.That(VNDialogueController.Instance.TryStartInteractiveScene(showcase, out string failure), Is.True, failure);
            InteractiveSceneController interactive = VNDialogueController.Instance.ActiveInteractiveSceneController;
            Button laptop = interactive.GetHotspotButton("showcase_laptop");
            Button notes = interactive.GetHotspotButton("showcase_notes");
            Button door = interactive.GetHotspotButton("showcase_door");
            InputState.Change(mouse.position, new Vector2(5f, 5f));
            yield return null;
            AssertMarkerPresentation(new[] { laptop, notes, door });
            AssertMarkerState(laptop, focused: true, locked: false);
            AssertMarkerState(notes, focused: false, locked: false);
            AssertMarkerState(door, focused: false, locked: true);
            Sprite idleBrackets = notes.transform.Find("Marker/Open Brackets").GetComponent<Image>().sprite;

            yield return PressFrame(keyboard.rightArrowKey);
            AssertMarkerState(laptop, focused: false, locked: false);
            AssertMarkerState(notes, focused: true, locked: false);
            Assert.That(notes.transform.Find("Marker/Open Brackets").GetComponent<Image>().sprite, Is.Not.EqualTo(idleBrackets), "Focus changes contour geometry, not just color.");
            yield return PressFrame(gamepad.dpad.right);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject));
            AssertMarkerState(notes, focused: false, locked: false);
            AssertMarkerState(door, focused: true, locked: true);
            yield return PressFrame(gamepad.buttonSouth);
            Assert.That(interactive.ActivationCount, Is.Zero, "Locked controller Submit stays a no-op.");

            // Real virtual mouse hover goes through the existing UI input module.
            InputState.Change(mouse.position, GetScreenCenter(notes));
            yield return null;
            yield return null;
            AssertMarkerState(notes, focused: true, locked: false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(door.gameObject), "Hover must not steal the selected owner.");
            AssertMarkerState(door, focused: false, locked: true);
            Assert.That(new[] { laptop, notes, door }.Count(button => button.transform.Find("Marker/Focus Chevron").gameObject.activeSelf), Is.EqualTo(1),
                "Pointer emphasis must not leave a second keyboard/controller marker visually focused.");
            InputState.Change(mouse.position, new Vector2(5f, 5f));
            yield return null;
            yield return null;
            AssertMarkerState(notes, focused: false, locked: false);
            AssertMarkerState(door, focused: true, locked: true);

            Click(laptop);
            AssertMarkerState(laptop, focused: false, locked: false, completed: true);
            Click(notes);
            AssertMarkerState(notes, focused: false, locked: false, completed: true);
            AssertMarkerState(door, focused: true, locked: false);
            foreach (InteractiveHotspotData hotspot in showcase.hotspots)
            {
                RectTransform hit = interactive.GetHotspotButton(hotspot.hotspotId).GetComponent<RectTransform>();
                Assert.That(hit.anchorMin, Is.EqualTo(hotspot.normalizedRect.min));
                Assert.That(hit.anchorMax, Is.EqualTo(hotspot.normalizedRect.max));
                Assert.That(hit.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(hit.offsetMax, Is.EqualTo(Vector2.zero));
            }
            Click(door);
            yield return null;
            Assert.That(interactive.IsRunning, Is.False);
        }

        private static void AssertShowcaseMarkersInsideImage(InteractiveSceneController interactive)
        {
            RectTransform image = interactive.DisplayedImageRect;
            Assert.That(image, Is.Not.Null);
            Vector3[] imageCorners = new Vector3[4]; image.GetWorldCorners(imageCorners);
            foreach (string id in new[] { "showcase_laptop", "showcase_notes", "showcase_door" })
            {
                RectTransform hotspot = interactive.GetHotspotButton(id).GetComponent<RectTransform>();
                Vector3[] hotspotCorners = new Vector3[4]; hotspot.GetWorldCorners(hotspotCorners);
                foreach (Vector3 corner in hotspotCorners)
                {
                    Assert.That(corner.x, Is.InRange(imageCorners[0].x - .1f, imageCorners[2].x + .1f), id + " left/right drifted outside the displayed image.");
                    Assert.That(corner.y, Is.InRange(imageCorners[0].y - .1f, imageCorners[2].y + .1f), id + " top/bottom drifted outside the displayed image.");
                }
            }
        }

        [UnityTest]
        public IEnumerator VirtualMouseClick_ActivatesHotspot_BackdropClickDoesNotReachReading()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            InteractiveSceneData room = Resources.Load<InteractiveSceneData>("InteractiveHotspot/TechnicalInteractiveRoom");
            Assert.That(room, Is.Not.Null, "TECH Interactive Room resource is missing.");
            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            EnsureEventSystem();
            Screen.SetResolution(1920, 1080, false);
            yield return null;

            Assert.That(dialogue.TryStartInteractiveScene(room, out string failure), Is.True, failure);
            InteractiveSceneController interactive = dialogue.ActiveInteractiveSceneController;
            Button laptop = interactive.GetHotspotButton("test_laptop");
            Button window = interactive.GetHotspotButton("test_window");

            // Real pointer press/release through the runtime UI input module (virtual mouse).
            yield return ClickScreenPoint(GetScreenCenter(laptop));
            Assert.That(interactive.IsHotspotCompleted("test_laptop"), Is.True,
                "A pointer press/release through the runtime UI input module must activate the hotspot.");
            Assert.That(interactive.ActivationCount, Is.EqualTo(1));

            // A click on the interactive backdrop must not leak into suppressed Reading.
            string sceneBefore = GameState.Instance.currentSceneId;
            yield return ClickScreenPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.04f));
            Assert.That(interactive.IsRunning, Is.True, "A backdrop click must not complete or close the interactive runtime.");
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "A backdrop click must leave the accepted Reading dialogue shell untouched.");
            Assert.That(dialogue.CanAdvanceDialogue, Is.False, "A backdrop click must not restore Reading input ownership.");
            Assert.That(GameState.Instance.currentSceneId, Is.EqualTo(sceneBefore), "A backdrop click must not advance the underlying dialogue.");
            Assert.That(interactive.ActivationCount, Is.EqualTo(1), "A backdrop click must not activate any hotspot.");

            // Pointer interaction continues to drive other hotspots after the keyboard path.
            yield return ClickScreenPoint(GetScreenCenter(window));
            Assert.That(interactive.IsHotspotCompleted("test_window"), Is.True);
            Assert.That(state.suspicion, Is.EqualTo(initialSuspicion));
        }

        private IEnumerator PressFrame(ButtonControl button)
        {
            Assert.That(button != null && button.device != null && button.device.added, Is.True, "Virtual input device control must exist for the whole press.");
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        private IEnumerator ClickScreenPoint(Vector2 screenPoint)
        {
            InputState.Change(mouse.position, screenPoint);
            yield return null;
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private IEnumerator NavigateUntilSelected(Button target, Button[] ownedButtons, ButtonControl[] cycleKeys)
        {
            for (int attempt = 0; attempt < 10 && EventSystem.current.currentSelectedGameObject != target.gameObject; attempt++)
            {
                AssertFocusInsideHotspots(ownedButtons);
                yield return PressFrame(cycleKeys[attempt % cycleKeys.Length]);
            }
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(target.gameObject),
                "Directional navigation did not reach the expected hotspot button.");
        }

        private static void AssertFocusInsideHotspots(Button[] ownedButtons)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            string selectedName = selected == null ? "<null>" : selected.name;
            Assert.That(selected, Is.Not.Null, "Hotspot keyboard/controller focus owner is missing.");
            Assert.That(ownedButtons.Any(button => button != null && button.gameObject == selected), Is.True,
                "Keyboard/controller focus escaped the Hotspot runtime controls (selected: " + selectedName + ").");
        }

        private static Vector2 GetScreenCenter(Button button)
        {
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas != null ? canvas.worldCamera : null;
            Vector3[] corners = new Vector3[4];
            button.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return (min + max) * 0.5f;
        }

        private static Texture2D CreateDistinctBackgroundTexture()
        {
            // Clearly distinguishable from the dark technical fallback: teal→amber
            // gradient with a white top band. TECH DEMO ONLY / NOT CANON.
            Texture2D texture = new Texture2D(64, 36, TextureFormat.RGBA32, false) { name = "TECH_AuthoredHotspotBackground" };
            Color[] pixels = new Color[64 * 36];
            for (int y = 0; y < 36; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (y >= 32)
                    {
                        pixels[y * 64 + x] = new Color(0.95f, 0.97f, 1f, 1f);
                        continue;
                    }
                    float t = x / 63f;
                    pixels[y * 64 + x] = Color.Lerp(new Color(0.10f, 0.62f, 0.55f, 1f), new Color(0.72f, 0.45f, 0.15f, 1f), t);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static InteractiveSceneData CreateAuthoredBackgroundScene(Sprite background)
        {
            InteractiveSceneData scene = ScriptableObject.CreateInstance<InteractiveSceneData>();
            scene.sceneId = "tech_authored_background_room";
            scene.displayName = "Interactive Room (authored background) — TECH DEMO ONLY / NOT CANON";
            scene.background = background;
            scene.hotspots = new List<InteractiveHotspotData>
            {
                new InteractiveHotspotData
                {
                    hotspotId = "tech_exit",
                    displayName = "TECH:Exit",
                    normalizedRect = new Rect(0.32f, 0.34f, 0.36f, 0.28f),
                    availabilityConditions = new List<ChoiceCondition>(),
                    requiredCompletedHotspotIds = new List<string>(),
                    oneShot = true,
                    outcome = new InteractiveHotspotOutcome
                    {
                        feedbackText = "TECH: authored-background exit",
                        completeScene = true,
                        stateChanges = new List<InteractiveStateChange>()
                    }
                }
            };
            return scene;
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null.And.Property("interactable").True);
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        private static Texture2D CreatePreviewTexture()
        {
            var texture = new Texture2D(16, 9, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 9];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.08f, 0.16f, 0.24f, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void AssertHotspotsInsideImage(InteractiveSceneController interactive)
        {
            RectTransform image = interactive.DisplayedImageRect;
            Assert.That(image, Is.Not.Null);
            Vector3[] imageCorners = new Vector3[4]; image.GetWorldCorners(imageCorners);
            foreach (string id in new[] { "test_laptop", "test_door", "test_window" })
            {
                RectTransform hotspot = interactive.GetHotspotButton(id).GetComponent<RectTransform>();
                Vector3[] hotspotCorners = new Vector3[4]; hotspot.GetWorldCorners(hotspotCorners);
                foreach (Vector3 corner in hotspotCorners)
                {
                    Assert.That(corner.x, Is.InRange(imageCorners[0].x - .1f, imageCorners[2].x + .1f), id + " left/right drifted outside the displayed image.");
                    Assert.That(corner.y, Is.InRange(imageCorners[0].y - .1f, imageCorners[2].y + .1f), id + " top/bottom drifted outside the displayed image.");
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null) new GameObject("InteractiveHotspotEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        private static IEnumerator WaitFor(System.Func<bool> predicate, string error)
        {
            const float timeout = 15f;
            float started = Time.realtimeSinceStartup;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(timeout), error);
                yield return null;
            }
        }
    }
}
