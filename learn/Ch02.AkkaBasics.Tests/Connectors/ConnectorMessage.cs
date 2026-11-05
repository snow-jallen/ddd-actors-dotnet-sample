namespace Ch02.AkkaBasics.Tests.Connectors;

/// <summary>
/// The connector's messages from section 2.2. None of them carries a reply address: Akka.NET
/// sends the sender's address along with every message.
/// </summary>
public abstract record ConnectorMessage;

public sealed record StartSession(string SessionId) : ConnectorMessage;

public sealed record EndSession(string SessionId) : ConnectorMessage;

public sealed record GetStatus : ConnectorMessage;
