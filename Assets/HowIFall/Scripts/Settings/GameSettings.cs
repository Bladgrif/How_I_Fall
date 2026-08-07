using System;

[Serializable]
public class GameSettings
{
    public float masterVolume = 0.8f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public float ambientVolume = 0.8f;
    public bool musicDuringPause = false;
    public string screenMode = SettingsOptionValues.Fullscreen;
    public string resolution = "1920x1080";
    public string refreshRate = "60";
    public string gameLook = "Чистый";
    public string interfaceStyle = "Классический";
    public bool rewindVhsFilter = true;
    public bool runInBackground = false;
    public bool characterAnimations = true;
    public bool backgroundAnimations = true;
    public string language = "Русский";
    public string fontSizeMode = "Мелкий";
    public string skipMode = "Виденное";
    public string skipBehavior = SettingsOptionValues.ClassicSkip;
    public float textSpeed = 50f;
    public float autoForwardDelay = 250f;
    public bool skipAfterChoices = false;
    public bool autoForward = false;
    public bool autoSave = true;
    public bool showHints = true;
    public bool fullscreen = true;
}
