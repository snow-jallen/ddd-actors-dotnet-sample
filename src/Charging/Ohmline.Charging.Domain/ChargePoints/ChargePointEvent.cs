namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// Everything that can happen to a charge point, named as on the design-level EventStorming
/// board (<c>docs/eventstorming/design-level.md</c>). These are the charge point's own memory:
/// they stay inside Charging, and other contexts never reference them.
/// </summary>
/// <remarks>The events carry no data yet; section 7.2 adds it.</remarks>
public closed record ChargePointEvent;

public sealed record ChargePointRegistered : ChargePointEvent;

public sealed record ConnectorReserved : ChargePointEvent;

public sealed record ReservationCancelled : ChargePointEvent;

public sealed record ReservationExpired : ChargePointEvent;

public sealed record ConnectorOccupied : ChargePointEvent;

public sealed record ConnectorReleased : ChargePointEvent;

public sealed record ConnectorFaulted : ChargePointEvent;

public sealed record ConnectorFaultCleared : ChargePointEvent;
