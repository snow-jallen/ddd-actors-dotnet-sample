namespace Ch02.ActorsByHand;

/// <summary>
/// The same connector with every check-then-act sequence inside one lock. It is correct, but
/// the lock is held while the token service answers, and every caller must remember to take it.
/// </summary>
public sealed class LockedConnector(string connectorId, Func<string, bool> authorize)
{
    private readonly Lock _gate = new();
    private string? _activeSession;

    public string ConnectorId { get; } = connectorId;

    public string? ActiveSession
    {
        get
        {
            lock (_gate)
                return _activeSession;
        }
    }

    public bool TryStartSession(string sessionId, string tokenUid)
    {
        lock (_gate)
        {
            if (_activeSession is not null)
                return false;

            if (!authorize(tokenUid))
                return false;

            _activeSession = sessionId;
            return true;
        }
    }

    public void EndSession(string sessionId)
    {
        lock (_gate)
        {
            if (_activeSession == sessionId)
                _activeSession = null;
        }
    }
}
