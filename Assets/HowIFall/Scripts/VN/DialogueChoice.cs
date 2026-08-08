using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueChoice
{
    public string text;

    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();

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
    public DialogueSceneData nextScene;
}
