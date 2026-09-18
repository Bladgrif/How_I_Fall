using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PreferencesControllerResolutionDraftEditModeTests
{
    private const int DisplayWidth = 1920;
    private const int DisplayHeight = 1080;

    private PreferencesController controller;
    private FakeResolutionPreferencesService service;
    private StubPreferencesView view;

    [SetUp]
    public void SetUp()
    {
        service = new FakeResolutionPreferencesService();
        view = new StubPreferencesView();
        controller = new PreferencesController(
            service,
            view,
            displayResolutionProvider: () => new Vector2Int(DisplayWidth, DisplayHeight));
        controller.Initialize();
        controller.Open();
    }

    [TearDown]
    public void TearDown()
    {
        controller.Hide();
    }

    [Test]
    public void SwitchToWindowed_NormalizesDraftImmediatelyBeforeApply()
    {
        Assume.That(service.Source.resolution, Is.EqualTo("1920x1080"));
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        Assert.That(service.Source.resolution, Is.EqualTo("1920x1080"), "Draft normalization must not persist before Apply.");
        Assert.That(view.DisplayedResolution, Is.EqualTo("1600x900"),
            "Switching to Windowed must visibly normalize the native-size draft before Apply.");
        Assert.That(controller.IsDirty, Is.True);
    }

    [Test]
    public void Apply_PersistsExactlyTheDisplayedWindowedDraft()
    {
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        string displayed = view.DisplayedResolution;
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo(displayed), "Apply must persist exactly the displayed resolution.");
        Assert.That(service.Source.resolution, Is.EqualTo("1600x900"));
    }

    [Test]
    public void SetResolution_InWindowed_NormalizesOverflowingValueInsteadOfStagingIt()
    {
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        controller.SetResolution("3840x2160");
        Assert.That(view.DisplayedResolution, Is.EqualTo("1600x900"));
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo("1600x900"));
    }

    [Test]
    public void Windowed1280x720_AppliesExactlyAsDisplayed()
    {
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        controller.SetResolution("1280x720");
        Assert.That(view.DisplayedResolution, Is.EqualTo("1280x720"));
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo("1280x720"));
    }

    [Test]
    public void Fullscreen1920x1080_AppliesExactlyAsDisplayedWithFullList()
    {
        controller.SetResolution("1920x1080");
        Assert.That(view.DisplayedResolution, Is.EqualTo("1920x1080"));
        Assert.That(controller.GetResolutionOptions(SettingsOptionValues.Fullscreen).Count,
            Is.EqualTo(PreferencesOptions.Resolutions.Count));
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo("1920x1080"));
    }

    [Test]
    public void Borderless_KeepsFullListAndAppliesExactSelectedValue()
    {
        controller.SetScreenMode(SettingsOptionValues.Borderless);
        controller.SetResolution("2560x1440");
        Assert.That(view.DisplayedResolution, Is.EqualTo("2560x1440"));
        Assert.That(controller.GetResolutionOptions(SettingsOptionValues.Borderless).Count,
            Is.EqualTo(PreferencesOptions.Resolutions.Count));
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo("2560x1440"));
    }

    [Test]
    public void CycleResolution_InWindowed_StaysWithinFittingValues()
    {
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        controller.SetResolution("1280x720");
        controller.CycleResolution();
        Assert.That(view.DisplayedResolution, Is.EqualTo("1600x900"), "Cycling must skip Windowed values that do not fit.");
        controller.CycleResolution();
        Assert.That(view.DisplayedResolution, Is.EqualTo("1280x720"));
    }

    [Test]
    public void Reopen_ShowsThePersistedAppliedResolution()
    {
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        controller.Apply();
        controller.Close();
        controller.Open();
        Assert.That(view.DisplayedResolution, Is.EqualTo(service.Source.resolution));
        Assert.That(view.DisplayedResolution, Is.EqualTo("1600x900"));
    }

    [Test]
    public void Open_NormalizesLegacyOverflowingWindowedBaseline()
    {
        controller.Close();
        service.Source.screenMode = SettingsOptionValues.Windowed;
        service.Source.resolution = "3840x2160";
        controller.Open();
        Assert.That(view.DisplayedResolution, Is.EqualTo("1600x900"));
        Assert.That(controller.IsDirty, Is.True, "Correcting a legacy overflowing baseline must offer Apply.");
        controller.Apply();
        Assert.That(service.Source.resolution, Is.EqualTo("1600x900"));
    }

    [Test]
    public void Reset_DraftOnly_RestoresDefaultsAndLeavesPersistedValue()
    {
        controller.SetResolution("1600x900");
        controller.Apply();
        controller.SetScreenMode(SettingsOptionValues.Windowed);
        controller.Reset();
        Assert.That(view.DisplayedResolution, Is.EqualTo(new GameSettings().resolution));
        Assert.That(service.Source.resolution, Is.EqualTo("1600x900"), "Reset must stay draft-only.");
    }

    private sealed class StubPreferencesView : IPreferencesView
    {
        public string DisplayedResolution { get; private set; } = "—";
        public string DisplayedScreenMode { get; private set; } = "—";

        public void Bind(PreferencesController sharedController) { }
        public void SetVisible(bool visible) { }

        public void Refresh(PreferencesState settings)
        {
            DisplayedResolution = settings.resolution;
            DisplayedScreenMode = settings.screenMode;
        }
    }

    private sealed class FakeResolutionPreferencesService : IPreferencesService
    {
        public GameSettings Source { get; private set; } = new GameSettings();
        public PreferencesState Current => new PreferencesState(Source);
        public bool IsAvailable => true;

        public void Reset() => Source = new GameSettings();
        public void SetMasterVolume(float value) { Source.masterVolume = value; }
        public void SetMusicVolume(float value) { Source.musicVolume = value; }
        public void SetSfxVolume(float value) { Source.sfxVolume = value; }
        public void SetMuteAll(bool value) { Source.muteAll = value; }
        public void SetScreenMode(string value) { Source.screenMode = value; }
        public void SetResolution(string value) { Source.resolution = value; }
        public void SetRunInBackground(bool value) { Source.runInBackground = value; }
        public void SetSkipMode(string value) { Source.skipMode = value; }
        public void SetSkipBehavior(string value) { Source.skipBehavior = value; }
        public void SetTextSpeed(float value) { Source.textSpeed = value; }
        public void SetDialogueTextScale(float value) { Source.dialogueTextScale = value; }
        public void SetTextboxOpacity(float value) { Source.textboxOpacity = value; }
        public void SetAutoForwardDelay(float value) { Source.autoForwardDelay = value; }
        public void SetSkipAfterChoices(bool value) { Source.skipAfterChoices = value; }
        public void SetAutoForward(bool value) { Source.autoForward = value; }
        public void SetAutoSave(bool value) { Source.autoSave = value; }
        public void SetShowQuickMenu(bool value) { Source.showQuickMenu = value; }
    }
}
