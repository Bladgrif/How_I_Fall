using System;
using UnityEngine;

[Serializable]
public class DialogueChoice
{
    public string text;

    [TextArea(2, 5)]
    public string resultText;

    public int lustDelta;
    public int romanceDelta;
    public int purityDelta;
    public int corruptionDelta;
    public int selfControlDelta;
    public int suspicionDelta;
    public int trustMashaDelta;
    public int trustArtemDelta;
    public int leraInterestDelta;
}
