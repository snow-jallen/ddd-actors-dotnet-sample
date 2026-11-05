namespace Ch03.LanguageLab.Tests;

/// <summary>What a status board shows: a connector's status, or that its charger is gone.</summary>
public union StatusReport(ConnectorStatus, ChargerOffline);

public sealed record ChargerOffline(string ChargePointId);
