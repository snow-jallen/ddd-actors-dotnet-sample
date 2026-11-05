using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// WRONG ON PURPOSE (branch wrong/ch10-s06-persist-async-decider). With PersistAllAsync the
/// actor decides the next command before the previous decision's events are applied, so two
/// drivers can both be told yes for one connector.
/// </summary>
public sealed class PersistAsyncDeciderTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(PersistAsyncDeciderTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_two_drivers_both_occupy_one_connector()
    {
        var clock = new FakeTimeProvider(Morning);
        var chargePoint = Sys.ActorOf(ChargePointActor.Props(Charger, clock));
        chargePoint.Tell(Register(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        var slowJournal = HeldWrite<ConnectorOccupied>.Create();
        await Journal.OnWrite.SetInterceptorAsync(slowJournal);

        chargePoint.Tell(Occupy(1, DriversCard), TestActor);
        var otherSession = new SessionId(Guid.CreateVersion7(Morning.AddSeconds(1)));
        chargePoint.Tell(
            new OccupyConnector(Command, Charger, 1, otherSession, SomeoneElsesCard), TestActor);
        await slowJournal.Reached.WaitAsync(Dilated(TimeSpan.FromSeconds(3)), Ct);
        slowJournal.Release();

        // Both are accepted: the second was decided against a connector that still looked free.
        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);
        await ExpectMsgAsync(new CommandAccepted(3), cancellationToken: Ct);
    }
}
