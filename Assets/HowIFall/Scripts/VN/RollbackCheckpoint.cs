using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum RollbackCheckpointKind
{
    StableLine,
    PreChoice
}

/// <summary>Transient, non-serialized copy of the complete campaign state owned by GameState.</summary>
public sealed class RollbackGameStateSnapshot
{
    public string CurrentSceneId { get; }
    public string CurrentLineId { get; }
    public int CurrentLineIndex { get; }
    public int Lust { get; }
    public int Romance { get; }
    public int Purity { get; }
    public int CorruptionLevel { get; }
    public int SelfControl { get; }
    public int Suspicion { get; }
    public int TrustMasha { get; }
    public int TrustArtem { get; }
    public int LeraInterest { get; }
    public int SelectedChoiceIndex { get; }
    public bool ChoiceResultActive { get; }
    public string PendingNextSceneId { get; }

    public RollbackGameStateSnapshot(
        string currentSceneId,
        string currentLineId,
        int currentLineIndex,
        int lust,
        int romance,
        int purity,
        int corruptionLevel,
        int selfControl,
        int suspicion,
        int trustMasha,
        int trustArtem,
        int leraInterest,
        int selectedChoiceIndex,
        bool choiceResultActive,
        string pendingNextSceneId)
    {
        CurrentSceneId = currentSceneId ?? string.Empty;
        CurrentLineId = currentLineId ?? string.Empty;
        CurrentLineIndex = currentLineIndex;
        Lust = lust;
        Romance = romance;
        Purity = purity;
        CorruptionLevel = corruptionLevel;
        SelfControl = selfControl;
        Suspicion = suspicion;
        TrustMasha = trustMasha;
        TrustArtem = trustArtem;
        LeraInterest = leraInterest;
        SelectedChoiceIndex = selectedChoiceIndex;
        ChoiceResultActive = choiceResultActive;
        PendingNextSceneId = pendingNextSceneId ?? string.Empty;
    }

    public static RollbackGameStateSnapshot Capture(GameState gameState)
    {
        if (gameState == null)
        {
            return null;
        }

        return new RollbackGameStateSnapshot(
            gameState.currentSceneId,
            gameState.currentLineId,
            gameState.currentLineIndex,
            gameState.lust,
            gameState.romance,
            gameState.purity,
            gameState.corruptionLevel,
            gameState.selfControl,
            gameState.suspicion,
            gameState.trustMasha,
            gameState.trustArtem,
            gameState.leraInterest,
            gameState.selectedChoiceIndex,
            gameState.choiceResultActive,
            gameState.pendingNextSceneId);
    }

    public void ApplyTo(GameState gameState)
    {
        if (gameState == null)
        {
            throw new ArgumentNullException(nameof(gameState));
        }

        gameState.currentSceneId = CurrentSceneId;
        gameState.currentLineId = CurrentLineId;
        gameState.currentLineIndex = CurrentLineIndex;
        gameState.lust = Lust;
        gameState.romance = Romance;
        gameState.purity = Purity;
        gameState.corruptionLevel = CorruptionLevel;
        gameState.selfControl = SelfControl;
        gameState.suspicion = Suspicion;
        gameState.trustMasha = TrustMasha;
        gameState.trustArtem = TrustArtem;
        gameState.leraInterest = LeraInterest;
        gameState.selectedChoiceIndex = SelectedChoiceIndex;
        gameState.choiceResultActive = ChoiceResultActive;
        gameState.pendingNextSceneId = PendingNextSceneId;
    }

    public bool HasSameState(RollbackGameStateSnapshot other)
    {
        return other != null
            && string.Equals(CurrentSceneId, other.CurrentSceneId, StringComparison.Ordinal)
            && string.Equals(CurrentLineId, other.CurrentLineId, StringComparison.Ordinal)
            && CurrentLineIndex == other.CurrentLineIndex
            && Lust == other.Lust
            && Romance == other.Romance
            && Purity == other.Purity
            && CorruptionLevel == other.CorruptionLevel
            && SelfControl == other.SelfControl
            && Suspicion == other.Suspicion
            && TrustMasha == other.TrustMasha
            && TrustArtem == other.TrustArtem
            && LeraInterest == other.LeraInterest
            && SelectedChoiceIndex == other.SelectedChoiceIndex
            && ChoiceResultActive == other.ChoiceResultActive
            && string.Equals(PendingNextSceneId, other.PendingNextSceneId, StringComparison.Ordinal);
    }
}

/// <summary>Actual presentation state that authored line reconstruction cannot guarantee.</summary>
public sealed class RollbackPresentationSnapshot
{
    public Sprite BackgroundSprite { get; }
    public bool BackgroundEnabled { get; }
    public Color BackgroundColor { get; }
    public Sprite CharacterSprite { get; }
    public bool CharacterEnabled { get; }
    public Vector2 CharacterAnchoredPosition { get; }
    public Vector2 CharacterSizeDelta { get; }
    public AudioClip MusicClip { get; }
    public bool MusicWasPlaying { get; }

