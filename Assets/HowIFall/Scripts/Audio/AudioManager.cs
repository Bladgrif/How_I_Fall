using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public const float DefaultAmbienceFadeSeconds = 1.25f;

    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource ambienceSourceA;
    public AudioSource ambienceSourceB;

    public float AmbienceGainA => ambienceGainA;
    public float AmbienceGainB => ambienceGainB;
    public AudioClip CurrentAmbienceClip => activeAmbienceSource != null ? activeAmbienceSource.clip : null;
    public bool IsAmbiencePlaying => activeAmbienceSource != null && activeAmbienceSource.isPlaying;
    public bool IsAmbienceTransitionActive => ambienceTransitionCoroutine != null;

    private float ambienceGainA;
    private float ambienceGainB;
    private AudioSource activeAmbienceSource;
    private Coroutine ambienceTransitionCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
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

    public void PlayAmbience(AudioClip clip, float fadeSeconds = DefaultAmbienceFadeSeconds)
    {
        if (clip == null || ambienceSourceA == null || ambienceSourceB == null)
        {
            return;
        }

        if (activeAmbienceSource != null && activeAmbienceSource.clip == clip)
        {
            ApplyAmbienceVolumes();
            return;
        }

        CancelAmbienceTransition();
        AudioSource outgoingSource = CollapseToDominantAmbienceSource();
        AudioSource incomingSource = outgoingSource == ambienceSourceA ? ambienceSourceB : ambienceSourceA;

        StopAndClearAmbienceSource(incomingSource);
        incomingSource.clip = clip;
        SetAmbienceGain(incomingSource, 0f);
        incomingSource.Play();
        activeAmbienceSource = incomingSource;

        if (fadeSeconds <= 0f)
        {
            StopAndClearAmbienceSource(outgoingSource);
            SetAmbienceGain(incomingSource, 1f);
            ApplyAmbienceVolumes();
            return;
        }

        ambienceTransitionCoroutine = StartCoroutine(CrossfadeAmbience(outgoingSource, incomingSource, fadeSeconds));
    }

    public void StopAmbience(float fadeSeconds = DefaultAmbienceFadeSeconds)
    {
        CancelAmbienceTransition();
        AudioSource outgoingSource = CollapseToDominantAmbienceSource();

        if (outgoingSource == null)
        {
            return;
        }

        activeAmbienceSource = null;
        if (fadeSeconds <= 0f)
        {
            StopAndClearAmbienceSource(outgoingSource);
            ApplyAmbienceVolumes();
            return;
        }

        ambienceTransitionCoroutine = StartCoroutine(FadeOutAmbience(outgoingSource, fadeSeconds));
    }

    public void RestorePlaybackStateAfterReplay(
        AudioClip musicClip,
        bool musicWasPlaying,
        AudioClip ambienceClip,
        bool ambienceWasPlaying)
    {
        StopMusic();
        if (musicSource != null)
        {
            musicSource.clip = musicClip;
            if (musicWasPlaying && musicClip != null)
            {
                musicSource.Play();
            }
        }

        CancelAmbienceTransition();
        StopAndClearAmbienceSource(ambienceSourceA);
        StopAndClearAmbienceSource(ambienceSourceB);
        if (ambienceClip != null && ambienceSourceA != null)
        {
            ambienceSourceA.clip = ambienceClip;
            activeAmbienceSource = ambienceSourceA;
            SetAmbienceGain(ambienceSourceA, 1f);
            if (ambienceWasPlaying)
            {
                ambienceSourceA.Play();
            }
        }

        ApplySettingsVolume();
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

        ApplyAmbienceVolumes();
    }

    public void ApplyAmbienceVolumes()
    {
        float ambientVolume = SettingsManager.Instance != null
            ? SettingsManager.Instance.settings.ambientVolume
            : 1f;

        if (ambienceSourceA != null)
        {
            ambienceSourceA.volume = Mathf.Clamp01(ambientVolume) * ambienceGainA;
        }

        if (ambienceSourceB != null)
        {
            ambienceSourceB.volume = Mathf.Clamp01(ambientVolume) * ambienceGainB;
        }
    }

    public static void GetCrossfadeGains(float progress, out float fromGain, out float toGain)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        fromGain = 1f - clampedProgress;
        toGain = clampedProgress;
    }

    private IEnumerator CrossfadeAmbience(AudioSource outgoingSource, AudioSource incomingSource, float fadeSeconds)
    {
        float elapsedSeconds = 0f;
        while (elapsedSeconds < fadeSeconds)
        {
            GetCrossfadeGains(elapsedSeconds / fadeSeconds, out float fromGain, out float toGain);
            SetAmbienceGain(outgoingSource, fromGain);
            SetAmbienceGain(incomingSource, toGain);
            ApplyAmbienceVolumes();
            elapsedSeconds += Time.unscaledDeltaTime;
            yield return null;
        }

        SetAmbienceGain(outgoingSource, 0f);
        SetAmbienceGain(incomingSource, 1f);
        StopAndClearAmbienceSource(outgoingSource);
        activeAmbienceSource = incomingSource;
        ApplyAmbienceVolumes();
        ambienceTransitionCoroutine = null;
    }

    private IEnumerator FadeOutAmbience(AudioSource source, float fadeSeconds)
    {
        float startingGain = GetAmbienceGain(source);
        float elapsedSeconds = 0f;
        while (elapsedSeconds < fadeSeconds)
        {
            float progress = Mathf.Clamp01(elapsedSeconds / fadeSeconds);
            SetAmbienceGain(source, Mathf.Lerp(startingGain, 0f, progress));
            ApplyAmbienceVolumes();
            elapsedSeconds += Time.unscaledDeltaTime;
            yield return null;
        }

        StopAndClearAmbienceSource(source);
        ApplyAmbienceVolumes();
        ambienceTransitionCoroutine = null;
    }

    private void CancelAmbienceTransition()
    {
        if (ambienceTransitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(ambienceTransitionCoroutine);
        ambienceTransitionCoroutine = null;
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = CreateAudioSource("Music Source");
        }

        if (sfxSource == null)
        {
            sfxSource = CreateAudioSource("SFX Source");
        }

        if (ambienceSourceA == null)
        {
            ambienceSourceA = CreateAudioSource("Ambience Source A");
        }

        if (ambienceSourceB == null)
        {
            ambienceSourceB = CreateAudioSource("Ambience Source B");
        }

        musicSource.loop = true;
        sfxSource.loop = false;
        ConfigureAmbienceSource(ambienceSourceA);
        ConfigureAmbienceSource(ambienceSourceB);
        ambienceGainA = 0f;
        ambienceGainB = 0f;
    }

    private AudioSource CollapseToDominantAmbienceSource()
    {
        AudioSource dominantSource = GetDominantAmbienceSource();
        if (dominantSource == null)
        {
            StopAndClearAmbienceSource(ambienceSourceA);
            StopAndClearAmbienceSource(ambienceSourceB);
            return null;
        }

        AudioSource otherSource = dominantSource == ambienceSourceA ? ambienceSourceB : ambienceSourceA;
        StopAndClearAmbienceSource(otherSource);
        SetAmbienceGain(dominantSource, 1f);
        activeAmbienceSource = dominantSource;
        ApplyAmbienceVolumes();
        return dominantSource;
    }

    private AudioSource GetDominantAmbienceSource()
    {
        if (activeAmbienceSource != null && activeAmbienceSource.clip != null)
        {
            return activeAmbienceSource;
        }

        bool sourceAHasClip = ambienceSourceA != null && ambienceSourceA.clip != null;
        bool sourceBHasClip = ambienceSourceB != null && ambienceSourceB.clip != null;
        if (!sourceAHasClip && !sourceBHasClip)
        {
            return null;
        }

        return !sourceBHasClip || (sourceAHasClip && ambienceGainA >= ambienceGainB)
            ? ambienceSourceA
            : ambienceSourceB;
    }

    private void StopAndClearAmbienceSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
        SetAmbienceGain(source, 0f);
        if (activeAmbienceSource == source)
        {
            activeAmbienceSource = null;
        }
    }

    private void ConfigureAmbienceSource(AudioSource source)
    {
        source.loop = true;
        source.playOnAwake = false;
    }

    private float GetAmbienceGain(AudioSource source)
    {
        return source == ambienceSourceA ? ambienceGainA : source == ambienceSourceB ? ambienceGainB : 0f;
    }

    private void SetAmbienceGain(AudioSource source, float gain)
    {
        if (source == ambienceSourceA)
        {
            ambienceGainA = Mathf.Clamp01(gain);
        }
        else if (source == ambienceSourceB)
        {
            ambienceGainB = Mathf.Clamp01(gain);
        }
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        var sourceGo = new GameObject(sourceName);
        sourceGo.transform.SetParent(transform, false);
        return sourceGo.AddComponent<AudioSource>();
    }
}
