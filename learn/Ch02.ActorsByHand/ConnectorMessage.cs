namespace Ch02.ActorsByHand;

/// <summary>
/// The messages a <see cref="ConnectorActor"/> accepts. Records are immutable, so a message
/// cannot change after it has been sent.
/// </summary>
public abstract record ConnectorMessage;

public sealed record StartSession(string SessionId, string TokenUid) : ConnectorMessage;

public sealed record EndSession(string SessionId) : ConnectorMessage;

public sealed record GetStatus(ReplyTo<ConnectorStatus> ReplyTo) : ConnectorMessage;
