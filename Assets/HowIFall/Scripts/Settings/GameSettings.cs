using System;

[Serializable]
public class GameSettings
{
    public float masterVolume = 0.8f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public float ambientVolume = 0.8f;
    public bool musicDuringPause = false;
    public float textSpeed = 1f;
    public bool fullscreen = true;
}
