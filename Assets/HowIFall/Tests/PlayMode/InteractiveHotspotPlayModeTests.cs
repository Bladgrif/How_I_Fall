using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    [Category("InteractiveHotspot")]
    public sealed class InteractiveHotspotPlayModeTests
    {
        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160), new Vector2Int(1024, 768)
        };

        private bool originalAutoSave;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SetAutoSave(originalAutoSave);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TechnicalRoom_LaptopUnlocksDoor_WindowIsOneShot_AndDoorRestoresNarrative()
        {
            yield return LoadScene("VNPrototype");
            yield return WaitFor(() => VNDialogueController.Instance != null && VNDialogueController.Instance.IsRuntimeReady, "VN runtime did not become ready.");
            VNDialogueController dialogue = VNDialogueController.Instance;
            originalAutoSave = SettingsManager.Instance != null && SettingsManager.Instance.settings != null && SettingsManager.Instance.settings.autoSave;
            SettingsManager.Instance?.SetAutoSave(false);
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

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null.And.Property("interactable").True);
            ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
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
