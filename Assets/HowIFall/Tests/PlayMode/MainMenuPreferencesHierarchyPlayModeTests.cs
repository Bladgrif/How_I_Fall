using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    /// <summary>
    /// Regression coverage for the Preferences/Main Menu hierarchy correction: the
    /// translucent Preferences surface must open without the five Main Menu action
    /// rows remaining readable underneath it, and every row, focus and availability
    /// state must survive the round-trip unchanged.
    /// </summary>
    public sealed class MainMenuPreferencesHierarchyPlayModeTests
    {
        private const string MainMenuSceneName = "MainMenu";
        private string temporarySaveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            temporarySaveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_MainMenuPrefsHierarchy_" + System.Guid.NewGuid().ToString("N"));
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            yield return WaitFor(() => Object.FindFirstObjectByType<MainMenuController>() != null
                && EventSystem.current != null && SaveManager.Instance != null && SettingsManager.Instance != null,
                "Main Menu Preferences hierarchy fixture did not become ready.");

            SaveManager.Instance.ConfigureSaveDirectoryForTests(temporarySaveDirectory);
            Object.FindFirstObjectByType<MainMenuController>().RefreshContinueAvailability();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Directory.Exists(temporarySaveDirectory))
            {
                Directory.Delete(temporarySaveDirectory, true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenAndApply_PreferencesHidesActionRows_KeepsSurfaceAndBackgroundAlive()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Button[] actions = menu.PlayerFacingActionButtons.ToArray();
            GameObject[] rows = actions.Select(button => button.transform.parent.gameObject).ToArray();
            (GameObject tagline, GameObject dash) = FindTargetV1TaglineBlock(rows[0].transform.parent);

            Assert.That(actions.Length, Is.EqualTo(5), "Main Menu must present the five player-facing actions.");
            Assert.That(rows.All(row => row.activeSelf), Is.True, "All five action rows must be visible before Preferences.");
            Assert.That(tagline != null && dash != null, Is.True, "The target v1 tagline block must exist in the Main Menu scene.");
            Assert.That(tagline.activeSelf && dash.activeSelf, Is.True,
                "Tagline and dash must be visible before Preferences, next to the action rows.");
            Assert.That(actions[0].interactable, Is.False, "Continue must start disabled on the empty save directory.");

            menu.OpenSettings();
            yield return null;

            SharedPreferencesView view = FindSharedView();
            Assert.That(view, Is.Not.Null, "Preferences view must exist in the Main Menu scene.");
            Assert.That(rows.All(row => !row.activeSelf), Is.True, "Opening Preferences must hide all five action rows.");
            Assert.That(!tagline.activeSelf && !dash.activeSelf, Is.True,
                "Opening Preferences must hide the tagline and dash so no Main Menu typography ghosts through the surface.");
            Assert.That(actions.All(button => !button.isActiveAndEnabled), Is.True, "Hidden rows must deactivate their buttons.");
            Assert.That(view.IsVisible, Is.True, "Preferences must remain visible and functional over the Main Menu.");

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.That(canvas != null && canvas.gameObject.activeInHierarchy, Is.True, "The Main Menu canvas must stay enabled.");
            Transform background = canvas.transform.Find("Background");
            Assert.That(background != null && background.gameObject.activeInHierarchy, Is.True,
                "Background art must stay visible under the translucent Preferences surface.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.GetButton("category_0").gameObject),
                "Focus must move into Preferences instead of staying on a hidden menu row.");

            Slider volumeSlider = view.GetSlider(SharedPreferencesView.MasterVolumeId);
            // Draft a value guaranteed to differ from whatever the committed baseline is;
            // earlier tests in the suite may have persisted any volume as the baseline.
            volumeSlider.value = volumeSlider.value >= 0.5f ? 0.2f : 0.8f;
            Assert.That(menu.settingsPanel.SharedController.IsDirty, Is.True, "Drafting a change must keep Preferences interactive.");
            view.GetButton("apply").onClick.Invoke();
            yield return null;

            Assert.That(view.IsVisible, Is.True, "Apply must keep Preferences open.");
            Assert.That(view.GetButton("apply").interactable, Is.False, "Clean Apply must disable the Apply button.");
            Assert.That(rows.All(row => !row.activeSelf), Is.True, "Action rows must stay hidden while Preferences remains open after Apply.");
            Assert.That(!tagline.activeSelf && !dash.activeSelf, Is.True,
                "Tagline and dash must stay hidden while Preferences remains open after Apply.");

            view.GetButton("back").onClick.Invoke();
            yield return null;

            Assert.That(view.IsVisible, Is.False, "Back must close Preferences.");
            Assert.That(rows.All(row => row.activeSelf), Is.True, "Closing Preferences must restore all five action rows.");
            Assert.That(tagline.activeSelf && dash.activeSelf, Is.True,
                "Closing Preferences must restore the tagline and dash together with the rows.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(actions[3].gameObject),
                "Closing Preferences must restore Settings focus through the existing contract.");
            Assert.That(actions[0].interactable, Is.False, "Continue disabled state must survive the Preferences round-trip.");
        }

        [UnityTest]
        public IEnumerator RepeatedCycles_PreserveRowsFocusAndActionStates()
        {
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Button[] actions = menu.PlayerFacingActionButtons.ToArray();
            GameObject[] rows = actions.Select(button => button.transform.parent.gameObject).ToArray();
            (GameObject tagline, GameObject dash) = FindTargetV1TaglineBlock(rows[0].transform.parent);
            float committedMasterVolume = SettingsManager.Instance.settings.masterVolume;
            bool originalContinueState = actions[0].interactable;

            try
            {
                // Back/discard semantics: a drafted change must not leak into a reopened Preferences.
                menu.OpenSettings();
                yield return null;
                SharedPreferencesView view = FindSharedView();
                Assert.That(rows.All(row => !row.activeSelf), Is.True, "Opening Preferences must hide the rows.");
                Slider draftSlider = view.GetSlider(SharedPreferencesView.MasterVolumeId);
                draftSlider.value = draftSlider.value >= 0.5f ? 0.91f : 0.1f;
                view.GetButton("back").onClick.Invoke();
                yield return null;

                menu.OpenSettings();
                yield return null;
                view = FindSharedView();
                Assert.That(view.GetSlider(SharedPreferencesView.MasterVolumeId).value, Is.EqualTo(committedMasterVolume),
                    "Back must discard the draft; reopening shows the committed value.");
                Assert.That(rows.All(row => !row.activeSelf), Is.True, "Reopening Preferences must hide the rows again.");

                // Continue availability is preserved in the enabled direction too.
                actions[0].interactable = true;
                view.GetButton("back").onClick.Invoke();
                yield return null;
                Assert.That(rows.All(row => row.activeSelf), Is.True, "Close must restore the rows.");
                Assert.That(actions[0].interactable, Is.True, "Continue enabled state must survive the Preferences round-trip.");

                // Repeated open/close cycles must not lose or duplicate rows,
                // and the tagline block must track the same lifecycle.
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    menu.OpenSettings();
                    yield return null;
                    Assert.That(FindSharedView().IsVisible, Is.True, "Preferences must reopen on cycle " + cycle + ".");
                    Assert.That(rows.All(row => !row.activeSelf), Is.True, "Rows must be hidden on every open, cycle " + cycle + ".");
                    Assert.That(!tagline.activeSelf && !dash.activeSelf, Is.True,
                        "Tagline and dash must be hidden on every open, cycle " + cycle + ".");

                    FindSharedView().GetButton("back").onClick.Invoke();
                    yield return null;
                    Assert.That(rows.All(row => row.activeSelf), Is.True, "Rows must be restored on every close, cycle " + cycle + ".");
                    Assert.That(tagline.activeSelf && dash.activeSelf, Is.True,
                        "Tagline and dash must be restored on every close, cycle " + cycle + ".");
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(actions[3].gameObject),
                        "Settings focus must be restored on every close, cycle " + cycle + ".");
                }

                Assert.That(menu.PlayerFacingActionButtons.Count, Is.EqualTo(5), "Cycles must not lose any player-facing action.");
                Assert.That(actions.All(button => button.isActiveAndEnabled), Is.True, "All five buttons must be active again after the cycles.");
            }
            finally
            {
                actions[0].interactable = originalContinueState;
                menu.RefreshContinueAvailability();
            }
        }

        private static IEnumerator WaitFor(System.Func<bool> predicate, string failureMessage)
        {
            const float timeoutSeconds = 10f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail(failureMessage);
                }

                yield return null;
            }
        }

        private static SharedPreferencesView FindSharedView()
        {
            return Object.FindFirstObjectByType<SharedPreferencesView>();
        }

        private static (GameObject tagline, GameObject dash) FindTargetV1TaglineBlock(Transform menuContent)
        {
            Transform tagline = menuContent != null ? menuContent.Find("Main Menu Tagline") : null;
            Transform dash = menuContent != null ? menuContent.Find("Main Menu Tagline Dash") : null;
            return (tagline != null ? tagline.gameObject : null, dash != null ? dash.gameObject : null);
        }
    }
}
