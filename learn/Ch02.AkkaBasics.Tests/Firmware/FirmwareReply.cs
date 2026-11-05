namespace Ch02.AkkaBasics.Tests.Firmware;

public abstract record FirmwareReply;

public sealed record FirmwareRelease(string Model, string Version) : FirmwareReply;

public sealed record FirmwareUnavailable(string Model) : FirmwareReply;

public sealed record Pong : FirmwareReply;
