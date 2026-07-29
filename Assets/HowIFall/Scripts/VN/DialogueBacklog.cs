using System.Collections.Generic;

public sealed class DialogueBacklog
{
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
