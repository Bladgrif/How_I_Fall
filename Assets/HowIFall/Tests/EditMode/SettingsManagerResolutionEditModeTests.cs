using System.Collections.Generic;
using NUnit.Framework;

public sealed class SettingsManagerResolutionEditModeTests
{
    [TestCase("1920x1080", SettingsOptionValues.Windowed, 1920, 1080, "1600x900")]
    [TestCase("2560x1440", SettingsOptionValues.Windowed, 2560, 1440, "1920x1080")]
    [TestCase("3840x2160", SettingsOptionValues.Windowed, 3840, 2160, "2560x1440")]
    [TestCase("1280x720", SettingsOptionValues.Windowed, 1920, 1080, "1280x720")]
    [TestCase("1920x1080", SettingsOptionValues.Fullscreen, 1920, 1080, "1920x1080")]
    [TestCase("1920x1080", SettingsOptionValues.Borderless, 1920, 1080, "1920x1080")]
    public void NormalizeResolutionForDisplay_UsesOnlySafeWindowedFallback(
        string requestedResolution,
        string screenMode,
        int displayWidth,
        int displayHeight,
        string expectedResolution)
    {
        Assert.That(
            SettingsManager.NormalizeResolutionForDisplay(requestedResolution, screenMode, displayWidth, displayHeight),
            Is.EqualTo(expectedResolution));
    }

    [Test]
    public void GetSupportedResolutionsForScreenMode_Windowed_ExcludesNativeSizeAndLargerOnFullHdDisplay()
    {
        IReadOnlyList<string> options = SettingsManager.GetSupportedResolutionsForScreenMode(
            SettingsOptionValues.Windowed, 1920, 1080);
        Assert.That(options, Is.EqualTo(new[] { "1280x720", "1600x900" }));
    }

    [Test]
    public void GetSupportedResolutionsForScreenMode_Windowed_IncludesOnlyStrictlyFittingValues()
    {
        IReadOnlyList<string> options = SettingsManager.GetSupportedResolutionsForScreenMode(
            SettingsOptionValues.Windowed, 2560, 1440);
        Assert.That(options, Is.EqualTo(new[] { "1280x720", "1600x900", "1920x1080" }));
    }

    [Test]
    public void GetSupportedResolutionsForScreenMode_FullscreenAndBorderless_KeepFullSupportedList()
    {
        Assert.That(
            SettingsManager.GetSupportedResolutionsForScreenMode(SettingsOptionValues.Fullscreen, 1920, 1080),
            Is.EqualTo(PreferencesOptions.Resolutions));
        Assert.That(
            SettingsManager.GetSupportedResolutionsForScreenMode(SettingsOptionValues.Borderless, 1920, 1080),
            Is.EqualTo(PreferencesOptions.Resolutions));
    }

    [Test]
    public void GetSupportedResolutionsForScreenMode_Windowed_WithoutFittingValue_FallsBackToFullList()
    {
        Assert.That(
            SettingsManager.GetSupportedResolutionsForScreenMode(SettingsOptionValues.Windowed, 640, 400),
            Is.EqualTo(PreferencesOptions.Resolutions));
    }
}
