using System.Collections.Immutable;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// Whenever a session ends, then release its power allocation and unlock its token. The event
/// names the site, connector and token, so the reaction needs nothing else: a subscriber that
/// sees only the journal can run it (section 15.1).
/// </summary>
public sealed class ReleaseOnSessionEnded : ISessionPolicy
{
    public ImmutableArray<ChargingIntent> React(SessionEvent @event) => @event switch
    {
        SessionEnded ended =>
        [
            new ChargingIntent.ReleaseSiteAllocation(
                ended.SiteId, ended.ConnectorId, ended.SessionId),
            new ChargingIntent.UnlockToken(ended.TokenUid, ended.SessionId),
        ],
        _ => [],
    };
}
