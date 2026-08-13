using NUnit.Framework;

public sealed class DialogueBacklogEditModeTests
{
    [Test]
    public void RetainsMostRecentEntriesWhenCapacityIsExceeded()
    {
        var backlog = new DialogueBacklog(2);
        backlog.Add(string.Empty, "first");
        backlog.Add(string.Empty, "second");
        backlog.Add(string.Empty, "third");

        string text = backlog.BuildRichText();

        Assert.That(backlog.Count, Is.EqualTo(2));
        Assert.That(text, Does.Not.Contain("first"));
        Assert.That(text, Does.Contain("second"));
        Assert.That(text, Does.Contain("third"));
    }
}