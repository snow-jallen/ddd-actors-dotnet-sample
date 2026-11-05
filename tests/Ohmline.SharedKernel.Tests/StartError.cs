namespace Ohmline.SharedKernel.Tests;

/// <summary>An error set that is itself a union, as most of Ohmline's error sets are.</summary>
public union StartError(ConnectorBusy, TokenRejected);

public sealed record ConnectorBusy(int ConnectorNumber);

public sealed record TokenRejected(string TokenUid);
