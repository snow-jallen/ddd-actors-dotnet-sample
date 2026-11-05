using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Contracts.Sessions;

/// <summary>
/// Everything a charging session can be told, by the charging journey or, through the Device
/// Gateway, by the charge point. Every command names its session, which is how the shell routes
/// it, and carries the id its sender gave it. Meter registers are in watt-hours, as charge points
/// report them.
/// </summary>
public closed record SessionCommand(CommandId CommandId, SessionId SessionId);

/// <summary>
/// Start charging on a connector for a token, under a tariff locked now. Until section 8.2 the
/// tariff is its id, its version and its energy price; the spend cap is in the same currency.
/// </summary>
public sealed record StartSession(
    CommandId CommandId,
    SessionId SessionId,
    SiteId SiteId,
    ConnectorId ConnectorId,
    TokenUid TokenUid,
    string TariffId,
    int TariffVersion,
    Money PricePerKwh,
    Money SpendCap,
    long MeterStartWh)
    : SessionCommand(CommandId, SessionId);

/// <summary>The charge point's energy register, read at a moment it reports.</summary>
public sealed record RecordMeterValue(
    CommandId CommandId, SessionId SessionId, long RegisterWh, DateTimeOffset MeasuredAt)
    : SessionCommand(CommandId, SessionId);

/// <summary>Charging paused while the car stays plugged in.</summary>
public sealed record SuspendCharging(CommandId CommandId, SessionId SessionId)
    : SessionCommand(CommandId, SessionId);

/// <summary>Charging continues after a pause.</summary>
public sealed record ResumeCharging(CommandId CommandId, SessionId SessionId)
    : SessionCommand(CommandId, SessionId);

/// <summary>The session is over: the car was unplugged, or charging was stopped remotely.</summary>
public sealed record StopSession(CommandId CommandId, SessionId SessionId)
    : SessionCommand(CommandId, SessionId);
