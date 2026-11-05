namespace Ch02.AkkaBasics.Tests.Connectors;

public abstract record ConnectorEvent;

public sealed record SessionStarted(string ConnectorId, string SessionId) : ConnectorEvent;

public sealed record SessionRejected(string ConnectorId, string SessionId) : ConnectorEvent;

public sealed record SessionEnded(string ConnectorId, string SessionId) : ConnectorEvent;
