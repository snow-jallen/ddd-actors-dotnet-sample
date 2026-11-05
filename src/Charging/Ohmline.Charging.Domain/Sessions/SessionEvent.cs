using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// Everything that can happen to a charging session, named as on the design-level EventStorming
/// board. Written to the journal, so plain data only: no union, no domain state. Every event
/// records the command that caused it and when it happened; <see cref="ChargingSession"/> stamps
/// both on every event of a decision.
/// </summary>
public closed record SessionEvent(SessionId SessionId)
{
    /// <summary>The command that caused this event: its causation.</summary>
    public CommandId CommandId { get; init; }

    /// <summary>When it happened: the time of the decision, from its context.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// The session started, and its terms were locked: the whole tariff snapshot, not a reference
/// to the tariff, because a later version must never change this session; and the spend cap.
/// </summary>
/// <param name="PhaseCount">
/// The phases the car charges on, added in section 20.2. Optional, with a default, because
/// every session started before then was stored without it, and those events are read for ever.
/// </param>
public sealed record SessionStarted(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    TariffSnapshot Tariff,
    Money SpendCap,
    long MeterStartWh,
    int? PhaseCount = null)
    : SessionEvent(SessionId);

/// <summary>
/// A meter reading that follows the rules, in Wh as the charge point sent it. It has two times:
/// when the charge point measured it, and when Ohmline recorded it (<c>OccurredAt</c>).
/// </summary>
public sealed record MeterValueRecorded(
    SessionId SessionId, long RegisterWh, DateTimeOffset MeasuredAt)
    : SessionEvent(SessionId);

/// <summary>
/// A meter reading that breaks the rules: its register is lower than the last accepted one, or
/// it was taken before it. The charge point said it, so it is recorded, next to the last reading
/// that was accepted, for someone to investigate. It never changes the energy delivered.
/// </summary>
public sealed record MeterAnomalyDetected(
    SessionId SessionId,
    long RegisterWh,
    DateTimeOffset MeasuredAt,
    long LastRegisterWh,
    DateTimeOffset LastMeasuredAt)
    : SessionEvent(SessionId);

/// <summary>The car stopped drawing power while plugged in: the driver is idle from now.</summary>
public sealed record ChargingSuspendedByEv(SessionId SessionId) : SessionEvent(SessionId);

/// <summary>
/// The charge point stopped delivering power, at Ohmline's request: the driver is not idle.
/// </summary>
public sealed record ChargingSuspendedByCharger(SessionId SessionId) : SessionEvent(SessionId);

public sealed record ChargingResumed(SessionId SessionId) : SessionEvent(SessionId);

/// <summary>A meter value pushed the cost to the spend cap. Recorded once, never refused.</summary>
public sealed record SpendCapReached(SessionId SessionId, Money Cost, Money SpendCap)
    : SessionEvent(SessionId);

/// <summary>
/// The session is over. It names the site, connector and token so that whoever reacts to it
/// (section 8.5) needs nothing else, and the energy delivered as the session counted it.
/// </summary>
public sealed record SessionEnded(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    Energy EnergyDelivered)
    : SessionEvent(SessionId);
