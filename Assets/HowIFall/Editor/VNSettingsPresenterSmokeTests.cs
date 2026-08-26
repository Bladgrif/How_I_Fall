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
            var service = new FakePreferencesService();
            string toast = string.Empty;
            int closeCount = 0;
            var view = new VNPreferencesAdapter(
                overlay,
                panel,
                null, null, null, null, null, null, null, null, null);
            var controller = new PreferencesController(
                service,
                view,
                value => toast = value,
                () => closeCount++);

            controller.Initialize();
            Require(!overlay.activeSelf && !panel.activeSelf && !view.SharedView.IsVisible, "Initialize did not hide the Preferences UI.");

            controller.Open();
            Require(!overlay.activeSelf && !panel.activeSelf && view.SharedView.IsVisible, "Open did not use only the shared Preferences UI.");
            Require(Mathf.Approximately(view.SharedView.GetSlider(SharedPreferencesView.MasterVolumeId).value, service.Source.masterVolume), "Master volume was not refreshed.");
            Require(Mathf.Approximately(view.SharedView.GetSlider(SharedPreferencesView.TextSpeedId).value, service.Source.textSpeed), "Text speed was not refreshed.");
            Require(Mathf.Approximately(view.SharedView.GetSlider(SharedPreferencesView.AutoForwardDelayId).value, 2.5f), "Auto-forward delay was not converted to seconds.");

            view.SharedView.GetSlider(SharedPreferencesView.MasterVolumeId).value = 0.35f;
            view.SharedView.GetSlider(SharedPreferencesView.AutoForwardDelayId).value = 4f;
            view.SharedView.GetToggle(SharedPreferencesView.SkipUnseenId).isOn = true;
            Require(Mathf.Approximately(service.Source.masterVolume, 0.35f), "Master volume change was not forwarded.");
            Require(Mathf.Approximately(service.Source.autoForwardDelay, 400f), "Auto-forward delay change was not forwarded.");
            Require(service.Source.skipMode == "Всё", "Skip unseen toggle was not forwarded truthfully.");

            view.SharedView.GetButton("reset").onClick.Invoke();
            Require(service.ResetCount == 1, "Reset was not forwarded.");
            Require(toast == "Настройки сброшены", "Reset toast was not shown.");
            Require(Mathf.Approximately(view.SharedView.GetSlider(SharedPreferencesView.MasterVolumeId).value, service.Source.masterVolume), "Reset did not refresh the open view.");

            view.SharedView.GetButton("back").onClick.Invoke();
            Require(!overlay.activeSelf && !panel.activeSelf && !view.SharedView.IsVisible, "Close did not hide the Preferences UI.");
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
        public void SetMuteAll(bool value) => Source.muteAll = value;
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
        public void SetDialogueTextScale(float value) => Source.dialogueTextScale = value;
        public void SetTextboxOpacity(float value) => Source.textboxOpacity = value;
        public void SetAutoForwardDelay(float value) => Source.autoForwardDelay = value;
        public void SetSkipAfterChoices(bool value) => Source.skipAfterChoices = value;
        public void SetAutoForward(bool value) => Source.autoForward = value;
        public void SetAutoSave(bool value) => Source.autoSave = value;
        public void SetShowQuickMenu(bool value) => Source.showQuickMenu = value;

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
