using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// What a policy wants to happen next, in Charging's own words. An intent is not another
/// context's command: Charging does not know Site Energy's or Driver Accounts' commands, and the
/// shell translates each intent into one (section 15.1). A union, because intents stay in the
/// process: they are produced, translated and sent, never persisted (ADR 0003).
/// </summary>
public union ChargingIntent(
    ChargingIntent.ReleaseSiteAllocation,
    ChargingIntent.UnlockToken,
    ChargingIntent.StopCharging,
    ChargingIntent.WarnOfIdleFee)
{
    /// <summary>The site's power budget can give this connector's power to the others.</summary>
    public sealed record ReleaseSiteAllocation(
        SiteId SiteId, ConnectorId ConnectorId, SessionId SessionId);

    /// <summary>The token may start another session.</summary>
    public sealed record UnlockToken(TokenUid TokenUid, SessionId SessionId);

    /// <summary>The charge point should stop delivering energy to this session.</summary>
    public sealed record StopCharging(SessionId SessionId);

    /// <summary>Tell the driver when the idle fee will start and what it costs a minute.</summary>
    public sealed record WarnOfIdleFee(
        SessionId SessionId, DateTimeOffset StartsAt, Money PricePerMinute);
}
