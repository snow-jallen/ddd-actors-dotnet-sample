using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.DriverApp;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.Charging.ReadModels.SiteLiveBoard;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// Every projection's rules are pure functions (section 14.7), so they can be run over the
/// events Charging's actors really write, from the in-memory journal, with no database: the
/// session history's row, the driver app's card and the live board's connectors. Replaying the
/// events again, with the version check each projection makes first, changes nothing.
/// </summary>
public sealed class ProjectionRulesTests
{
    private const string DepotBays = "Salt Lake bus depot, public bays";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_whole_session_then_its_events_fold_into_its_history_row()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var events = await ChargeForHalfAnHourAsync(journal);

        events.Aggregate((SessionHistoryRow?)null, ApplyToHistory).ShouldBe(new SessionHistoryRow(
            Session.Value,
            "04A2B3C4D5E6F7",
            "OHM-SLC-0042/1",
            "ended",
            EnergyKwh: 11m,
            MeterStartWh: 1_200_000,
            StartedAt: Morning,
            EndedAt: Morning.AddMinutes(30),
            LastSequenceNr: 3));
    }

    [Fact]
    public async Task Given_a_whole_session_then_its_events_fold_into_its_driver_app_card()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var events = await ChargeForHalfAnHourAsync(journal);

        events.Aggregate((DriverAppCard?)null, ApplyToCard).ShouldBe(new DriverAppCard(
            "04A2B3C4D5E6F7",
            Session.Value,
            "ended",
            "OHM-SLC-0042/1",
            DepotBays,
            EnergyKwh: 11m,
            MeterStartWh: 1_200_000,
            StartedAt: Morning,
            EndedAt: Morning.AddMinutes(30),
            Version: 3));
    }

    [Fact]
    public async Task Given_a_charge_points_events_then_its_connectors_end_as_the_board_shows()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var chargePoint =
            journal.System.ActorOf(ChargePointActor.Props(Charger, new FakeTimeProvider(Morning)));
        await journal.AskAsync<CommandReply>(chargePoint, Register(Charger), Ct);
        await journal.AskAsync<CommandReply>(chargePoint, Reserve(Charger, 1), Ct);

        var board = (await CurrentAsync(journal, ChargingTags.Charging))
            .SelectMany(envelope => ConnectorStatusChange.Of((ChargePointEvent)envelope.Event))
            .GroupBy(change => change.ConnectorId)
            .Select(changes => (changes.Key, changes.Last().Status));

        board.ShouldBe([("OHM-SLC-0042/1", "reserved"), ("OHM-SLC-0042/2", "available")]);
    }

    [Fact]
    public async Task Given_every_event_again_then_neither_the_row_nor_the_card_changes()
    {
        await using var journal = await MemoryJournal.StartAsync(Ct);
        var events = await ChargeForHalfAnHourAsync(journal);
        var twice = events.Concat(events).ToList();

        twice.Aggregate((SessionHistoryRow?)null, ApplyToHistory)
            .ShouldBe(events.Aggregate((SessionHistoryRow?)null, ApplyToHistory));
        twice.Aggregate((DriverAppCard?)null, ApplyToCard)
            .ShouldBe(events.Aggregate((DriverAppCard?)null, ApplyToCard));
    }

    /// <summary>
    /// A session started, metered after 30 minutes and stopped, as the session actor writes it:
    /// its three events, in journal order.
    /// </summary>
    private static async Task<IReadOnlyList<EventEnvelope>> ChargeForHalfAnHourAsync(
        MemoryJournal journal)
    {
        var clock = new FakeTimeProvider(Morning);
        var session = journal.System.ActorOf(ChargingSessionActor.Props(Session, clock));
        await journal.AskAsync<CommandReply>(session, Begin(), Ct);
        clock.Advance(TimeSpan.FromMinutes(30));
        await journal.AskAsync<CommandReply>(
            session, new RecordMeterValue(Command, Session, 1_211_000, clock.GetUtcNow()), Ct);
        await journal.AskAsync<CommandReply>(session, new StopSession(Command, Session), Ct);
        return await CurrentAsync(journal, ChargingTags.ChargingSession);
    }

    /// <summary>What <see cref="SessionHistoryProjection"/> does, without its table.</summary>
    private static SessionHistoryRow? ApplyToHistory(SessionHistoryRow? row, EventEnvelope next) =>
        row?.LastSequenceNr >= next.SequenceNr
            ? row
            : SessionHistoryRow.Apply(row, (SessionEvent)next.Event, next.SequenceNr);

    /// <summary>What <see cref="DriverAppProjection"/> does, without its tables.</summary>
    private static DriverAppCard? ApplyToCard(DriverAppCard? card, EventEnvelope next) =>
        card?.Version >= next.SequenceNr
            ? card
            : DriverAppCard.Apply(card, (SessionEvent)next.Event, next.SequenceNr, DepotBays);

    private static async Task<IReadOnlyList<EventEnvelope>> CurrentAsync(
        MemoryJournal journal, string tag) =>
        await journal.ReadJournal.CurrentEventsByTag(tag, Offset.NoOffset())
            .RunWith(Sink.Seq<EventEnvelope>(), journal.System.Materializer());
}
