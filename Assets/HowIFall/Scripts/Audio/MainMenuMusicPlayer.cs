using UnityEngine;

public class MainMenuMusicPlayer : MonoBehaviour
{
    public AudioClip musicClip;

    private void Start()
    {
        if (musicClip == null)
        {
            Debug.LogWarning("Main menu music clip is not assigned.");
            return;
        }

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("AudioManager.Instance is missing.");
            return;
        }

        AudioManager.Instance.PlayMusic(musicClip);
    }
}
