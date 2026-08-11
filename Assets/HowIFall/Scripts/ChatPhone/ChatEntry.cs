using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChatEntryKind { Text, Image, Choice }
public enum ChatSenderSide { Incoming, Player }
public enum ChatEntryPacing { Immediate, Delay, IncomingTyping }
public enum ChatRuntimeState { Idle, Active, WaitingForEntryReveal, ResolvingTerminalChoice, Resolved }

[Serializable]
public sealed class ChatGameStateDelta
{
    public int lustDelta;
    public int romanceDelta;
    public int purityDelta;
    public int corruptionDelta;
    public int selfControlDelta;
    public int suspicionDelta;
    public int trustMashaDelta;
    public int trustArtemDelta;
    public int leraInterestDelta;

    public void ApplyTo(GameState state)
    {
        state?.ApplyChoiceStateDelta(lustDelta, romanceDelta, purityDelta, corruptionDelta, selfControlDelta, suspicionDelta, trustMashaDelta, trustArtemDelta, leraInterestDelta);
    }
}

[Serializable]
public sealed class ChatChoiceOption
{
    public string text;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
    public ChatGameStateDelta effects = new ChatGameStateDelta();
    public string nextEntryId;
}

[Serializable]
public sealed class ChatEntry
{
    public string entryId;
    public ChatEntryKind kind;
    public ChatSenderSide sender;
    public ChatEntryPacing pacing;
    [Min(0f)] public float pacingSeconds;
    [TextArea(2, 5)] public string text;
    public Sprite image;
    public List<ChatChoiceOption> options = new List<ChatChoiceOption>();
    public string fallbackEntryId;
}

public sealed class ChatTranscriptEntry
{
    public readonly ChatSenderSide sender;
    public readonly string text;
    public readonly Sprite image;
    public ChatTranscriptEntry(ChatSenderSide sender, string text, Sprite image = null) { this.sender = sender; this.text = text ?? string.Empty; this.image = image; }
}
