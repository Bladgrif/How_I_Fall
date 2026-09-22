using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace HowIFall.PlayModeTests
{
    /// <summary>
    /// Preferences opened through SettingsPanelController must not depend on a
    /// MainMenuController: gameplay-style hosts stay fully functional with no
    /// action-row suppression or focus-restore side effects.
    /// </summary>
    public sealed class SettingsPanelWithoutMainMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator OpenAndClose_WithoutMainMenuController_RemainsFunctional()
        {
            GameObject managerObject = new GameObject("WithoutMainMenuSettingsManager", typeof(SettingsManager));
            GameObject host = new GameObject("WithoutMainMenuPanelHost", typeof(RectTransform), typeof(Canvas));
            GameObject panelObject = new GameObject("WithoutMainMenuPanel", typeof(RectTransform));
            panelObject.transform.SetParent(host.transform, false);
            GameObject events = new GameObject("WithoutMainMenuEvents", typeof(EventSystem));
            try
            {
                // An earlier test class may still own the loaded MainMenu scene; remove
                // its controller so this fixture truly runs without a Main Menu.
                MainMenuController leftoverMenu = Object.FindFirstObjectByType<MainMenuController>();
                if (leftoverMenu != null)
                {
                    Object.Destroy(leftoverMenu.gameObject);
                    yield return null;
                }

                Assert.That(SettingsManager.Instance, Is.Not.Null, "The fixture requires a SettingsManager.");
                Assert.That(Object.FindFirstObjectByType<MainMenuController>(), Is.Null,
                    "This fixture must run without a Main Menu to prove the independence contract.");

                SettingsPanelController panel = panelObject.AddComponent<SettingsPanelController>();
                yield return null;

                int taglineObjectsBefore = CountTaglineChromeObjects();

                panel.Show();
                yield return null;
                SharedPreferencesView[] visibleViews = Object.FindObjectsByType<SharedPreferencesView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                Assert.That(visibleViews.Length, Is.EqualTo(1), "Exactly the fresh Preferences view must be visible.");
                SharedPreferencesView view = visibleViews[0];
                Assert.That(view.IsVisible, Is.True, "Preferences must open without a Main Menu.");
                Assert.That(panel.SharedController.IsOpen, Is.True);
                Assert.That(view.GetButton("category_0"), Is.Not.Null, "Preferences must stay interactive without a Main Menu.");
                Assert.That(CountTaglineChromeObjects(), Is.EqualTo(taglineObjectsBefore),
                    "Gameplay Preferences must not create or toggle Main Menu tagline chrome.");

                panel.Hide();
                yield return null;
                Assert.That(view.IsVisible, Is.False, "Preferences must close without a Main Menu.");
                Assert.That(panel.SharedController.IsOpen, Is.False);
                Assert.That(CountTaglineChromeObjects(), Is.EqualTo(taglineObjectsBefore),
                    "The Preferences round-trip must leave Main Menu tagline chrome untouched in gameplay hosts.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(events);
                Object.DestroyImmediate(managerObject);
            }
        }

        /// <summary>
        /// Tagline chrome is inert debris when a Main Menu scene from an earlier
        /// fixture is still loaded, so the independence contract counts objects
        /// instead of requiring an empty hierarchy.
        /// </summary>
        private static int CountTaglineChromeObjects()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(transform => transform.name == "Main Menu Tagline" || transform.name == "Main Menu Tagline Dash");
        }
    }
}
