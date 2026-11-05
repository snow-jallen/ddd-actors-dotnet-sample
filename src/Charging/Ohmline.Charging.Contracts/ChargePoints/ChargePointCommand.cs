namespace Ohmline.Charging.Contracts.ChargePoints;

/// <summary>
/// Everything a charge point can be asked to do, named as on the design-level EventStorming
/// board (<c>docs/eventstorming/design-level.md</c>). The base is closed: only this assembly
/// can add a command, so a switch over it anywhere else knows every case.
/// </summary>
/// <remarks>The commands carry no data yet; section 7.2 adds it.</remarks>
public closed record ChargePointCommand;

/// <summary>Add a charge point and its connectors to Ohmline's network.</summary>
public sealed record RegisterChargePoint : ChargePointCommand;

/// <summary>Keep a connector for one token until the reservation expires.</summary>
public sealed record ReserveConnector : ChargePointCommand;

/// <summary>The driver no longer wants the reserved connector.</summary>
public sealed record CancelReservation : ChargePointCommand;

/// <summary>The reservation's time is up; sent by a timer, not by a person.</summary>
public sealed record ExpireReservation : ChargePointCommand;

/// <summary>A car has been plugged into the connector.</summary>
public sealed record OccupyConnector : ChargePointCommand;

/// <summary>The car has been unplugged; the connector can take the next one.</summary>
public sealed record ReleaseConnector : ChargePointCommand;

/// <summary>The charge point reports that a connector cannot be used.</summary>
public sealed record ReportConnectorFault : ChargePointCommand;

/// <summary>The connector has been repaired and can be used again.</summary>
public sealed record ClearConnectorFault : ChargePointCommand;
