using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// Everything that can happen to a charging session, named as on the design-level EventStorming
/// board. Written to the journal, so plain data only: no union, no domain state.
/// </summary>
public closed record SessionEvent(SessionId SessionId);

/// <summary>
/// The session started, and its terms were locked: the tariff (by id and version, with its
/// energy price, until section 8.2 records a whole tariff snapshot) and the spend cap.
/// </summary>
public sealed record SessionStarted(
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    string TariffId,
    int TariffVersion,
    Money PricePerKwh,
    Money SpendCap,
    long MeterStartWh,
    DateTimeOffset StartedAt)
    : SessionEvent(SessionId);

/// <summary>A meter reading that follows the rules, in Wh as the charge point sent it.</summary>
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

public sealed record ChargingSuspended(SessionId SessionId, DateTimeOffset SuspendedAt)
    : SessionEvent(SessionId);

public sealed record ChargingResumed(SessionId SessionId, DateTimeOffset ResumedAt)
    : SessionEvent(SessionId);

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
    Energy EnergyDelivered,
    DateTimeOffset EndedAt)
    : SessionEvent(SessionId);
