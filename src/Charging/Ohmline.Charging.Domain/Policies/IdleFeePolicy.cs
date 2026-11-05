using System.Collections.Immutable;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// Whenever charging is suspended, then warn the driver when the idle fee will start. The fee
/// comes from the session's tariff: a policy configured by the Knowledge Level.
/// </summary>
public sealed record IdleFeePolicy(IdleFee Fee) : ISessionPolicy
{
    public ImmutableArray<ChargingIntent> React(SessionEvent @event) => @event switch
    {
        ChargingSuspended suspended =>
        [
            new ChargingIntent.WarnOfIdleFee(
                suspended.SessionId, suspended.OccurredAt + Fee.GracePeriod, Fee.PricePerMinute),
        ],
        _ => [],
    };
}
