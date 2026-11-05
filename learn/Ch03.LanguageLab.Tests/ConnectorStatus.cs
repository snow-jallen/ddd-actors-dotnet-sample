namespace Ch03.LanguageLab.Tests;

/// <summary>A connector is in exactly one of three states. Each case is a record.</summary>
public union ConnectorStatus(Available, Occupied, Faulted);

public sealed record Available;

public sealed record Occupied(string SessionId);

public sealed record Faulted(string ErrorCode);
