using Akka.Actor;
using Akka.Persistence.TestKit;
using Akka.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// A reservation expires when its time is up, with no one asking: the actor's own timer sends
/// <see cref="ExpireReservation"/>, and the decider decides it. Time is virtual on both sides:
/// the decider's clock is a <see cref="FakeTimeProvider"/>, the actors' timers run on Akka's
/// <see cref="TestScheduler"/>, and the test moves the two together.
/// </summary>
public sealed class ReservationExpiryTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(ActorTestConfig.VirtualTime, Serializers.Charging),
        nameof(ReservationExpiryTests),
        output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TestScheduler Scheduler => (TestScheduler)Sys.Scheduler;

    [Fact]
    public async Task Given_a_reservation_when_its_time_is_up_then_the_charge_point_expires_it()
    {
        var chargePoint = await ReservedAsync();

        Advance(ChargePoint.ReservationHold);

        var status = await StatusAsync(chargePoint);
        status.Version.ShouldBe(3);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Available);
        var events = await JournalProbe.EventsAsync<ChargePointEvent>(
            Sys, ChargePointActor.PersistenceIdOf(Charger), Ct);
        events[^1].ShouldBeOfType<ReservationExpired>().OccurredAt.ShouldBe(
            Morning + ChargePoint.ReservationHold);
    }

    [Fact]
    public async Task Given_a_reservation_when_its_time_is_not_up_then_it_still_holds()
    {
        var chargePoint = await ReservedAsync();

        Advance(ChargePoint.ReservationHold - TimeSpan.FromSeconds(1));

        var status = await StatusAsync(chargePoint);
        status.Version.ShouldBe(2);
        status.Connectors[0].ReservedFor.ShouldBe(DriversCard);
    }

    [Fact]
    public async Task Given_a_cancelled_reservation_when_its_time_would_be_up_then_nothing_happens()
    {
        var chargePoint = await ReservedAsync();
        await AcceptAsync(chargePoint, Cancel(1, DriversCard), version: 3);

        Advance(ChargePoint.ReservationHold);

        (await StatusAsync(chargePoint)).Version.ShouldBe(3);
    }

    [Fact]
    public async Task Given_a_reservation_when_the_charge_point_restarts_then_it_still_expires()
    {
        await StopAsync(await ReservedAsync());
        var restarted = Start();
        (await StatusAsync(restarted)).Version.ShouldBe(2); // Recovered, with its timer again.

        Advance(ChargePoint.ReservationHold);

        var status = await StatusAsync(restarted);
        status.Version.ShouldBe(3);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Available);
    }

    [Fact]
    public async Task Given_a_reservation_that_lapsed_while_stopped_when_recovered_then_it_expires()
    {
        await StopAsync(await ReservedAsync());
        _clock.Advance(TimeSpan.FromMinutes(20)); // No actor, no timer: nothing happens.

        var restarted = Start();
        (await StatusAsync(restarted)).Version.ShouldBe(2); // Recovered; its timer is due now.
        Scheduler.Advance(TimeSpan.Zero); // Virtual time: even "now" waits for the test.

        var status = await StatusAsync(restarted);
        status.Version.ShouldBe(3);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Available);
    }

    private IActorRef Start() => Sys.ActorOf(ChargePointActor.Props(Charger, _clock));

    /// <summary>A registered charge point with connector 1 reserved for 15 minutes.</summary>
    private async Task<IActorRef> ReservedAsync()
    {
        var chargePoint = Start();
        await AcceptAsync(chargePoint, Register(), version: 1);
        await AcceptAsync(chargePoint, Reserve(1, DriversCard), version: 2);
        return chargePoint;
    }

    /// <summary>The decider's clock and the actors' timers move together.</summary>
    private void Advance(TimeSpan by)
    {
        _clock.Advance(by);
        Scheduler.Advance(by);
    }

    private async Task AcceptAsync(IActorRef chargePoint, ChargePointCommand command, long version)
    {
        chargePoint.Tell(command, TestActor);
        await ExpectMsgAsync(new CommandAccepted(version), cancellationToken: Ct);
    }

    private async Task<ChargePointStatus> StatusAsync(IActorRef chargePoint)
    {
        chargePoint.Tell(Status(), TestActor);
        return await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
    }

    private async Task StopAsync(IActorRef actor)
    {
        await WatchAsync(actor);
        Sys.Stop(actor);
        await ExpectTerminatedAsync(actor, cancellationToken: Ct);
    }
}
