using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.Testing;
using Ohmline.Testing.Metrics;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Diagnostics;

/// <summary>
/// What Charging's actors record, and with which tags (section 18.1). Each test has a meter
/// factory of its own and listens only to the meters it made: tests run in parallel in one
/// process, and every one of them has a meter named <c>Ohmline.Charging</c>.
/// </summary>
public sealed class ChargingMetricsTests : PersistenceShellSpec
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly TestMeterFactory _meters = new();
    private readonly InstrumentRecorder _recorded;
    private readonly ChargingMetrics _metrics;

    public ChargingMetricsTests(ITestOutputHelper output)
        : base(ActorTestConfig.With(Serializers.Charging), nameof(ChargingMetricsTests), output)
    {
        _recorded = new InstrumentRecorder(_meters);
        _metrics = new ChargingMetrics(_meters);
    }

    [Fact]
    public async Task Given_a_refused_command_then_it_is_counted_and_refused_for_its_errors_case()
    {
        await Shell(
                ChargePointActor.Props(Charger, _clock, _metrics),
                ChargePointActor.PersistenceIdOf(Charger))
            .Given(Registered(), Reserved(1, DriversCard))
            .When(Occupy(1, SomeoneElsesCard))
            .ThenAsync<CommandRejected>(_ => { });

        var decided = _recorded.Of(ChargingMetrics.Commands).ShouldHaveSingleItem();
        decided.Value.ShouldBe(1);
        decided.Tags.ShouldBe(Tags(("aggregate.type", "charge-point"),
            ("aggregate.id", "charge-point-OHM-SLC-0042"),
            ("command.type", "OccupyConnector")), ignoreOrder: true);
        var refused = _recorded.Of(ChargingMetrics.CommandsRejected).ShouldHaveSingleItem();
        refused.Tags.ShouldBe(Tags(("aggregate.type", "charge-point"),
            ("aggregate.id", "charge-point-OHM-SLC-0042"),
            ("command.type", "OccupyConnector"), ("reason", "ReservedForAnotherToken")),
            ignoreOrder: true);
    }

    /// <summary>
    /// With an id among the tags, twenty charge points make twenty series of one measurement
    /// each, where main makes one series of twenty. Ohmline's thousands of charge points, its
    /// sessions by the million, and every series kept by the backend, for as long as it keeps
    /// metrics: the bill and the query time grow with the business.
    /// </summary>
    [Fact]
    public async Task Demonstrates_entity_ids_as_tags_make_a_series_per_charge_point()
    {
        for (var number = 1; number <= 20; number++)
        {
            var chargePoint = new ChargePointId($"OHM-SLC-{number:D4}");
            var actor = Sys.ActorOf(ChargePointActor.Props(chargePoint, _clock, _metrics));
            await actor.Ask<CommandAccepted>(Register(chargePoint), Patience, Ct);
        }

        var decided = _recorded.Of(ChargingMetrics.Commands);
        decided.Count.ShouldBe(20);
        decided.Select(measurement => Series(measurement.Tags)).Distinct().Count().ShouldBe(20);
    }

    [Fact]
    public async Task Given_a_session_that_starts_and_ends_then_it_counts_once_with_its_energy()
    {
        var session = Sys.ActorOf(ChargingSessionActor.Props(Session, _clock, _metrics));

        await session.Ask<CommandAccepted>(Begin(), Patience, Ct);
        await session.Ask<CommandAccepted>(
            new RecordMeterValue(Command, Session, 1_207_400, Morning.AddMinutes(10)),
            Patience,
            Ct);
        await session.Ask<CommandAccepted>(new StopSession(Command, Session), Patience, Ct);

        _recorded.Of(ChargingMetrics.SessionsStarted).ShouldHaveSingleItem().Value.ShouldBe(1);
        var delivered = _recorded.Of(ChargingMetrics.EnergyDelivered).ShouldHaveSingleItem();
        delivered.Value.ShouldBe(7.4);
        delivered.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_session_that_recovers_then_its_start_is_not_counted_again()
    {
        var session = Sys.ActorOf(ChargingSessionActor.Props(Session, _clock));
        await session.Ask<CommandAccepted>(Begin(), Patience, Ct);
        await StopAsync(session);

        var recovered = Sys.ActorOf(ChargingSessionActor.Props(Session, _clock, _metrics));
        await recovered.Ask<CommandRejected>(Begin(), Patience, Ct);

        _recorded.Of(ChargingMetrics.SessionsStarted).ShouldBeEmpty();
        _recorded.Of(ChargingMetrics.CommandsRejected).ShouldHaveSingleItem()
            .Tags["reason"].ShouldBe("AlreadyStarted");
    }

    private async Task StopAsync(IActorRef actor)
    {
        await WatchAsync(actor);
        Sys.Stop(actor);
        await ExpectTerminatedAsync(actor, cancellationToken: Ct);
    }

    private static Dictionary<string, string?> Tags(params (string Key, string Value)[] tags) =>
        tags.ToDictionary(tag => tag.Key, string? (tag) => tag.Value);

    private static string Series(IReadOnlyDictionary<string, string?> tags) =>
        string.Join(',', tags.OrderBy(tag => tag.Key).Select(tag => $"{tag.Key}={tag.Value}"));
}
