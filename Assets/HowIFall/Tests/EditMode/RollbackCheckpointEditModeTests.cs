using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RollbackCheckpointEditModeTests
{
    [Test]
    public void EmptyBuffer_HasNoPreviousCheckpoint()
    {
        var buffer = new RollbackCheckpointBuffer();

        Assert.That(buffer.TryGetPrevious(null, out _, out _), Is.False);
        Assert.That(buffer.Count, Is.Zero);
    }

    [Test]
    public void CapacityTwelve_EvictsOnlyOldest_AndRollbackOrderIsDeterministic()
    {
        var buffer = new RollbackCheckpointBuffer();
        var checkpoints = new List<RollbackCheckpoint>();
        for (int lineIndex = 1; lineIndex <= 13; lineIndex++)
        {
            RollbackCheckpoint checkpoint = CreateCheckpoint(lineIndex, $"line-{lineIndex}");
            checkpoints.Add(checkpoint);
            Assert.That(buffer.TryAdd(checkpoint), Is.True);
        }

        Assert.That(buffer.Count, Is.EqualTo(12));

        RollbackCheckpoint current = checkpoints[12];
        for (int expectedLine = 12; expectedLine >= 2; expectedLine--)
        {
            Assert.That(buffer.TryGetPrevious(current, out RollbackCheckpoint target, out int targetIndex), Is.True);
            Assert.That(target.GameState.CurrentLineIndex, Is.EqualTo(expectedLine));
            buffer.CommitRollback(targetIndex);
            current = target;
        }

        Assert.That(buffer.TryGetPrevious(current, out _, out _), Is.False, "Evicted line 1 must not reappear.");
    }

    [Test]
    public void CheckpointBacklog_IsDeepCopiedWithoutAliasing()
    {
        var source = new List<DialogueBacklogEntry>
        {
            new DialogueBacklogEntry { speaker = "Masha", text = "Original" }
        };

        Assert.That(RollbackCheckpoint.TryCreate(
            RollbackCheckpointKind.StableLine,
            CreateState(0, "line-0"),
            source,
            CreatePresentation(),
            out RollbackCheckpoint checkpoint), Is.True);

        source[0].speaker = "Changed";
        source[0].text = "Changed";
        List<DialogueBacklogEntry> firstRead = checkpoint.CaptureBacklogSnapshot();
        Assert.That(firstRead[0].speaker, Is.EqualTo("Masha"));
        Assert.That(firstRead[0].text, Is.EqualTo("Original"));

        firstRead[0].text = "Mutated read";
        Assert.That(checkpoint.CaptureBacklogSnapshot()[0].text, Is.EqualTo("Original"));
    }

    [Test]
    public void BacklogGuard_AcceptsExactly65536Utf16CodeUnits()
    {
        var backlog = new[]
        {
            new DialogueBacklogEntry { speaker = new string('s', 32768), text = new string('t', 32768) }
        };

        Assert.That(RollbackCheckpoint.TryCreate(
            RollbackCheckpointKind.StableLine,
            CreateState(0, "line-0"),
            backlog,
            CreatePresentation(),
            out RollbackCheckpoint checkpoint), Is.True);
        Assert.That(checkpoint, Is.Not.Null);
    }

    [Test]
    public void BacklogGuard_Rejects65537WithoutChangingExistingBuffer()
    {
        var buffer = new RollbackCheckpointBuffer();
        Assert.That(buffer.TryAdd(CreateCheckpoint(0, "line-0")), Is.True);
        var oversized = new[]
        {
            new DialogueBacklogEntry { speaker = new string('s', 32768), text = new string('t', 32769) }
        };

        Assert.That(RollbackCheckpoint.TryCreate(
            RollbackCheckpointKind.StableLine,
            CreateState(1, "line-1"),
            oversized,
            CreatePresentation(),
            out RollbackCheckpoint rejected), Is.False);
        Assert.That(rejected, Is.Null);
        Assert.That(buffer.Count, Is.EqualTo(1));
    }

    [Test]
    public void PartialFuture_SelectsLatestStable_WhileStableCurrentSelectsPrior()
    {
        var buffer = new RollbackCheckpointBuffer();
        RollbackCheckpoint lineA = CreateCheckpoint(0, "line-a");
        RollbackCheckpoint lineB = CreateCheckpoint(1, "line-b");
        buffer.TryAdd(lineA);
        buffer.TryAdd(lineB);

        RollbackCheckpoint partialFuture = CreateCheckpoint(2, "line-c-partial");
        Assert.That(buffer.TryGetPrevious(partialFuture, out RollbackCheckpoint fromPartial, out _), Is.True);
        Assert.That(fromPartial.GameState.CurrentLineId, Is.EqualTo("line-b"));

        Assert.That(buffer.TryGetPrevious(lineB, out RollbackCheckpoint fromStable, out _), Is.True);
        Assert.That(fromStable.GameState.CurrentLineId, Is.EqualTo("line-a"));
    }

    [Test]
    public void PreChoiceCapture_PromotesDuplicateInsteadOfAddingAnotherCheckpoint()
    {
        var buffer = new RollbackCheckpointBuffer();
        RollbackCheckpoint stable = CreateCheckpoint(0, "choice-line", RollbackCheckpointKind.StableLine);
        RollbackCheckpoint choice = CreateCheckpoint(0, "choice-line", RollbackCheckpointKind.PreChoice);

        Assert.That(buffer.TryAdd(stable), Is.True);
        Assert.That(buffer.TryAdd(choice), Is.True);
        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer.TryGetPrevious(CreateCheckpoint(1, "future"), out RollbackCheckpoint target, out _), Is.True);
        Assert.That(target.Kind, Is.EqualTo(RollbackCheckpointKind.PreChoice));
    }

    private static RollbackCheckpoint CreateCheckpoint(
        int lineIndex,
        string lineId,
        RollbackCheckpointKind kind = RollbackCheckpointKind.StableLine)
    {
        Assert.That(RollbackCheckpoint.TryCreate(
            kind,
            CreateState(lineIndex, lineId),
            new[] { new DialogueBacklogEntry { speaker = "", text = lineId } },
            CreatePresentation(),
            out RollbackCheckpoint checkpoint), Is.True);
        return checkpoint;
    }

    private static RollbackGameStateSnapshot CreateState(int lineIndex, string lineId)
    {
        return new RollbackGameStateSnapshot(
            "scene",
            lineId,
            lineIndex,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            -1,
            false,
            string.Empty);
    }

    private static RollbackPresentationSnapshot CreatePresentation()
    {
        return new RollbackPresentationSnapshot(
            null,
            true,
            Color.white,
            null,
            false,
            new Vector2(10f, 20f),
            new Vector2(30f, 40f),
            null,
            false);
    }
}
