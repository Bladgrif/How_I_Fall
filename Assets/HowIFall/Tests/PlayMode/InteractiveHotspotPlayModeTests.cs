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
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.False, "Completion must release the suppressed dialogue shell.");
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
            Assert.That(dialogue.IsDialogueShellSuppressed, Is.True, "A backdrop click must not release the suppressed dialogue shell.");
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
