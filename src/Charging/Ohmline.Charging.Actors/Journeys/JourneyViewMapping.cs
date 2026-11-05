using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.SharedKernel.Identity;
using ChargingStep = Ohmline.Charging.Domain.Journeys.Charging;

namespace Ohmline.Charging.Actors.Journeys;

/// <summary>From the journey's state to the plain view that answers a question about it.</summary>
public static class JourneyViewMapping
{
    extension(JourneyState state)
    {
        public JourneyView ToView(JourneyId journeyId, long version)
        {
            JourneyView In(string step) =>
                new(journeyId, version, step, state.Terms?.SessionId);

            return state switch
            {
                NotStarted => In(JourneyView.NotStarted),
                AwaitingTokenLock => In(JourneyView.AwaitingTokenLock),
                AwaitingHold => In(JourneyView.AwaitingHold),
                AwaitingAllocation => In(JourneyView.AwaitingAllocation),
                AwaitingEnergyFlow => In(JourneyView.AwaitingEnergyFlow),
                ChargingStep => In(JourneyView.Charging),
                Settling => In(JourneyView.Settling),
                Completed => In(JourneyView.Completed),
                Abandoned abandoned => In(JourneyView.Abandoned) with
                {
                    AbandonReason = abandoned.Reason,
                    Detail = abandoned.Detail,
                },
                null => throw new InvalidOperationException("The journey's state is default."),
            };
        }
    }
}
