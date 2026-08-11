using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "How I Fall/Chat Phone/Chat Scene", fileName = "ChatScene")]
public sealed class ChatSceneData : ScriptableObject
{
    public string chatId;
    public string contactDisplayName;
    public List<ChatEntry> entries = new List<ChatEntry>();
    public DialogueSceneData returnScene;

    public bool TryValidate(DialogueSceneRegistry registry, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(contactDisplayName) || entries == null || entries.Count == 0)
        { diagnostic = "chatId, contactDisplayName, and entries are required."; return false; }
        if (returnScene == null || registry == null || registry.scenes == null || !registry.scenes.Contains(returnScene) || returnScene.lines == null || returnScene.lines.Count == 0)
        { diagnostic = "returnScene is missing, invalid, or unregistered."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var byId = new Dictionary<string, ChatEntry>(StringComparer.Ordinal);
        int images = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            ChatEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.entryId) || !ids.Add(entry.entryId)) { diagnostic = "Entry IDs must be non-empty and unique."; return false; }
            if (!Enum.IsDefined(typeof(ChatEntryKind), entry.kind) || !Enum.IsDefined(typeof(ChatSenderSide), entry.sender)) { diagnostic = "Entry kind or sender is unsupported."; return false; }
            if (!TryValidatePacing(entry, out diagnostic)) { return false; }
            byId.Add(entry.entryId, entry);
            if (entry.kind == ChatEntryKind.Text)
            {
                if (string.IsNullOrWhiteSpace(entry.text) || entry.image != null || (entry.options != null && entry.options.Count != 0) || !string.IsNullOrEmpty(entry.fallbackEntryId)) { diagnostic = "Text entry has mismatched or empty payload."; return false; }
            }
            if (entry.kind == ChatEntryKind.Image)
            {
                images++;
                if (entry.image == null || !string.IsNullOrEmpty(entry.text) || (entry.options != null && entry.options.Count != 0) || !string.IsNullOrEmpty(entry.fallbackEntryId)) { diagnostic = "Image entry has mismatched or empty payload."; return false; }
            }
            if (entry.kind == ChatEntryKind.Choice)
            {
                if (entry.options == null || entry.options.Count != 2) { diagnostic = "Choice entry must have exactly two options."; return false; }
                if (!string.IsNullOrEmpty(entry.text) || entry.image != null) { diagnostic = "Choice entry has mismatched payload."; return false; }
                for (int o = 0; o < entry.options.Count; o++)
                {
                    ChatChoiceOption option = entry.options[o];
                    if (option == null || string.IsNullOrWhiteSpace(option.text) || !AreConditionsValid(option.conditions)) { diagnostic = "Choice option payload or conditions are invalid."; return false; }
                }
            }
        }
        if (images > 1) { diagnostic = "V1 permits at most one Image entry."; return false; }
        for (int i = 0; i < entries.Count; i++)
        {
            ChatEntry entry = entries[i];
            if (entry.kind != ChatEntryKind.Choice) continue;
            foreach (ChatChoiceOption option in entry.options)
                if (!string.IsNullOrEmpty(option.nextEntryId) && !byId.ContainsKey(option.nextEntryId)) { diagnostic = "Choice target is missing from this chat."; return false; }
            if (!string.IsNullOrEmpty(entry.fallbackEntryId) && !byId.ContainsKey(entry.fallbackEntryId)) { diagnostic = "Choice fallback target is missing from this chat."; return false; }
        }
        if (!HasTerminalPath(0, byId, new HashSet<int>())) { diagnostic = "Chat graph has no valid terminal path or contains a cycle."; return false; }
        return true;
    }

    public static bool TryValidatePacing(ChatEntry entry, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (entry == null || !Enum.IsDefined(typeof(ChatEntryPacing), entry.pacing)
            || float.IsNaN(entry.pacingSeconds) || float.IsInfinity(entry.pacingSeconds) || entry.pacingSeconds < 0f)
        {
            diagnostic = "Entry pacing is unsupported or has a negative duration.";
            return false;
        }

        if (entry.kind == ChatEntryKind.Choice)
        {
            if (entry.pacing != ChatEntryPacing.Immediate)
            {
                diagnostic = "Choice entry pacing must be Immediate.";
                return false;
            }

            return true;
        }

        if (entry.pacing == ChatEntryPacing.Immediate)
        {
            return true;
        }

        if (entry.kind != ChatEntryKind.Text && entry.kind != ChatEntryKind.Image)
        {
            diagnostic = "Delayed pacing is only supported by Text or Image entries.";
            return false;
        }

        if (entry.pacingSeconds <= 0f)
        {
            diagnostic = "Delayed pacing requires a duration greater than zero.";
            return false;
        }

        if (entry.pacing == ChatEntryPacing.IncomingTyping && entry.sender != ChatSenderSide.Incoming)
        {
            diagnostic = "IncomingTyping pacing requires an Incoming sender.";
            return false;
        }

        return true;
    }

    public int FindEntryIndex(string entryId)
    {
        if (entries == null || string.IsNullOrEmpty(entryId)) return -1;
        for (int i = 0; i < entries.Count; i++) if (entries[i] != null && entries[i].entryId == entryId) return i;
        return -1;
    }

    private bool HasTerminalPath(int index, Dictionary<string, ChatEntry> byId, HashSet<int> visiting)
    {
        if (index < 0 || index >= entries.Count) return true;
        if (!visiting.Add(index)) return false;
        ChatEntry entry = entries[index]; bool result;
        if (entry.kind != ChatEntryKind.Choice) result = index == entries.Count - 1 || HasTerminalPath(index + 1, byId, visiting);
        else
        {
            result = true;
            foreach (ChatChoiceOption option in entry.options)
            {
                if (string.IsNullOrEmpty(option.nextEntryId)) continue;
                result &= HasTerminalPath(FindEntryIndex(option.nextEntryId), byId, new HashSet<int>(visiting));
            }
            if (!string.IsNullOrEmpty(entry.fallbackEntryId)) result &= HasTerminalPath(FindEntryIndex(entry.fallbackEntryId), byId, new HashSet<int>(visiting));
        }
        visiting.Remove(index); return result;
    }

    private static bool AreConditionsValid(List<ChoiceCondition> conditions)
    {
        if (conditions == null) return false;
        foreach (ChoiceCondition condition in conditions)
            if (condition == null || !Enum.IsDefined(typeof(ChoiceStateValue), condition.stateValue) || !Enum.IsDefined(typeof(ChoiceComparisonOperator), condition.comparison)) return false;
        return true;
    }
}
