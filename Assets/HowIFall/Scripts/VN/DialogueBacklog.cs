using System;
using System.Collections.Generic;

public sealed class DialogueBacklog
{
    public const int DefaultCapacity = 100;
    public const int MaximumEntryTextLength = 16384;

    private const string EmptyHistoryText = "История пока пуста.";

    private readonly int capacity;
    private readonly List<DialogueBacklogEntry> entries = new List<DialogueBacklogEntry>();

    public DialogueBacklog(int capacity)
    {
        this.capacity = capacity > 0 ? capacity : 1;
    }

    public int Count => entries.Count;

    public void Add(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        entries.Add(new DialogueBacklogEntry
        {
            speaker = speaker ?? string.Empty,
            text = text
        });

        int excessCount = entries.Count - capacity;
        if (excessCount > 0)
        {
            entries.RemoveRange(0, excessCount);
        }
    }

    public List<DialogueBacklogEntry> CaptureSnapshot()
    {
        var snapshot = new List<DialogueBacklogEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            DialogueBacklogEntry entry = entries[i];
            snapshot.Add(new DialogueBacklogEntry
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text
            });
        }

        return snapshot;
    }

    public void ReplaceFromSnapshot(
        IEnumerable<DialogueBacklogEntry> snapshot,
        Action<string> warningSink = null)
    {
        entries.Clear();
        if (snapshot == null)
        {
            return;
        }

        foreach (DialogueBacklogEntry entry in snapshot)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.text))
            {
                continue;
            }

            if (entry.text.Length > MaximumEntryTextLength)
            {
                warningSink?.Invoke(
                    $"Backlog entry with {entry.text.Length} characters exceeds the {MaximumEntryTextLength}-character limit and was skipped.");
                continue;
            }

            entries.Add(new DialogueBacklogEntry
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text
            });

            int excessCount = entries.Count - capacity;
            if (excessCount > 0)
            {
                entries.RemoveRange(0, excessCount);
            }
        }
    }

    public void Clear()
    {
        entries.Clear();
    }

    public string BuildRichText()
    {
        if (entries.Count == 0)
        {
            return EmptyHistoryText;
        }

        var formattedEntries = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            formattedEntries[i] = FormatEntry(entries[i]);
        }

        return string.Join("\n\n", formattedEntries);
    }

    private static string FormatEntry(DialogueBacklogEntry entry)
    {
        string text = EscapeRichText(entry.text);

        if (string.IsNullOrWhiteSpace(entry.speaker))
        {
            return $"<size=24><color=#FFFFFFDB>{text}</color></size>";
        }

        string speaker = EscapeRichText(entry.speaker);
        return $"<size=22><b><color=#F2F2FFFF>{speaker}</color></b></size>\n<size=24><color=#FFFFFFDB>{text}</color></size>";
    }

    private static string EscapeRichText(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
