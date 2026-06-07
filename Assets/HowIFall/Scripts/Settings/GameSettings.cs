using System;

[Serializable]
public class GameSettings
{
    public float masterVolume = 0.8f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public float ambientVolume = 0.8f;
    public bool musicDuringPause = false;
    public string screenMode = "Полный экран";
    public string resolution = "1920x1080";
    public string refreshRate = "60";
    public string gameLook = "Чистый";
    public string interfaceStyle = "Классический";
    public bool rewindVhsFilter = true;
    public bool runInBackground = false;
    public bool characterAnimations = true;
    public bool backgroundAnimations = true;
    public float textSpeed = 1f;
    public bool fullscreen = true;
}
