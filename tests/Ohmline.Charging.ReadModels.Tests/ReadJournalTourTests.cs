using System.Collections.Immutable;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.ReadModels.Query;
using Ohmline.Testing;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>
/// The four questions the read journal answers, asked of a SQLite journal that Charging's own
/// actors wrote (section 13.3). The <c>Current</c> queries are finite: they return what is
/// stored now, and complete. The others stay open and keep delivering.
/// </summary>
public sealed class ReadJournalTourTests
{
    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(10 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_two_charge_points_then_one_persistence_id_gives_its_events_in_order()
    {
        using var database = new TemporaryDatabase();
        await using var node = await StartWithTwoChargersAsync(database);

        var envelopes = await node.System.ReadJournal
            .CurrentEventsByPersistenceId("charge-point-OHM-SLC-0042", 0, long.MaxValue)
            .RunWith(Sink.Seq<EventEnvelope>(), node.System.Materializer());

        envelopes.Select(envelope => envelope.SequenceNr).ShouldBe([1, 2]);
        envelopes.ShouldAllBe(envelope => envelope.PersistenceId == "charge-point-OHM-SLC-0042");
        envelopes[0].Event.ShouldBeOfType<ChargePointRegistered>().ChargePointId.ShouldBe(Charger);
        envelopes[1].Event.ShouldBeOfType<ConnectorReserved>().ConnectorNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Given_a_stored_offset_then_events_by_tag_start_after_it()
    {
        using var database = new TemporaryDatabase();
        await using var node = await StartWithTwoChargersAsync(database);
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));

        var all = await CurrentByTagAsync(node, ChargingTags.Charging, Offset.NoOffset());
        var afterFirst = await CurrentByTagAsync(node, ChargingTags.Charging, Offset.Sequence(1));

        all.Select(OrderingOf).ShouldBe([1, 2, 3]); // The session's event is tagged otherwise.
        afterFirst.Select(OrderingOf).ShouldBe([2, 3]); // The offset itself is excluded.
        afterFirst.Select(envelope => (envelope.PersistenceId, envelope.SequenceNr)).ShouldBe(
        [
            ("charge-point-OHM-SLC-0043", 1),
            ("charge-point-OHM-SLC-0042", 2),
        ]);
        all.ShouldAllBe(envelope => envelope.Timestamp > 0);
    }

    [Fact]
    public async Task Given_a_live_query_then_it_delivers_an_event_stored_after_it_started()
    {
        using var database = new TemporaryDatabase();
        await using var node =
            await JournalNode.StartAsync(database, new FakeTimeProvider(Morning), Ct);
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(Charger), Ct))
            .ShouldBe(new CommandAccepted(1));

        var live = node.System.ReadJournal
            .EventsByTag(ChargingTags.Charging, Offset.NoOffset())
            .RunWith(Sink.Queue<EventEnvelope>(), node.System.Materializer());
        (await NextAsync(live)).Event.ShouldBeOfType<ChargePointRegistered>();

        (await node.AskAsync<ChargePointRegion, CommandReply>(Reserve(Charger, 1), Ct))
            .ShouldBe(new CommandAccepted(2));

        var next = await NextAsync(live);
        next.Event.ShouldBeOfType<ConnectorReserved>();
        OrderingOf(next).ShouldBe(2);
    }

    [Fact]
    public async Task Given_entities_of_three_kinds_then_current_persistence_ids_lists_each()
    {
        using var database = new TemporaryDatabase();
        await using var node = await StartWithTwoChargersAsync(database);
        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));

        var ids = await node.System.ReadJournal
            .CurrentPersistenceIds()
            .RunWith(Sink.Seq<string>(), node.System.Materializer());

        ids.ShouldBe(
            [
                "charge-point-OHM-SLC-0042",
                "charge-point-OHM-SLC-0043",
                $"charging-session-{Session}",
            ],
            ignoreOrder: true);
    }

    /// <summary>Registers OHM-SLC-0042, then OHM-SLC-0043, then reserves on OHM-SLC-0042.</summary>
    private static async Task<JournalNode> StartWithTwoChargersAsync(TemporaryDatabase database)
    {
        var node = await JournalNode.StartAsync(database, new FakeTimeProvider(Morning), Ct);
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(Charger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Register(NextCharger), Ct))
            .ShouldBe(new CommandAccepted(1));
        (await node.AskAsync<ChargePointRegion, CommandReply>(Reserve(Charger, 1), Ct))
            .ShouldBe(new CommandAccepted(2));
        return node;
    }

    /// <summary>
    /// Runs a finite query on the system's shared materializer. In Akka.NET 1.5 that one must
    /// never be disposed: every later stream of the system would fail.
    /// </summary>
    private static Task<IImmutableList<EventEnvelope>> CurrentByTagAsync(
        JournalNode node, string tag, Offset offset) =>
        node.System.ReadJournal
            .CurrentEventsByTag(tag, offset)
            .RunWith(Sink.Seq<EventEnvelope>(), node.System.Materializer());

    private static long OrderingOf(EventEnvelope envelope) =>
        envelope.Offset.ShouldBeOfType<Sequence>().Value;

    private static async Task<EventEnvelope> NextAsync(ISinkQueue<EventEnvelope> live)
    {
        var next = await live.PullAsync().WaitAsync(Patience, Ct);
        next.HasValue.ShouldBeTrue("The live query completed; it should stay open.");
        return next.Value;
    }
}
