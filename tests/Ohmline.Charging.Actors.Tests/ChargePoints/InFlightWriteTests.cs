using Akka.Actor;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// What the actor does while a decision is being written: nothing else. Messages that arrive
/// meanwhile wait in Akka.Persistence's internal stash and are handled, in the order they
/// arrived, once the write and its callbacks are done.
/// </summary>
public sealed class InFlightWriteTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(InFlightWriteTests), output)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_write_in_flight_when_more_messages_arrive_then_they_wait_in_order()
    {
        var clock = new FakeTimeProvider(Morning);
        var chargePoint = Sys.ActorOf(ChargePointActor.Props(Charger, clock));
        chargePoint.Tell(Register(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        var slowJournal = HeldWrite<ConnectorReserved>.Create();
        await Journal.OnWrite.SetInterceptorAsync(slowJournal);

        chargePoint.Tell(Reserve(1, DriversCard), TestActor);
        await slowJournal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        chargePoint.Tell(Status(), TestActor);
        chargePoint.Tell(Occupy(1, SomeoneElsesCard), TestActor);

        await ExpectNoMsgAsync(Quiet, Ct);
        slowJournal.Release();

        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        var status = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        status.Version.ShouldBe(2);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Reserved);
        status.Connectors[0].ReservedFor.ShouldBe(DriversCard);
        var rejected = await ExpectMsgAsync<CommandRejected>(cancellationToken: Ct);
        rejected.Code.ShouldBe("reserved-for-another-token");
    }
}
