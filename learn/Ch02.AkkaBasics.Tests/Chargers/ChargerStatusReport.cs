namespace Ch02.AkkaBasics.Tests.Chargers;

public sealed record ChargerStatusReport(
    string ChargePointId, ChargerStatus Status, string FirmwareVersion);