    public RollbackPresentationSnapshot(
        Sprite backgroundSprite,
        bool backgroundEnabled,
        Color backgroundColor,
        Sprite characterSprite,
        bool characterEnabled,
        Vector2 characterAnchoredPosition,
        Vector2 characterSizeDelta,
        AudioClip musicClip,
        bool musicWasPlaying)
    {
        BackgroundSprite = backgroundSprite;
        BackgroundEnabled = backgroundEnabled;
        BackgroundColor = backgroundColor;
        CharacterSprite = characterSprite;
        CharacterEnabled = characterEnabled;
        CharacterAnchoredPosition = characterAnchoredPosition;
        CharacterSizeDelta = characterSizeDelta;
        MusicClip = musicClip;
        MusicWasPlaying = musicWasPlaying;
    }

    public static RollbackPresentationSnapshot Capture(Image background, Image character, AudioSource musicSource)
    {
        RectTransform characterRect = character != null ? character.rectTransform : null;
        return new RollbackPresentationSnapshot(
            background != null ? background.sprite : null,
            background != null && background.enabled,
            background != null ? background.color : Color.clear,
            character != null ? character.sprite : null,
            character != null && character.enabled,
            characterRect != null ? characterRect.anchoredPosition : Vector2.zero,
            characterRect != null ? characterRect.sizeDelta : Vector2.zero,
            musicSource != null ? musicSource.clip : null,
            musicSource != null && musicSource.isPlaying);
    }

    public void Apply(Image background, Image character, AudioSource musicSource)
    {
        if (background == null)
        {
            throw new InvalidOperationException("Rollback background Image is missing.");
        }

        if (character == null)
        {
            throw new InvalidOperationException("Rollback character Image is missing.");
        }

        background.sprite = BackgroundSprite;
        background.enabled = BackgroundEnabled;
        background.color = BackgroundColor;

        character.sprite = CharacterSprite;
        character.enabled = CharacterEnabled;
        character.rectTransform.anchoredPosition = CharacterAnchoredPosition;
        character.rectTransform.sizeDelta = CharacterSizeDelta;

        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = MusicClip;
            if (MusicWasPlaying && MusicClip != null)
            {
                musicSource.Play();
            }
        }
        else if (MusicClip != null || MusicWasPlaying)
        {
            throw new InvalidOperationException("Rollback music AudioSource is missing.");
        }
    }

    public bool HasSameState(RollbackPresentationSnapshot other)
    {
        return other != null
            && BackgroundSprite == other.BackgroundSprite
            && BackgroundEnabled == other.BackgroundEnabled
            && BackgroundColor == other.BackgroundColor
            && CharacterSprite == other.CharacterSprite
            && CharacterEnabled == other.CharacterEnabled
            && CharacterAnchoredPosition == other.CharacterAnchoredPosition
            && CharacterSizeDelta == other.CharacterSizeDelta
            && MusicClip == other.MusicClip
            && MusicWasPlaying == other.MusicWasPlaying;
    }
}

/// <summary>One immutable rollback checkpoint. It is never serialized to SaveData.</summary>
public sealed class RollbackCheckpoint
{
    public const int MaximumBacklogCodeUnits = 65536;

    private readonly List<DialogueBacklogEntry> backlogEntries;

    public RollbackCheckpointKind Kind { get; }
    public long Ordinal { get; }
    public RollbackGameStateSnapshot GameState { get; }
    public RollbackPresentationSnapshot Presentation { get; }
    public int BacklogEntryCount => backlogEntries.Count;

    private RollbackCheckpoint(
        RollbackCheckpointKind kind,
        long ordinal,
        RollbackGameStateSnapshot gameState,
        IEnumerable<DialogueBacklogEntry> backlog,
        RollbackPresentationSnapshot presentation)
    {
        Kind = kind;
        Ordinal = ordinal;
        GameState = gameState;
        Presentation = presentation;
        backlogEntries = CopyBacklog(backlog);
    }

    public static bool TryCreate(
        RollbackCheckpointKind kind,
        RollbackGameStateSnapshot gameState,
        IEnumerable<DialogueBacklogEntry> backlog,
        RollbackPresentationSnapshot presentation,
        out RollbackCheckpoint checkpoint)
    {
        checkpoint = null;
        if (gameState == null || presentation == null || !IsBacklogWithinMemoryGuard(backlog))
        {
            return false;
        }

        checkpoint = new RollbackCheckpoint(kind, 0, gameState, backlog, presentation);
        return true;
    }

