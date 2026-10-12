using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression for the disabled Apply defect: the footer ColorTint only reaches the
/// plate, so after Apply the label kept its near-white enabled text and the button
/// still read as available.
/// </summary>
public class PreferencesApplyDisabledPresentationEditModeTests
{
    [Test]
    public void ApplyLabel_DimsWhenApplied_RestoresWhenDraftIsDirty()
    {
        GameObject host = new GameObject("ApplyDisabledPresentationHost", typeof(RectTransform));
        try
        {
            SharedPreferencesView view = SharedPreferencesView.Create(host.transform, "Test");
            var controller = new PreferencesController(new FakePreferencesService(), view);
            controller.Initialize();
            controller.Open();

            Button apply = view.GetButton("apply");
            TextMeshProUGUI applyLabel = apply.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.That(apply, Is.Not.Null, "Apply footer button must exist.");
            Assert.That(applyLabel, Is.Not.Null, "Apply footer button must expose its label.");

            Assert.That(apply.interactable, Is.False, "A freshly opened draft must start applied/disabled.");
            Color disabledColor = applyLabel.color;
            Assert.That(disabledColor.a, Is.LessThan(0.9f), "Disabled Apply label must lose the enabled near-white alpha.");
            Assert.That(Mathf.Max(disabledColor.r, Mathf.Max(disabledColor.g, disabledColor.b)), Is.LessThan(0.6f),
                "Disabled Apply label must be visibly dimmed instead of staying bright.");

            controller.SetMasterVolume(0.42f);
            Assert.That(apply.interactable, Is.True, "A changed draft must enable Apply.");
            Color enabledColor = applyLabel.color;
            Assert.That(enabledColor.r > 0.9f && enabledColor.a > 0.9f, Is.True,
                "Dirty Apply label must return to the bright enabled text.");

            controller.Apply();
            Assert.That(apply.interactable, Is.False, "Applied draft must disable Apply again.");
            Assert.That(applyLabel.color, Is.EqualTo(disabledColor),
                "Apply label must re-enter the same dimmed presentation after Apply.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Target11_ReadableValuesAndDecorations_PreserveControlSurface()
    {
        GameObject host = new GameObject("PreferencesPresentationHost", typeof(RectTransform));
        try
        {
            SharedPreferencesView view = SharedPreferencesView.Create(host.transform, "Test");
            var controller = new PreferencesController(new FakePreferencesService(), view);
            controller.Initialize(); controller.Open();
            Assert.That(SharedPreferencesView.VisibleControlIds.Count, Is.EqualTo(13));
            foreach (string id in SharedPreferencesView.VisibleControlIds) Assert.That(view.HasControl(id), Is.True, id);
            Assert.That(view.GetDropdown(SharedPreferencesView.ScreenModeId).captionText.fontSize, Is.GreaterThanOrEqualTo(22f));
            Assert.That(view.GetDropdown(SharedPreferencesView.ScreenModeId).itemText.fontSize, Is.GreaterThanOrEqualTo(20f));
            Assert.That(view.GetToggle(SharedPreferencesView.SkipUnseenId).GetComponentInChildren<TextMeshProUGUI>().fontSize,
                Is.GreaterThanOrEqualTo(20f));
            Assert.That(view.GetButton("apply").GetComponentInChildren<TextMeshProUGUI>().fontSize, Is.GreaterThanOrEqualTo(18f));

            int separators = 0;
            foreach (Image image in view.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "Row Separator") continue;
                separators++;
                Assert.That(image.raycastTarget, Is.False, "Decorative separators must not intercept pointer input.");
                Assert.That(image.GetComponent<LayoutElement>().ignoreLayout, Is.True, "Separators must not resize control rows.");
            }
            Assert.That(separators, Is.EqualTo(13));
            view.SelectCategory(1);
            for (int i = 0; i < 4; i++)
            {
                Transform marker = view.GetButton("category_" + i).transform.Find("Active Category Line");
                Assert.That(marker.gameObject.activeSelf, Is.EqualTo(i == 1));
                Assert.That(marker.GetComponent<Image>().raycastTarget, Is.False);
            }
            Assert.That(controller.IsDirty, Is.False, "Presentation and category selection must not edit draft settings.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        public GameSettings Source { get; private set; } = new GameSettings();
        public PreferencesState Current => new PreferencesState(Source);
        public bool IsAvailable => true;

        public void Reset() => Source = new GameSettings();
        public void SetMasterVolume(float value) => Source.masterVolume = value;
        public void SetMusicVolume(float value) => Source.musicVolume = value;
        public void SetSfxVolume(float value) => Source.sfxVolume = value;
        public void SetMuteAll(bool value) => Source.muteAll = value;
        public void SetScreenMode(string value) => Source.screenMode = value;
        public void SetResolution(string value) => Source.resolution = value;
        public void SetRunInBackground(bool value) => Source.runInBackground = value;
        public void SetSkipMode(string value) => Source.skipMode = value;
        public void SetSkipBehavior(string value) => Source.skipBehavior = value;
        public void SetTextSpeed(float value) => Source.textSpeed = value;
        public void SetDialogueTextScale(float value) => Source.dialogueTextScale = value;
        public void SetTextboxOpacity(float value) => Source.textboxOpacity = value;
        public void SetAutoForwardDelay(float value) => Source.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => Source.skipAfterChoices = value;
        public void SetAutoForward(bool value) => Source.autoForward = value;
        public void SetAutoSave(bool value) => Source.autoSave = value;
        public void SetShowQuickMenu(bool value) => Source.showQuickMenu = value;
    }
}
