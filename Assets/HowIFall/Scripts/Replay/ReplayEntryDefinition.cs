using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/Replay/Entry Definition", fileName = "ReplayEntry")]
public sealed class ReplayEntryDefinition : ScriptableObject
{
    public string replayId;
    public string displayName;
    public Sprite thumbnail;
    public DialogueSceneData startScene;
}
