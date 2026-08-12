using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VNSettingsPresenterSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Shared Preferences Presenter Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall shared Preferences presenter smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall shared Preferences presenter smoke tests passed.");
    }

    private static void Run()
    {
        GameObject root = new GameObject("SharedPreferencesPresenterTests");

        try
        {
            GameObject overlay = CreateChild(root, "Overlay");
            GameObject panel = CreateChild(root, "Panel");
            Slider master = CreateComponent<Slider>(root, "Master");
            Slider music = CreateComponent<Slider>(root, "Music");
            Slider sfx = CreateComponent<Slider>(root, "Sfx");
            Slider textSpeed = CreateComponent<Slider>(root, "TextSpeed");
            textSpeed.minValue = 20f;
            textSpeed.maxValue = 100f;
            Toggle autoForward = CreateComponent<Toggle>(root, "AutoForward");
            Slider autoForwardDelay = CreateComponent<Slider>(root, "AutoForwardDelay");
            autoForwardDelay.minValue = 50f;
            autoForwardDelay.maxValue = 500f;
            autoForwardDelay.wholeNumbers = true;
            Toggle fullscreen = CreateComponent<Toggle>(root, "Fullscreen");
            Button close = CreateComponent<Button>(root, "Close");
            Button reset = CreateComponent<Button>(root, "Reset");

            var service = new FakePreferencesService();
            string toast = string.Empty;
            int closeCount = 0;
            var view = new VNPreferencesAdapter(
                overlay,
                panel,
                master,
                music,
                sfx,
                textSpeed,
                autoForward,
                autoForwardDelay,
                fullscreen,
                close,
                reset);
            var controller = new PreferencesController(
                service,
                view,
                value => toast = value,
                () => closeCount++);

            controller.Initialize();
            Require(!overlay.activeSelf && !panel.activeSelf, "Initialize did not hide the Preferences UI.");

            controller.Open();
            Require(overlay.activeSelf && panel.activeSelf, "Open did not show the Preferences UI.");
            Require(Mathf.Approximately(master.value, service.Source.masterVolume), "Master volume was not refreshed.");
            Require(Mathf.Approximately(textSpeed.value, service.Source.textSpeed), "Text speed was not refreshed.");
            Require(autoForward.isOn == service.Source.autoForward, "Auto-forward state was not refreshed.");
            Require(Mathf.Approximately(autoForwardDelay.value, service.Source.autoForwardDelay), "Auto-forward delay was not refreshed.");
            Require(fullscreen.isOn == SettingsManager.IsFullscreenScreenMode(service.Source.screenMode), "Fullscreen compatibility toggle was not derived from screenMode.");


            master.value = 0.35f;
            autoForward.isOn = true;
            autoForwardDelay.value = 400f;
            fullscreen.isOn = false;
            Require(Mathf.Approximately(service.Source.masterVolume, 0.35f), "Master volume change was not forwarded.");
            Require(service.Source.screenMode == SettingsOptionValues.Windowed, "Compact fullscreen toggle did not update canonical screenMode.");
            Require(service.Source.autoForward, "Auto-forward change was not forwarded.");
            Require(Mathf.Approximately(service.Source.autoForwardDelay, 400f), "Auto-forward delay change was not forwarded.");

            reset.onClick.Invoke();
            Require(service.ResetCount == 1, "Reset was not forwarded.");
            Require(toast == "Настройки сброшены", "Reset toast was not shown.");
            Require(Mathf.Approximately(master.value, service.Source.masterVolume), "Reset did not refresh the open view.");

            close.onClick.Invoke();
            Require(!overlay.activeSelf && !panel.activeSelf, "Close did not hide the Preferences UI.");
            Require(closeCount == 1, "Gameplay close context callback was not invoked once.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateChild(GameObject root, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform);
        return child;
    }

    private static T CreateComponent<T>(GameObject root, string name)
        where T : Component
    {
        return CreateChild(root, name).AddComponent<T>();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        public GameSettings Source { get; private set; } = CreateNonDefaultSettings();
        public PreferencesState Current => new PreferencesState(Source);
        public bool IsAvailable => true;
        public int ResetCount { get; private set; }

        public void Reset()
        {
            ResetCount++;
            Source = new GameSettings();
        }

        public void SetMasterVolume(float value) => Source.masterVolume = value;
        public void SetMusicVolume(float value) => Source.musicVolume = value;
        public void SetSfxVolume(float value) => Source.sfxVolume = value;
        public void SetScreenMode(string value)
        {
            Source.screenMode = value;
            Source.fullscreen = SettingsManager.IsFullscreenScreenMode(value);
        }

        public void SetResolution(string value) => Source.resolution = value;
        public void SetRunInBackground(bool value) => Source.runInBackground = value;
        public void SetSkipMode(string value) => Source.skipMode = value;
        public void SetSkipBehavior(string value) => Source.skipBehavior = value;
        public void SetTextSpeed(float value) => Source.textSpeed = value;
        public void SetAutoForwardDelay(float value) => Source.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => Source.skipAfterChoices = value;
        public void SetAutoForward(bool value) => Source.autoForward = value;
        public void SetAutoSave(bool value) => Source.autoSave = value;

        private static GameSettings CreateNonDefaultSettings()
        {
            return new GameSettings
            {
                masterVolume = 0.7f,
                musicVolume = 0.6f,
                sfxVolume = 0.5f,
                textSpeed = 80f,
                autoForward = false,
                autoForwardDelay = 250f,
                screenMode = SettingsOptionValues.Borderless,
                fullscreen = false
            };
        }
    }
}