    internal static RollbackCheckpoint CreateTransactionFallback(
        RollbackGameStateSnapshot gameState,
        IEnumerable<DialogueBacklogEntry> backlog,
        RollbackPresentationSnapshot presentation)
    {
        return gameState == null || presentation == null
            ? null
            : new RollbackCheckpoint(RollbackCheckpointKind.StableLine, 0, gameState, backlog, presentation);
    }

    internal RollbackCheckpoint WithOrdinal(long ordinal, RollbackCheckpointKind kind)
    {
        return new RollbackCheckpoint(kind, ordinal, GameState, backlogEntries, Presentation);
    }

    public List<DialogueBacklogEntry> CaptureBacklogSnapshot()
    {
        return CopyBacklog(backlogEntries);
    }

    public bool HasSameStableState(RollbackCheckpoint other)
    {
        if (other == null
            || !GameState.HasSameState(other.GameState)
            || !Presentation.HasSameState(other.Presentation)
            || backlogEntries.Count != other.backlogEntries.Count)
        {
            return false;
        }

        for (int index = 0; index < backlogEntries.Count; index++)
        {
            DialogueBacklogEntry left = backlogEntries[index];
            DialogueBacklogEntry right = other.backlogEntries[index];
            if (!string.Equals(left.speaker, right.speaker, StringComparison.Ordinal)
                || !string.Equals(left.text, right.text, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsBacklogWithinMemoryGuard(IEnumerable<DialogueBacklogEntry> backlog)
    {
        long codeUnits = 0;
        if (backlog == null)
        {
            return true;
        }

        foreach (DialogueBacklogEntry entry in backlog)
        {
            if (entry == null)
            {
                continue;
            }

            codeUnits += (entry.speaker?.Length ?? 0) + (entry.text?.Length ?? 0);
            if (codeUnits > MaximumBacklogCodeUnits)
            {
                return false;
            }
        }

        return true;
    }

    private static List<DialogueBacklogEntry> CopyBacklog(IEnumerable<DialogueBacklogEntry> source)
    {
        var copy = new List<DialogueBacklogEntry>();
        if (source == null)
        {
            return copy;
        }

        foreach (DialogueBacklogEntry entry in source)
        {
            if (entry == null)
            {
                continue;
            }

            copy.Add(new DialogueBacklogEntry
            {
                speaker = entry.speaker ?? string.Empty,
                text = entry.text ?? string.Empty
            });
        }

        return copy;
    }
}

/// <summary>Controller-owned bounded history; selecting a target is non-mutating until commit.</summary>
public sealed class RollbackCheckpointBuffer
{
    public const int DefaultCapacity = 12;

    private readonly int capacity;
    private readonly List<RollbackCheckpoint> checkpoints = new List<RollbackCheckpoint>();
    private long nextOrdinal;

    public RollbackCheckpointBuffer(int capacity = DefaultCapacity)
    {
        this.capacity = Math.Max(1, capacity);
    }

    public int Count => checkpoints.Count;

    public bool TryAdd(RollbackCheckpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return false;
        }

        if (checkpoints.Count > 0 && checkpoints[checkpoints.Count - 1].HasSameStableState(checkpoint))
        {
            RollbackCheckpoint existing = checkpoints[checkpoints.Count - 1];
            if (checkpoint.Kind == RollbackCheckpointKind.PreChoice && existing.Kind != checkpoint.Kind)
            {
                checkpoints[checkpoints.Count - 1] = existing.WithOrdinal(existing.Ordinal, checkpoint.Kind);
            }

            return true;
        }

        nextOrdinal++;
        checkpoints.Add(checkpoint.WithOrdinal(nextOrdinal, checkpoint.Kind));
        if (checkpoints.Count > capacity)
        {
            checkpoints.RemoveAt(0);
        }

        return true;
    }

    public bool TryGetPrevious(
        RollbackCheckpoint currentRuntime,
        out RollbackCheckpoint target,
        out int targetIndex)
    {
        target = null;
        targetIndex = -1;
        if (checkpoints.Count == 0)
        {
            return false;
        }

        int latestIndex = checkpoints.Count - 1;
        targetIndex = currentRuntime != null && checkpoints[latestIndex].HasSameStableState(currentRuntime)
            ? latestIndex - 1
            : latestIndex;
        if (targetIndex < 0)
        {
            return false;
        }

        target = checkpoints[targetIndex];
        return true;
    }

    public void CommitRollback(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= checkpoints.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex));
        }

        int futureCount = checkpoints.Count - targetIndex - 1;
        if (futureCount > 0)
        {
            checkpoints.RemoveRange(targetIndex + 1, futureCount);
        }
    }

    public void Clear()
    {
        checkpoints.Clear();
    }
}
