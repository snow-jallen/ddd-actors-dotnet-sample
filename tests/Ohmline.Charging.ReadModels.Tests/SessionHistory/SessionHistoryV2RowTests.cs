using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.SessionHistory;

/// <summary>
/// The second version of the session history (section 20.9): the first version's rules, which
/// it reuses, plus the site and the phase count from the session's start.
/// </summary>
public sealed class SessionHistoryV2RowTests
{
    private static readonly SessionStarted Started = new(
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        Tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000,
        PhaseCount: 3) { OccurredAt = Morning };

    [Fact]
    public void Given_no_row_when_the_session_starts_then_it_has_the_site_and_the_phase_count()
    {
        var row = SessionHistoryV2Row.Apply(null, Started, sequenceNr: 1).ShouldNotBeNull();

        row.SiteId.ShouldBe("slc-depot");
        row.PhaseCount.ShouldBe(3);
        row.Session.ShouldBe(SessionHistoryRow.Apply(null, Started, sequenceNr: 1));
    }

    [Fact]
    public void Given_a_row_when_more_happens_then_the_first_version_s_rules_apply()
    {
        var started = SessionHistoryV2Row.Apply(null, Started, sequenceNr: 1);
        var recorded = new MeterValueRecorded(
            Session, Energy.FromWattHours(1_207_350), Morning.AddHours(1));

        var row = SessionHistoryV2Row.Apply(started, recorded, sequenceNr: 2).ShouldNotBeNull();

        row.Session.ShouldBe(
            SessionHistoryRow.Apply(started!.Session, recorded, sequenceNr: 2));
        row.SiteId.ShouldBe("slc-depot");
    }

    [Fact]
    public void Given_a_session_started_before_phase_counts_then_the_row_has_none()
    {
        SessionHistoryV2Row.Apply(null, Started with { PhaseCount = null }, sequenceNr: 1)
            .ShouldNotBeNull().PhaseCount.ShouldBeNull();
    }

    [Fact]
    public void Given_no_row_when_anything_but_the_start_happens_then_there_is_still_none()
    {
        var ended = new SessionEnded(
            Session, Depot, new ConnectorId(Charger, 1), DriversCard, new Energy(7.35m));

        SessionHistoryV2Row.Apply(null, ended, sequenceNr: 4).ShouldBeNull();
    }
}
