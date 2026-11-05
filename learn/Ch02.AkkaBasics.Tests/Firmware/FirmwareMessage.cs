namespace Ch02.AkkaBasics.Tests.Firmware;

public abstract record FirmwareMessage;

/// <summary>Asks for the latest firmware release for a charger model.</summary>
public sealed record CheckFirmware(string Model) : FirmwareMessage;

/// <summary>Answered at once by an actor whose mailbox is running.</summary>
public sealed record Ping : FirmwareMessage;
