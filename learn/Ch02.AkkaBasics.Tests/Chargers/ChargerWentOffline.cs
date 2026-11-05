namespace Ch02.AkkaBasics.Tests.Chargers;

/// <summary>Told to the twin's parent when the charger has missed too many heartbeats.</summary>
public sealed record ChargerWentOffline(string ChargePointId, int MissedHeartbeats);
