using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class VNSettingsPresenterSmokeTests
{
    [MenuItem("How I Fall/Tests/Run VN Settings Presenter Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall VN settings presenter smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall VN settings presenter smoke tests passed.");
    }

    private static void Run()
    {
        GameObject root = new GameObject("VNSettingsPresenterTests");

        try
        {
            GameObject overlay = CreateChild(root, "Overlay");
            GameObject panel = CreateChild(root, "Panel");
            Slider master = CreateComponent<Slider>(root, "Master");
            Slider music = CreateComponent<Slider>(root, "Music");
            Slider sfx = CreateComponent<Slider>(root, "Sfx");
            Slider textSpeed = CreateComponent<Slider>(root, "TextSpeed");
            Toggle fullscreen = CreateComponent<Toggle>(root, "Fullscreen");
            Button close = CreateComponent<Button>(root, "Close");
            Button reset = CreateComponent<Button>(root, "Reset");

            var service = new FakeSettingsService();
            string toast = string.Empty;
            var presenter = new VNSettingsPresenter(
                overlay,
                panel,
                master,
                music,
                sfx,
                textSpeed,
                fullscreen,
                close,
                reset,
                service,
                value => toast = value,
                null);

            presenter.Initialize();
            Require(!overlay.activeSelf && !panel.activeSelf, "Initialize did not hide the settings UI.");

            presenter.Open();
            Require(overlay.activeSelf && panel.activeSelf, "Open did not show the settings UI.");
            Require(Mathf.Approximately(master.value, service.Settings.masterVolume), "Master volume was not refreshed.");
            Require(Mathf.Approximately(textSpeed.value, service.Settings.textSpeed), "Text speed was not refreshed.");
            Require(fullscreen.isOn == service.Settings.fullscreen, "Fullscreen toggle was not refreshed.");

            master.value = 0.35f;
            fullscreen.isOn = false;
            Require(Mathf.Approximately(service.LastMasterVolume, 0.35f), "Master volume change was not forwarded.");
            Require(!service.LastFullscreen, "Fullscreen change was not forwarded.");

            reset.onClick.Invoke();
            Require(service.ResetCount == 1, "Reset was not forwarded.");
            Require(toast == "Настройки сброшены", "Reset toast was not shown.");

            close.onClick.Invoke();
            Require(!overlay.activeSelf && !panel.activeSelf, "Close did not hide the settings UI.");
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

    private sealed class FakeSettingsService : IVNSettingsService
    {
        public GameSettings Settings { get; } = new GameSettings
        {
            masterVolume = 0.7f,
            musicVolume = 0.6f,
            sfxVolume = 0.5f,
            textSpeed = 0.8f,
            fullscreen = true
        };

        public bool IsAvailable => true;
        public int ResetCount { get; private set; }
        public float LastMasterVolume { get; private set; }
        public bool LastFullscreen { get; private set; } = true;

        public void Reset()
        {
            ResetCount++;
        }

        public void SetMasterVolume(float value)
        {
            LastMasterVolume = value;
        }

        public void SetMusicVolume(float value)
        {
        }

        public void SetSfxVolume(float value)
        {
        }

        public void SetTextSpeed(float value)
        {
        }

        public void SetFullscreen(bool value)
        {
            LastFullscreen = value;
        }
    }
}
