namespace Ch02.AkkaBasics.Tests.Chargers;

/// <summary>Messages for a <see cref="ChargerTwin"/>, in the style of OCPP 1.6J.</summary>
public abstract record ChargerMessage;

/// <summary>The first message a charger sends after it connects or restarts.</summary>
public sealed record BootNotification(string Vendor, string Model, string FirmwareVersion)
    : ChargerMessage;

public sealed record StatusChanged(ChargerStatus Status) : ChargerMessage;

/// <summary>The charger's periodic sign of life.</summary>
public sealed record Heartbeat : ChargerMessage;

public sealed record GetStatus : ChargerMessage;
