public readonly struct ReplayContext
{
    public ReplayContext(string replayId)
    {
        ReplayId = replayId;
    }

    public string ReplayId { get; }
}
