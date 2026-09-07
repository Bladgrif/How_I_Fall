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
    public sealed class MainMenuFocusPlayModeTests
    {
        private const string MainMenuSceneName = "MainMenu";
        private string temporarySaveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            temporarySaveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_MainMenuFocus_" + System.Guid.NewGuid().ToString("N"));
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            yield return WaitFor(() => FindMainMenuController() != null && EventSystem.current != null && SaveManager.Instance != null,
                "Main Menu focus fixture did not become ready.");

            SaveManager.Instance.ConfigureSaveDirectoryForTests(temporarySaveDirectory);
            FindMainMenuController().RefreshContinueAvailability();
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
        public IEnumerator MainMenuFocusAndCloseOrCancel_UsesSafeModalAndRootContracts()
        {
            MainMenuController menu = FindMainMenuController();
            Button continueButton = menu.PlayerFacingActionButtons[0];
            Button newGameButton = menu.PlayerFacingActionButtons[1];
            Button quitButton = menu.PlayerFacingActionButtons[4];

            CollectionAssert.AreEqual(
                new[] { "Продолжить", "Новая игра", "Загрузить", "Настройки", "Выйти" },
                menu.PlayerFacingActionButtons.Select(GetButtonLabel),
                "Main Menu must expose only the final five player-facing actions in order.");

            continueButton.interactable = true;
            menu.FocusDefaultAction();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(continueButton.gameObject),
                "Available Continue must receive initial Main Menu focus.");

            continueButton.interactable = false;
            menu.FocusDefaultAction();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(newGameButton.gameObject),
                "Unavailable Continue must fall back to New Game focus.");

            EventSystem.current.SetSelectedGameObject(quitButton.gameObject);
            menu.OpenExitConfirm();
            yield return null;
            GameObject exitPanel = FindSceneObject("Exit Confirm Panel");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(FindButtonWithRoute(menu, nameof(MainMenuController.CloseExitConfirm)).gameObject),
                "Exit Confirmation must focus Cancel instead of the destructive action.");
            Assert.That(menu.TryHandleCloseOrCancel(), Is.True);
            Assert.That(exitPanel.activeSelf, Is.False, "Exit Esc/Cancel must close the confirmation without exiting.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(quitButton.gameObject),
                "Closing Exit Confirmation must restore the opener focus.");

            EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
            Assert.That(menu.TryHandleCloseOrCancel(), Is.False, "Root Main Menu Esc/Cancel must not invoke an action.");
            Assert.That(exitPanel.activeSelf, Is.False, "Root Main Menu Esc/Cancel must not open or confirm Exit.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(newGameButton.gameObject),
                "Root Main Menu Esc/Cancel must preserve the existing safe focus.");
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

        private static MainMenuController FindMainMenuController()
        {
            return Object.FindFirstObjectByType<MainMenuController>();
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == name)
                {
                    return transform.gameObject;
                }
            }

            Assert.Fail("Scene object was not found: " + name);
            return null;
        }

        private static Button FindButtonWithRoute(MainMenuController menu, string methodName)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                {
                    if (button.onClick.GetPersistentTarget(index) == menu
                        && button.onClick.GetPersistentMethodName(index) == methodName)
                    {
                        return button;
                    }
                }
            }

            Assert.Fail("Main Menu button route was not found: " + methodName);
            return null;
        }

        private static string GetButtonLabel(Button button)
        {
            TMPro.TextMeshProUGUI label = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            return label != null ? label.text : legacyLabel != null ? legacyLabel.text : string.Empty;
        }
    }
}
