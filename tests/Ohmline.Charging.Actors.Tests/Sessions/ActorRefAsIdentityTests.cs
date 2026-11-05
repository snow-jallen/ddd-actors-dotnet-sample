using Akka.Event;
using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sessions;

/// <summary>
/// The mistake on this branch: a session that remembers its site's power budget as an actor
/// reference. Two probes stand in for two incarnations of the site's budget entity: the one that
/// ran when the session started, and the one a rebalance starts on another node.
/// </summary>
public sealed class ActorRefAsIdentityTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ActorRefAsIdentityTests), output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Demonstrates_the_remembered_site_actor_is_gone_when_the_session_ends()
    {
        var siteBefore = CreateTestProbe();
        var siteAfter = CreateTestProbe();
        var deadLetters = CreateTestProbe();
        Sys.EventStream.Subscribe(deadLetters.Ref, typeof(DeadLetter));
        var session =
            Sys.ActorOf(ChargingSessionActor.Props(Session, new FakeTimeProvider(Morning)));
        session.Tell(Begin(), TestActor);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        session.Tell(new ChargingSessionActor.UseSiteBudget(siteBefore.Ref), TestActor);

        // The site's shard moves to another node: its entity there is a new actor.
        await WatchAsync(siteBefore.Ref);
        Sys.Stop(siteBefore.Ref);
        await ExpectTerminatedAsync(siteBefore.Ref, cancellationToken: Ct);

        session.Tell(new StopSession(Command, Session), TestActor);
        await ExpectMsgAsync(new CommandAccepted(2), cancellationToken: Ct);

        var lost = await deadLetters.FishForMessageAsync<DeadLetter>(
            dead => dead.Message is ReleaseAllocation, cancellationToken: Ct);
        lost.Recipient.ShouldBe(siteBefore.Ref);
        // The site never hears that the session ended: its power stays allocated.
        await siteAfter.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), Ct);
    }

    private static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000);
}
