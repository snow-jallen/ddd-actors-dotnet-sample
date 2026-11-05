namespace Ch02.ActorsByHand;

/// <summary>
/// The connector as an actor. Its state is private and only its mailbox loop touches it, so the
/// check-then-act in <see cref="Start"/> needs no lock. Callers tell it messages and learn the
/// outcome from the events it publishes, or from a reply sent to the address in the message.
/// </summary>
public sealed class ConnectorActor : ICanTell<ConnectorMessage>, IAsyncDisposable
{
    private readonly Func<string, bool> _authorize;
    private readonly Action<ConnectorEvent> _publish;
    private readonly Mailbox<ConnectorMessage> _mailbox;
    private string? _activeSession;

    public ConnectorActor(
        string connectorId, Func<string, bool> authorize, Action<ConnectorEvent> publish)
    {
        ConnectorId = connectorId;
        _authorize = authorize;
        _publish = publish;
        _mailbox = new Mailbox<ConnectorMessage>(Handle);
    }

    public string ConnectorId { get; }

    public void Tell(ConnectorMessage message) => _mailbox.Tell(message);

    public ValueTask DisposeAsync() => _mailbox.DisposeAsync();

    private void Handle(ConnectorMessage message)
    {
        switch (message)
        {
            case StartSession start:
                Start(start);
                break;
            case EndSession end:
                End(end);
                break;
            case GetStatus get:
                get.ReplyTo(new ConnectorStatus(ConnectorId, _activeSession));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message), message, null);
        }
    }

    private void Start(StartSession start)
    {
        if (_activeSession is not null)
        {
            _publish(new SessionRejected(
                ConnectorId, start.SessionId, RejectionReason.ConnectorOccupied));
            return;
        }

        if (!_authorize(start.TokenUid))
        {
            _publish(new SessionRejected(
                ConnectorId, start.SessionId, RejectionReason.TokenNotAuthorized));
            return;
        }

        _activeSession = start.SessionId;
        _publish(new SessionStarted(ConnectorId, start.SessionId));
    }

    private void End(EndSession end)
    {
        // Ending a session that is not active changes nothing: a repeated EndSession is harmless.
        if (_activeSession != end.SessionId)
            return;

        _activeSession = null;
        _publish(new SessionEnded(ConnectorId, end.SessionId));
    }
}
