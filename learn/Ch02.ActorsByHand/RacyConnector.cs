namespace Ch02.ActorsByHand;

/// <summary>
/// A connector that any thread may call. It checks that it is free, asks the token service
/// whether the card may charge, and then records the session: check, then act.
/// </summary>
public sealed class RacyConnector(string connectorId, Func<string, bool> authorize)
{
    public string ConnectorId { get; } = connectorId;

    public string? ActiveSession { get; private set; }

    public bool TryStartSession(string sessionId, string tokenUid)
    {
        if (ActiveSession is not null)
            return false;

        // Another thread can pass the check above while this one waits for the token service.
        if (!authorize(tokenUid))
            return false;

        ActiveSession = sessionId;
        return true;
    }

    public void EndSession(string sessionId)
    {
        if (ActiveSession == sessionId)
            ActiveSession = null;
    }
}
