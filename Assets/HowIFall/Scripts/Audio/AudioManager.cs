using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = CreateAudioSource("Music Source");
        }

        if (sfxSource == null)
        {
            sfxSource = CreateAudioSource("SFX Source");
        }

        musicSource.loop = true;
        sfxSource.loop = false;
        ApplySettingsVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.Stop();
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    public void ApplySettingsVolume()
    {
        float musicVolume = 1f;
        float sfxVolume = 1f;
        bool musicDuringPause = false;

        if (SettingsManager.Instance != null)
        {
            musicVolume = SettingsManager.Instance.settings.musicVolume;
            sfxVolume = SettingsManager.Instance.settings.sfxVolume;
            musicDuringPause = SettingsManager.Instance.settings.musicDuringPause;
        }

        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(musicVolume);
            musicSource.ignoreListenerPause = musicDuringPause;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(sfxVolume);
        }
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        var sourceGo = new GameObject(sourceName);
        sourceGo.transform.SetParent(transform, false);
        return sourceGo.AddComponent<AudioSource>();
    }
}
