using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string lineId;
    public Sprite background;
    public Sprite characterSprite;
    public CharacterPosition characterPosition = CharacterPosition.Center;
    public bool hideCharacter;

    public string speaker;

    [TextArea(2, 5)]
    public string text;
}
