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
}
