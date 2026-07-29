using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/VN/Dialogue Scene", fileName = "DialogueScene")]
public class DialogueSceneData : ScriptableObject
{
    public string sceneId;
    public AudioClip backgroundMusic;
    public bool stopMusicOnStart;
    public List<DialogueLine> lines = new List<DialogueLine>();
    public List<DialogueChoice> choices = new List<DialogueChoice>();
    public DialogueSceneData defaultNextScene;

    public int FindLineIndexById(string lineId)
    {
        if (lines == null || string.IsNullOrEmpty(lineId))
        {
            return -1;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null && lines[i].lineId == lineId)
            {
                return i;
            }
        }

        return -1;
    }
}
