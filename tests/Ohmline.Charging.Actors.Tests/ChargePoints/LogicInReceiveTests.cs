using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// WRONG ON PURPOSE (branch wrong/ch10-s10-logic-in-receive). The decider and the actor now
/// disagree about a lapsed reservation, and every decider test still passes.
/// </summary>
public sealed class LogicInReceiveTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(LogicInReceiveTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_a_lapsed_reservation_still_turns_the_next_driver_away()
    {
        var clock = new FakeTimeProvider(Morning);
        var chargePoint = Sys.ActorOf(ChargePointActor.Props(Charger, clock));
        chargePoint.Tell(Register(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        chargePoint.Tell(Reserve(1, DriversCard), TestActor);
        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        clock.Advance(TimeSpan.FromMinutes(16));

        chargePoint.Tell(Reserve(1, SomeoneElsesCard), TestActor);

        // The actor refuses; the decider, which nobody asked, would have accepted.
        (await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct))
            .Code.ShouldBe("reserved-for-another-token");
        var history = ChargePoint.Fold(
        [
            new ChargePointRegistered(Charger, Depot, [new(22m), new(22m)]),
            new ConnectorReserved(Charger, 1, DriversCard, Morning.AddMinutes(15)),
        ]);
        ChargePoint.Decide(history, Reserve(1, SomeoneElsesCard), DecisionContext.From(clock))
            .IsOk.ShouldBeTrue();
    }
}
