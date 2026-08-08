using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ReplaySession
{
    private enum SessionState
    {
        Starting,
        Active,
        Ending,
        Ended
    }

    private readonly ReplayGameStateSnapshot campaignState;
    private readonly VNDialogueController campaignController;
    private readonly List<DialogueBacklogEntry> campaignBacklog;
    private readonly HashSet<string> localSeen = new HashSet<string>(StringComparer.Ordinal);
    private readonly AudioClip musicClip;
    private readonly bool musicWasPlaying;
    private readonly AudioClip ambienceClip;
    private readonly bool ambienceWasPlaying;
    private SessionState state = SessionState.Starting;
    private bool campaignStateRestored;
    private VNDialogueController replayHost;

    public ReplaySession(ReplayEntryDefinition definition, GameState gameState, VNDialogueController activeController)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Context = new ReplayContext(definition.replayId);
        campaignState = ReplayGameStateSnapshot.Capture(gameState)
            ?? throw new ArgumentNullException(nameof(gameState));
        campaignController = activeController;
        campaignBacklog = activeController != null ? activeController.CaptureBacklogSnapshot() : null;

        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            musicClip = audio.musicSource != null ? audio.musicSource.clip : null;
            musicWasPlaying = audio.musicSource != null && audio.musicSource.isPlaying;
            ambienceClip = audio.CurrentAmbienceClip;
            ambienceWasPlaying = audio.IsAmbiencePlaying;
        }
    }

    public ReplayEntryDefinition Definition { get; }
    public ReplayContext Context { get; }
    public bool IsReplayMode => state != SessionState.Ended;
    public bool IsEnding => state == SessionState.Ending;
    public VNDialogueController ReplayHost => replayHost;

    public void Activate(GameState gameState)
    {
        if (state != SessionState.Starting)
        {
            throw new InvalidOperationException("Replay session activation is not in the starting state.");
        }

        gameState.ResetState();
        gameState.currentSceneId = Definition.startScene.sceneId;
        gameState.currentLineIndex = 0;
        gameState.currentLineId = Definition.startScene.lines[0].lineId ?? string.Empty;
        campaignController?.ClearBacklog();
        state = SessionState.Active;
    }

    public void AttachReplayHost(VNDialogueController controller)
    {
        if (state == SessionState.Active)
        {
            replayHost = controller;
        }
    }

    public bool IsSeen(string sceneId, string lineId)
    {
        string key = DialogueReadHistory.CreateKey(sceneId, lineId);
        return !string.IsNullOrEmpty(key) && localSeen.Contains(key);
    }

    public void MarkSeen(string sceneId, string lineId)
    {
        string key = DialogueReadHistory.CreateKey(sceneId, lineId);
        if (!string.IsNullOrEmpty(key))
        {
            localSeen.Add(key);
        }
    }

    public bool RestoreCampaignState(GameState gameState)
    {
        if (campaignStateRestored)
        {
            return false;
        }

        campaignStateRestored = true;
        RestoreAudio();
        replayHost?.ClearBacklog();
        localSeen.Clear();
        campaignState.Restore(gameState);
        if (campaignController != null && campaignController == VNDialogueController.Instance)
        {
            campaignController.ReplaceBacklogFromSnapshot(campaignBacklog);
        }

        replayHost = null;
        return true;
    }

    public bool BeginEnding()
    {
        if (state == SessionState.Ending || state == SessionState.Ended)
        {
            return false;
        }

        state = SessionState.Ending;
        return true;
    }

    public void MarkEnded()
    {
        state = SessionState.Ended;
    }

    private void RestoreAudio()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        audio.RestorePlaybackStateAfterReplay(
            musicClip,
            musicWasPlaying,
            ambienceClip,
            ambienceWasPlaying);
    }
}
