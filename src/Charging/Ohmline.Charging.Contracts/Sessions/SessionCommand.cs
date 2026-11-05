using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Contracts.Sessions;

/// <summary>
/// Everything a charging session can be told, by the charging journey or, through the Device
/// Gateway, by the charge point. Every command names its session, which is how the shell routes
/// it, and carries the id its sender gave it. Meter registers are in watt-hours, as charge points
/// report them.
/// </summary>
public closed record SessionCommand(CommandId CommandId, SessionId SessionId);

/// <summary>
/// Start charging on a connector for a token, under the tariff snapshot taken for it; the spend
/// cap is in the tariff's currency. <paramref name="PhaseCount"/>, the phases the car charges
/// on, came later (section 20.2): optional, so a sender that does not know it yet still works.
/// </summary>
public sealed record StartSession(
    CommandId CommandId,
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    TariffSnapshot Tariff,
    Money SpendCap,
    long MeterStartWh,
    int? PhaseCount = null)
    : SessionCommand(CommandId, SessionId);

/// <summary>The charge point's energy register, read at a moment it reports.</summary>
public sealed record RecordMeterValue(
    CommandId CommandId, SessionId SessionId, long RegisterWh, DateTimeOffset MeasuredAt)
    : SessionCommand(CommandId, SessionId);

/// <summary>Charging paused while the car stays plugged in, and who paused it.</summary>
public sealed record SuspendCharging(CommandId CommandId, SessionId SessionId, SuspendedBy By)
    : SessionCommand(CommandId, SessionId);

/// <summary>Charging continues after a pause.</summary>
public sealed record ResumeCharging(CommandId CommandId, SessionId SessionId)
    : SessionCommand(CommandId, SessionId);

/// <summary>The session is over: the car was unplugged, or charging was stopped remotely.</summary>
public sealed record StopSession(CommandId CommandId, SessionId SessionId)
    : SessionCommand(CommandId, SessionId);
