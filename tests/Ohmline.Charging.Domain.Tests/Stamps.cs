using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Sessions;

namespace Ohmline.Charging.Domain.Tests;

/// <summary>
/// An expected event as a decision records it: caused by <see cref="Known.Command"/>, at the
/// time given. <c>new ChargingResumed(Session).At(Later)</c> reads as the fact it is.
/// </summary>
internal static class Stamps
{
    extension(ChargePointEvent @event)
    {
        public ChargePointEvent At(DateTimeOffset time) =>
            @event with { CommandId = Known.Command, OccurredAt = time };
    }

    extension(SessionEvent @event)
    {
        public SessionEvent At(DateTimeOffset time) =>
            @event with { CommandId = Known.Command, OccurredAt = time };
    }
}
