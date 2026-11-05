using System.Collections.Immutable;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.ChargePoints;

/// <summary>
/// Everything that can happen to a charge point, named as on the design-level EventStorming
/// board (<c>docs/eventstorming/design-level.md</c>). These are the charge point's own memory:
/// they stay inside Charging, and other contexts never reference them. They are written to the
/// journal, so they hold plain data: value objects and closed records, never a union. Every
/// event records the command that caused it and when it happened; <see cref="ChargePoint"/>
/// stamps both on every event of a decision.
/// </summary>
public closed record ChargePointEvent(ChargePointId ChargePointId)
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// The charge point joined the network with its connectors, numbered from 1 in order. Equal
/// when the values are equal: a record alone would compare the array by reference.
/// </summary>
public sealed record ChargePointRegistered(
    ChargePointId ChargePointId, SiteId SiteId, ImmutableArray<Power> ConnectorMaxPowers)
    : ChargePointEvent(ChargePointId)
{
    public bool Equals(ChargePointRegistered? other) =>
        other is not null
        && base.Equals(other)
        && SiteId == other.SiteId
        && ConnectorMaxPowers.SequenceEqual(other.ConnectorMaxPowers);

    public override int GetHashCode() =>
        HashCode.Combine(base.GetHashCode(), SiteId, ConnectorMaxPowers.Length);
}

/// <summary>Something happened to one connector of the charge point.</summary>
public closed record ConnectorEvent(ChargePointId ChargePointId, int ConnectorNumber)
    : ChargePointEvent(ChargePointId);

/// <summary>
/// The connector is held for one token until <paramref name="ExpiresAt"/>. The expiry time is
/// computed when the reservation is accepted and recorded here, so replaying the event later
/// gives the same expiry, whatever the clock says then.
/// </summary>
public sealed record ConnectorReserved(
    ChargePointId ChargePointId, int ConnectorNumber, TokenUid TokenUid, DateTimeOffset ExpiresAt)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ReservationCancelled(
    ChargePointId ChargePointId, int ConnectorNumber, TokenUid TokenUid)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ReservationExpired(
    ChargePointId ChargePointId, int ConnectorNumber, TokenUid TokenUid)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorOccupied(
    ChargePointId ChargePointId, int ConnectorNumber, SessionId SessionId, TokenUid TokenUid)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

/// <summary>The car was unplugged; the session that occupied the connector is named.</summary>
public sealed record ConnectorReleased(
    ChargePointId ChargePointId, int ConnectorNumber, SessionId SessionId)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorFaulted(
    ChargePointId ChargePointId, int ConnectorNumber, string ErrorCode)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorFaultCleared(ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorMadeUnavailable(ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorEvent(ChargePointId, ConnectorNumber);

public sealed record ConnectorMadeAvailable(ChargePointId ChargePointId, int ConnectorNumber)
    : ConnectorEvent(ChargePointId, ConnectorNumber);
