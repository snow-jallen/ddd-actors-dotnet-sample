using System.Collections.Immutable;
using CsCheck;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using static Ohmline.Charging.Domain.Tests.Known;
using Gen = CsCheck.Gen;

namespace Ohmline.Charging.Domain.Tests.ChargePoints;

/// <summary>
/// What holds for a charge point whatever drivers, the gateway, timers and operators ask of it,
/// in any order and with minutes passing in between: two drivers, two connectors and a third
/// connector number that does not exist.
/// </summary>
public sealed class ChargePointProperties
{
    private static readonly RegisterChargePoint Registration =
        new(Command, Charger, Depot, [new(50m), new(22m)]);

    private static readonly Gen<int> Connectors = Gen.Int[1, 3];

    private static readonly Gen<(TokenUid Token, SessionId Session)> Drivers = Gen.OneOf(
        Gen.Const((DriversCard, Session)), Gen.Const((SomeoneElsesCard, NextSession)));

    private static readonly Gen<ChargePointCommand> Commands = Gen.OneOf<ChargePointCommand>(
        Gen.Select(Connectors, Drivers, (connector, driver) =>
            new ReserveConnector(Command, Charger, connector, driver.Token)),
        Gen.Select(Connectors, Drivers, (connector, driver) =>
            new CancelReservation(Command, Charger, connector, driver.Token)),
        Connectors.Select(connector => new ExpireReservation(Command, Charger, connector)),
        Gen.Select(Connectors, Drivers, (connector, driver) =>
            new OccupyConnector(Command, Charger, connector, driver.Session, driver.Token)),
        Connectors.Select(connector => new ReleaseConnector(Command, Charger, connector)),
        Connectors.Select(connector =>
            new ReportConnectorOutOfOrder(Command, Charger, connector, "GroundFailure")),
        Connectors.Select(connector => new ReportConnectorRepaired(Command, Charger, connector)),
        Connectors.Select(connector => new MakeConnectorUnavailable(Command, Charger, connector)),
        Connectors.Select(connector => new MakeConnectorAvailable(Command, Charger, connector)),
        Gen.Const(Registration));

    /// <summary>Registered first, then up to 40 commands, 0 to 10 minutes apart.</summary>
    private static readonly Gen<ImmutableArray<ChargePointChecks.Timed>> Scripts =
        ChargePointChecks.Scripts(
            Commands, Gen.Int[0, 10].Select(m => TimeSpan.FromMinutes(m)), 40, Registration);

    [Fact]
    public void Evolve_is_total_for_every_event_decide_produces()
    {
        ChargePointChecks.EvolveIsTotal(Scripts, Morning);
    }

    [Fact]
    public void Decide_never_records_an_event_that_evolve_ignores()
    {
        ChargePointChecks.EveryEventChangesTheState(Scripts, Morning);
    }

    [Fact]
    public void A_charge_point_keeps_the_connectors_it_was_registered_with()
    {
        ChargePointChecks.Invariant(
            Scripts,
            Morning,
            "Connectors 1 and 2, and no others",
            state => state.Value is not Registered chargePoint
                     || chargePoint.Connectors.Keys.Order().SequenceEqual([1, 2]));
    }

    [Fact]
    public void A_reservation_never_holds_a_connector_for_longer_than_the_hold()
    {
        ChargePointChecks.AfterEveryDecision(Scripts, Morning, step =>
        {
            if (step.After.Value is Registered chargePoint)
            {
                chargePoint.Connectors.Values.Select(connector => connector.State.Value)
                    .OfType<Reserved>()
                    .ShouldAllBe(held => held.ExpiresAt <= step.At + ChargePoint.ReservationHold);
            }
        });
    }
}
