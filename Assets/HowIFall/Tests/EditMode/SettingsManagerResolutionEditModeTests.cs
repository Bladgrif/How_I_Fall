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
}
