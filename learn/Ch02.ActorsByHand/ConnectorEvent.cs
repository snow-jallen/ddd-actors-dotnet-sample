namespace Ch02.ActorsByHand;

/// <summary>What a <see cref="ConnectorActor"/> decided, published after each message.</summary>
public abstract record ConnectorEvent;

public sealed record SessionStarted(string ConnectorId, string SessionId) : ConnectorEvent;

public sealed record SessionRejected(string ConnectorId, string SessionId, RejectionReason Reason)
    : ConnectorEvent;

public sealed record SessionEnded(string ConnectorId, string SessionId) : ConnectorEvent;
