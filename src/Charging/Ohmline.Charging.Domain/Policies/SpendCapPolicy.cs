using System.Collections.Immutable;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// Whenever the spend cap is reached, then stop charging. The session only records the fact;
/// stopping the charge point is someone else's job.
/// </summary>
public sealed class SpendCapPolicy : ISessionPolicy
{
    public ImmutableArray<ChargingIntent> React(SessionEvent @event) => @event switch
    {
        SpendCapReached reached => [new ChargingIntent.StopCharging(reached.SessionId)],
        _ => [],
    };
}
