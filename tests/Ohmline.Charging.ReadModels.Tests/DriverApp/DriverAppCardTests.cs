using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.DriverApp;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.DriverApp;

/// <summary>The driver app card's rules, without a database: the card after each event.</summary>
public sealed class DriverAppCardTests
{
    private const string DepotBays = "Salt Lake bus depot, public bays";

    private static readonly DateTimeOffset Later = Morning.AddMinutes(42);

    /// <summary>Started at the depot's local time, which the card turns into UTC.</summary>
    private static readonly SessionStarted Started = new SessionStarted(
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        Tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000) { OccurredAt = Morning.ToOffset(TimeSpan.FromHours(-7)) };

    private static readonly DriverAppCard Charging = new(
        "04A2B3C4D5E6F7",
        Session.Value,
        "charging",
        "OHM-SLC-0042/1",
        DepotBays,
        EnergyKwh: 0m,
        MeterStartWh: 1_200_000,
        StartedAt: Morning,
        EndedAt: null,
        Version: 1);

    public static TheoryData<SessionEvent, string> StatusChanges => new()
    {
        { new ChargingSuspendedByEv(Session), "paused-by-car" },
        { new ChargingSuspendedByCharger(Session), "paused-by-charger" },
        { new ChargingResumed(Session), "charging" },
    };

    public static TheoryData<SessionEvent> EventsThatChangeNothingShown => new()
    {
        new MeterAnomalyDetected(Session, 1_100_000, Later, 1_200_000, Morning),
        new SpendCapReached(Session, new Money(40.10m, Currency.Usd), new Money(40m, Currency.Usd)),
        Started,
    };

    [Fact]
    public void Given_no_card_when_the_session_starts_then_it_shows_the_charge_points_name()
    {
        var card = DriverAppCard.Apply(null, Started, sequenceNr: 1, DepotBays);

        card.ShouldBe(Charging);
        card!.StartedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Given_a_start_to_the_tick_then_the_card_keeps_it_to_the_microsecond()
    {
        var precise = Started with { OccurredAt = Morning.AddTicks(1_234_567) };

        DriverAppCard.Apply(null, precise, sequenceNr: 1, DepotBays)!.StartedAt
            .ShouldBe(Morning.AddTicks(1_234_560));
    }

    [Fact]
    public void Given_no_name_yet_when_the_session_starts_then_the_card_has_none() =>
        DriverAppCard.Apply(null, Started, sequenceNr: 1, displayName: null)
            .ShouldBe(Charging with { ChargePointDisplayName = null });

    [Fact]
    public void Given_a_card_without_a_name_when_the_next_event_brings_one_then_it_is_filled_in()
    {
        var nameless = Charging with { ChargePointDisplayName = null };

        DriverAppCard.Apply(nameless, new ChargingResumed(Session), 2, DepotBays)
            .ShouldBe(Charging with { Version = 2 });
    }

    [Fact]
    public void Given_a_named_card_when_the_listing_is_renamed_then_it_keeps_its_first_name() =>
        DriverAppCard.Apply(Charging, new ChargingResumed(Session), 2, "Depot, bays 1-6")
            .ShouldBe(Charging with { Version = 2 });

    [Fact]
    public void Given_a_card_when_a_meter_value_is_recorded_then_energy_counts_from_the_start() =>
        DriverAppCard.Apply(Charging, new MeterValueRecorded(Session, 1_207_350, Later), 2, null)
            .ShouldBe(Charging with { EnergyKwh = 7.35m, Version = 2 });

    [Theory]
    [MemberData(nameof(StatusChanges))]
    public void Given_a_card_when_charging_pauses_or_resumes_then_it_says_so_in_drivers_words(
        SessionEvent @event, string status) =>
        DriverAppCard.Apply(Charging, @event, 2, null)
            .ShouldBe(Charging with { Status = status, Version = 2 });

    [Fact]
    public void Given_a_card_when_the_session_ends_then_it_shows_the_sessions_own_energy()
    {
        var ended = new SessionEnded(
            Session, Depot, new ConnectorId(Charger, 1), DriversCard, new Energy(63.35m))
        {
            OccurredAt = Later.ToOffset(TimeSpan.FromHours(-7)),
        };

        DriverAppCard.Apply(Charging, ended, 3, null).ShouldBe(Charging with
        {
            Status = "ended",
            EnergyKwh = 63.35m,
            EndedAt = Later,
            Version = 3,
        });
    }

    [Theory]
    [MemberData(nameof(EventsThatChangeNothingShown))]
    public void Given_a_card_when_an_event_changes_nothing_shown_then_only_the_version_moves(
        SessionEvent @event) =>
        DriverAppCard.Apply(Charging, @event, 2, null).ShouldBe(Charging with { Version = 2 });

    [Fact]
    public void Given_no_card_when_an_event_other_than_the_start_arrives_then_there_is_none() =>
        DriverAppCard.Apply(null, new ChargingResumed(Session), 2, DepotBays).ShouldBeNull();
}
