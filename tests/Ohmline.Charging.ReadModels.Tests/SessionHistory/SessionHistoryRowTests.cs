using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.SessionHistory;

/// <summary>The session history's rules, without a database: the row after each event.</summary>
public sealed class SessionHistoryRowTests
{
    private static readonly DateTimeOffset Later = Morning.AddMinutes(42);

    private static readonly SessionStarted Started = new SessionStarted(
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        Tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000) { OccurredAt = Morning };

    private static readonly SessionHistoryRow Charging = new(
        Session.Value,
        "04A2B3C4D5E6F7",
        "OHM-SLC-0042/1",
        "charging",
        EnergyKwh: 0m,
        MeterStartWh: 1_200_000,
        StartedAt: Morning,
        EndedAt: null,
        LastSequenceNr: 1);

    public static TheoryData<SessionEvent, string> StatusChanges => new()
    {
        { new ChargingSuspendedByEv(Session), "suspended-by-ev" },
        { new ChargingSuspendedByCharger(Session), "suspended-by-charger" },
        { new ChargingResumed(Session), "charging" },
    };

    public static TheoryData<SessionEvent> EventsThatChangeNothingShown => new()
    {
        new MeterAnomalyDetected(Session, 1_100_000, Later, 1_200_000, Morning),
        new SpendCapReached(Session, new Money(40.10m, Currency.Usd), new Money(40m, Currency.Usd)),
        Started,
    };

    [Fact]
    public void Given_no_row_when_the_session_starts_then_it_is_charging_with_no_energy() =>
        SessionHistoryRow.Apply(null, Started, sequenceNr: 1).ShouldBe(Charging);

    [Fact]
    public void Given_a_row_when_a_meter_value_is_recorded_then_energy_counts_from_the_start()
    {
        var recorded = new MeterValueRecorded(Session, Energy.FromWattHours(1_207_350), Later);

        SessionHistoryRow.Apply(Charging, recorded, 2)
            .ShouldBe(Charging with { EnergyKwh = 7.35m, LastSequenceNr = 2 });
    }

    [Fact]
    public void Given_a_row_when_one_phase_s_register_is_recorded_then_the_energy_stays() =>
        SessionHistoryRow.Apply(
                Charging,
                new MeterValueRecorded(Session, Energy.FromWattHours(402_100), Later, "L1"),
                2)
            .ShouldBe(Charging with { LastSequenceNr = 2 });

    [Theory]
    [MemberData(nameof(StatusChanges))]
    public void Given_a_row_when_charging_pauses_or_resumes_then_the_status_says_so(
        SessionEvent @event, string status) =>
        SessionHistoryRow.Apply(Charging, @event, 2)
            .ShouldBe(Charging with { Status = status, LastSequenceNr = 2 });

    [Fact]
    public void Given_a_row_when_the_session_ends_then_it_shows_the_sessions_own_energy()
    {
        var ended = new SessionEnded(
            Session, Depot, new ConnectorId(Charger, 1), DriversCard, new Energy(63.35m))
        {
            OccurredAt = Later,
        };

        SessionHistoryRow.Apply(Charging, ended, 9).ShouldBe(
            Charging with
            {
                Status = "ended", EnergyKwh = 63.35m, EndedAt = Later, LastSequenceNr = 9,
            });
    }

    [Theory]
    [MemberData(nameof(EventsThatChangeNothingShown))]
    public void Given_a_row_when_an_event_changes_nothing_shown_then_only_the_version_moves(
        SessionEvent @event) =>
        SessionHistoryRow.Apply(Charging, @event, 2).ShouldBe(Charging with { LastSequenceNr = 2 });

    [Fact]
    public void Given_no_row_when_an_event_other_than_the_start_arrives_then_there_is_no_row() =>
        SessionHistoryRow.Apply(null, new ChargingResumed(Session), 2).ShouldBeNull();
}
