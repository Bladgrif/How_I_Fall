using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AudioAmbienceSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Audio Ambience Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall audio ambience smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        GameObject settingsObject = new GameObject("AudioAmbienceSmokeSettings");
        GameObject audioObject = new GameObject("AudioAmbienceSmokeManager");
        AudioClip musicClip = AudioClip.Create("AudioAmbienceSmokeMusic", 64, 1, 44100, false);
        AudioClip sfxClip = AudioClip.Create("AudioAmbienceSmokeSfx", 64, 1, 44100, false);
        AudioClip firstAmbienceClip = AudioClip.Create("AudioAmbienceSmokeFirst", 64, 1, 44100, false);
        AudioClip secondAmbienceClip = AudioClip.Create("AudioAmbienceSmokeSecond", 64, 1, 44100, false);

        try
        {
            SettingsManager settingsManager = settingsObject.AddComponent<SettingsManager>();
            SetSingleton(typeof(SettingsManager), settingsManager);
            settingsManager.settings.masterVolume = 0.9f;
            settingsManager.settings.musicVolume = 0.35f;
            settingsManager.settings.sfxVolume = 0.45f;
            settingsManager.settings.ambientVolume = 0.8f;

            AudioManager audioManager = audioObject.AddComponent<AudioManager>();
            InvokePrivate(audioManager, "EnsureAudioSources");

            VerifyDedicatedAmbienceSources(audioManager);
            VerifyImmediatePlaybackAndStop(audioManager, musicClip, sfxClip, firstAmbienceClip);
            VerifyCrossfadeMath();
            VerifyVolumeIsolation(audioManager, settingsManager, secondAmbienceClip);
            VerifyTransitionCancellation(audioManager, firstAmbienceClip, secondAmbienceClip);
            VerifySaveDataStaysAudioFree();
        }
        finally
        {
            SetSingleton(typeof(AudioManager), null);
            SetSingleton(typeof(SettingsManager), null);
            UnityEngine.Object.DestroyImmediate(audioObject);
            UnityEngine.Object.DestroyImmediate(settingsObject);
            UnityEngine.Object.DestroyImmediate(musicClip);
            UnityEngine.Object.DestroyImmediate(sfxClip);
            UnityEngine.Object.DestroyImmediate(firstAmbienceClip);
            UnityEngine.Object.DestroyImmediate(secondAmbienceClip);
        }
    }

    private static void VerifyDedicatedAmbienceSources(AudioManager audioManager)
    {
        Require(audioManager.ambienceSourceA != null && audioManager.ambienceSourceB != null, "AudioManager must create two ambience sources.");
        Require(audioManager.ambienceSourceA != audioManager.ambienceSourceB, "Ambience sources must be distinct.");
        Require(audioManager.musicSource != audioManager.ambienceSourceA && audioManager.musicSource != audioManager.ambienceSourceB, "Music source must stay independent from ambience.");
        Require(audioManager.sfxSource != audioManager.ambienceSourceA && audioManager.sfxSource != audioManager.ambienceSourceB, "SFX source must stay independent from ambience.");
        Require(audioManager.ambienceSourceA.loop && audioManager.ambienceSourceB.loop, "Ambience sources must loop.");
        Require(!audioManager.ambienceSourceA.playOnAwake && !audioManager.ambienceSourceB.playOnAwake, "Ambience sources must not play on awake.");
        Require(Mathf.Approximately(audioManager.AmbienceGainA, 0f) && Mathf.Approximately(audioManager.AmbienceGainB, 0f), "Ambience must start silent.");
    }

    private static void VerifyImmediatePlaybackAndStop(AudioManager audioManager, AudioClip musicClip, AudioClip sfxClip, AudioClip ambienceClip)
    {
        audioManager.musicSource.clip = musicClip;
        audioManager.sfxSource.clip = sfxClip;
        audioManager.PlayAmbience(ambienceClip, 0f);

        AudioSource activeSource = audioManager.ambienceSourceA.clip == ambienceClip
            ? audioManager.ambienceSourceA
            : audioManager.ambienceSourceB;
        Require(audioManager.CurrentAmbienceClip == ambienceClip, "Immediate ambience playback must select the requested clip.");
        Require(Mathf.Approximately(audioManager.AmbienceGainA + audioManager.AmbienceGainB, 1f), "Immediate ambience playback must use one full gain.");
        Require(audioManager.musicSource.clip == musicClip, "Ambience playback must not change music.");
        Require(audioManager.sfxSource.clip == sfxClip, "Ambience playback must not change SFX.");

        audioManager.PlayAmbience(ambienceClip, 0f);
        Require(activeSource.clip == ambienceClip && audioManager.CurrentAmbienceClip == ambienceClip, "Requesting the current ambience clip must not replace its source.");

        audioManager.PlayAmbience(null, 0f);
        Require(audioManager.CurrentAmbienceClip == ambienceClip, "Null ambience playback must be a safe no-op.");

        audioManager.StopAmbience(0f);
        Require(audioManager.CurrentAmbienceClip == null, "Immediate ambience stop must clear the active clip.");
        Require(audioManager.ambienceSourceA.clip == null && audioManager.ambienceSourceB.clip == null, "Immediate ambience stop must clear both sources.");
        Require(Mathf.Approximately(audioManager.AmbienceGainA, 0f) && Mathf.Approximately(audioManager.AmbienceGainB, 0f), "Immediate ambience stop must reset gains.");
        audioManager.StopAmbience(0f);
    }

    private static void VerifyCrossfadeMath()
    {
        AudioManager.GetCrossfadeGains(0f, out float fromStart, out float toStart);
        AudioManager.GetCrossfadeGains(0.5f, out float fromMiddle, out float toMiddle);
        AudioManager.GetCrossfadeGains(1f, out float fromEnd, out float toEnd);
        AudioManager.GetCrossfadeGains(-1f, out float fromClampedLow, out _);
        AudioManager.GetCrossfadeGains(2f, out _, out float toClampedHigh);

        Require(Mathf.Approximately(fromStart, 1f) && Mathf.Approximately(toStart, 0f), "Crossfade must start at outgoing gain 1 and incoming gain 0.");
        Require(Mathf.Approximately(fromMiddle, 0.5f) && Mathf.Approximately(toMiddle, 0.5f), "Crossfade midpoint must split gains equally.");
        Require(Mathf.Approximately(fromEnd, 0f) && Mathf.Approximately(toEnd, 1f), "Crossfade must end at outgoing gain 0 and incoming gain 1.");
        Require(Mathf.Approximately(fromClampedLow, 1f) && Mathf.Approximately(toClampedHigh, 1f), "Crossfade gains must stay clamped to 0..1.");
    }

    private static void VerifyVolumeIsolation(AudioManager audioManager, SettingsManager settingsManager, AudioClip ambienceClip)
    {
        audioManager.PlayAmbience(ambienceClip, 0f);
        SetPrivate(audioManager, "ambienceGainA", 0.25f);
        SetPrivate(audioManager, "ambienceGainB", 0.75f);
        audioManager.ApplySettingsVolume();

        float musicVolume = audioManager.musicSource.volume;
        float sfxVolume = audioManager.sfxSource.volume;
        Require(Mathf.Approximately(audioManager.ambienceSourceA.volume, settingsManager.settings.ambientVolume * 0.25f), "Ambience A volume must equal ambient setting times its gain.");
        Require(Mathf.Approximately(audioManager.ambienceSourceB.volume, settingsManager.settings.ambientVolume * 0.75f), "Ambience B volume must equal ambient setting times its gain.");

        settingsManager.settings.ambientVolume = 0.4f;
        audioManager.ApplySettingsVolume();
        Require(Mathf.Approximately(audioManager.AmbienceGainA, 0.25f) && Mathf.Approximately(audioManager.AmbienceGainB, 0.75f), "Changing ambient volume must preserve active crossfade gains.");
        Require(Mathf.Approximately(audioManager.ambienceSourceA.volume, 0.1f) && Mathf.Approximately(audioManager.ambienceSourceB.volume, 0.3f), "Ambient setting must affect ambience sources only through gains.");
        Require(Mathf.Approximately(audioManager.musicSource.volume, musicVolume) && Mathf.Approximately(audioManager.sfxSource.volume, sfxVolume), "Ambient setting must not change music or SFX volume.");

        settingsManager.settings.musicVolume = 0.6f;
        settingsManager.settings.sfxVolume = 0.7f;
        audioManager.ApplySettingsVolume();
        Require(Mathf.Approximately(audioManager.musicSource.volume, 0.6f), "Music setting must affect music source.");
        Require(Mathf.Approximately(audioManager.sfxSource.volume, 0.7f), "SFX setting must affect SFX source.");
        Require(Mathf.Approximately(audioManager.ambienceSourceA.volume, 0.1f) && Mathf.Approximately(audioManager.ambienceSourceB.volume, 0.3f), "Music and SFX settings must not change ambience volume.");
    }

    private static void VerifyTransitionCancellation(AudioManager audioManager, AudioClip firstClip, AudioClip secondClip)
    {
        audioManager.StopAmbience(0f);
        audioManager.PlayAmbience(firstClip, AudioManager.DefaultAmbienceFadeSeconds);
        Require(audioManager.IsAmbienceTransitionActive, "Timed ambience playback must own one transition coroutine.");
        audioManager.PlayAmbience(secondClip, 0f);
        Require(!audioManager.IsAmbienceTransitionActive, "Immediate replacement must cancel the previous ambience transition.");
        Require(audioManager.CurrentAmbienceClip == secondClip, "Replacement after an interrupted fade must select the newest clip.");
        Require(audioManager.ambienceSourceA.clip == null || audioManager.ambienceSourceB.clip == null, "Interrupted crossfade must leave only one ambience source assigned.");
    }

    private static void VerifySaveDataStaysAudioFree()
    {
        Require(SaveData.CurrentVersion == 3, "Ambience runtime work must preserve SaveData v3.");
        Require(!typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.Name.IndexOf("ambience", StringComparison.OrdinalIgnoreCase) >= 0),
            "SaveData must not gain ambience runtime fields.");
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, $"Missing lifecycle method '{methodName}'.");
        method.Invoke(target, null);
    }

    private static void SetSingleton(Type type, object value)
    {
        FieldInfo field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Require(field != null, $"Missing singleton backing field for '{type.Name}'.");
        field.SetValue(null, value);
    }

    private static void SetPrivate(object target, string fieldName, float value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"Missing ambience runtime field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
