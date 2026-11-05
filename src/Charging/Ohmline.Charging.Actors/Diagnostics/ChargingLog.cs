using Microsoft.Extensions.Logging;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Diagnostics;

/// <summary>
/// Charging's log messages, each written once (section 18.4): the source generator makes a
/// method per message that checks the level before it formats anything, boxes nothing, and
/// gives every message an id and named properties a backend can query, such as
/// <c>JourneyId</c>. Ids, which no metric may carry, are welcome here.
/// </summary>
/// <remarks>
/// Written through <see cref="ILogger"/> at the moment of the call, so a message logged while a
/// span is current, as in a journey's <c>apply</c> span, carries that span's trace without
/// further help.
/// </remarks>
internal static partial class ChargingLog
{
    [LoggerMessage(1001, LogLevel.Information,
        "RemoteStart {Key} on {Connector} for {Token}, session {Session}, at most {Limit}")]
    public static partial void RemoteStartLogged(
        this ILogger log,
        IdempotencyKey key,
        ConnectorId connector,
        TokenUid token,
        SessionId session,
        Power limit);

    [LoggerMessage(1002, LogLevel.Information, "RemoteStop on {Connector}, session {Session}")]
    public static partial void RemoteStopLogged(
        this ILogger log, ConnectorId connector, SessionId session);

    [LoggerMessage(1003, LogLevel.Information,
        "SetChargingProfile on {Connector}: at most {Limit} (allocation {Version})")]
    public static partial void ChargingProfileLogged(
        this ILogger log, ConnectorId connector, Power limit, long version);

    [LoggerMessage(1004, LogLevel.Information, "Held {Amount} for {Driver} as {HoldId} ({Key})")]
    public static partial void HoldPlaced(
        this ILogger log, Money amount, DriverRef driver, string holdId, IdempotencyKey key);

    [LoggerMessage(1005, LogLevel.Information, "Voided {HoldId} ({Key})")]
    public static partial void HoldVoided(this ILogger log, string holdId, IdempotencyKey key);

    [LoggerMessage(1006, LogLevel.Information, "Captured {Amount} from {HoldId}")]
    public static partial void Captured(this ILogger log, Money amount, string holdId);

    [LoggerMessage(1011, LogLevel.Information,
        "Journey {JourneyId} abandoned ({Reason}): {Detail}")]
    public static partial void JourneyAbandoned(
        this ILogger log, JourneyId journeyId, string reason, string detail);

    [LoggerMessage(1012, LogLevel.Warning,
        "Journey {JourneyId}: capture attempt {Attempt} failed ({Code}); it is tried again later")]
    public static partial void CaptureAttemptFailed(
        this ILogger log, JourneyId journeyId, int attempt, string code);

    [LoggerMessage(1013, LogLevel.Warning,
        "Journey {JourneyId}: {Amount} recorded as owed. {Reason}")]
    public static partial void DebtRecorded(
        this ILogger log, JourneyId journeyId, Money amount, string reason);

    [LoggerMessage(1014, LogLevel.Warning,
        "Journey {JourneyId}: a port failed; the journey waits for its timer")]
    public static partial void PortFailed(this ILogger log, JourneyId journeyId, Exception cause);
}
