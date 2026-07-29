using System;
using UnityEditor;
using UnityEngine;

public static class DialogueBacklogSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Dialogue Backlog Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall dialogue backlog smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall dialogue backlog smoke tests passed.");
    }

    private static void Run()
    {
        TestEmptyBacklog();
        TestFormattingAndEscaping();
        TestCapacity();
    }

    private static void TestEmptyBacklog()
    {
        var backlog = new DialogueBacklog(3);
        Require(backlog.Count == 0, "New backlog is not empty.");
        Require(backlog.BuildRichText() == "История пока пуста.", "Empty backlog text is incorrect.");
    }

    private static void TestFormattingAndEscaping()
    {
        var backlog = new DialogueBacklog(3);
        backlog.Add("<Герой>", "A&B <tag>");
        backlog.Add(string.Empty, "Описание");

        string text = backlog.BuildRichText();
        Require(text.Contains("&lt;Герой&gt;"), "Speaker rich text was not escaped.");
        Require(text.Contains("A&amp;B &lt;tag&gt;"), "Dialogue rich text was not escaped.");
        Require(text.Contains("Описание"), "Narration entry is missing.");
    }

    private static void TestCapacity()
    {
        var backlog = new DialogueBacklog(2);
        backlog.Add("", "first");
        backlog.Add("", "second");
        backlog.Add("", "third");

        string text = backlog.BuildRichText();
        Require(backlog.Count == 2, "Backlog capacity was not enforced.");
        Require(!text.Contains("first"), "Oldest backlog entry was not removed.");
        Require(text.Contains("second") && text.Contains("third"), "Recent backlog entries were lost.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
