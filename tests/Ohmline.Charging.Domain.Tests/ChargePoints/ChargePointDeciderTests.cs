using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Deciders;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>The charge point seen only through the shape every aggregate shares.</summary>
public sealed class ChargePointDeciderTests
{
    [Fact]
    public void Given_the_charge_point_as_any_decider_then_it_starts_unregistered()
    {
        InitialOf<ChargePoint, ChargePointState, ChargePointCommand, ChargePointEvent,
            ChargePointError>().ShouldBe(ChargePointState.Initial);
    }

    [Fact]
    public void Given_a_history_when_folded_as_any_decider_then_it_is_the_charge_points_own_fold()
    {
        ChargePointEvent[] history =
        [
            new ChargePointRegistered(Charger, Depot, [new(50m)]),
            new ConnectorOccupied(Charger, 1, Session, DriversCard),
        ];

        Decider.Fold<ChargePoint, ChargePointState, ChargePointCommand, ChargePointEvent,
            ChargePointError>(history).ShouldBe(ChargePoint.Fold(history));
    }

    private static TState InitialOf<TDecider, TState, TCommand, TEvent, TError>()
        where TDecider : IDecider<TState, TCommand, TEvent, TError> => TDecider.Initial;
}
