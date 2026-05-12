using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speaker;

    [TextArea(2, 5)]
    public string text;
}
