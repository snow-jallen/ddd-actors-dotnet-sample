namespace Ch03.ClosedLib;

/// <summary>
/// Messages a charge point sends to Ohmline, as a plain abstract record for contrast: any
/// assembly can derive from it, so no switch outside this assembly can know every case.
/// </summary>
public abstract record ChargerNotification(string ChargePointId);

public sealed record BootNotification(string ChargePointId, string FirmwareVersion)
    : ChargerNotification(ChargePointId);

public sealed record Heartbeat(string ChargePointId) : ChargerNotification(ChargePointId);
