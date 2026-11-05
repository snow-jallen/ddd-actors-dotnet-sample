using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.Domain.Tests.Known;

namespace Ohmline.Charging.Domain.Tests.Sessions;

/// <summary>
/// The session's decision table, row by row. States are built the way recovery builds them:
/// by folding events.
/// </summary>
public sealed class ChargingSessionTests
{
    private static readonly ConnectorId Connector = new(Charger, 1);
    private static readonly TariffSnapshot Tariff = StandardTariff(0.50m);
    private static readonly Money Cap = new(10.00m, Currency.Usd);
    private static readonly DateTimeOffset Later = Morning.AddMinutes(30);

    private static readonly SessionStarted Started = new(
        Session, Depot, Connector, DriversCard, Tariff, Cap, 1_200_000)
    {
        CommandId = Command,
        OccurredAt = Morning,
    };

    /// <summary>One event of every type, as one session could record them.</summary>
    private static readonly ImmutableArray<SessionEvent> OneOfEach =
    [
        Started,
        Recorded(1_205_000),
        Anomaly(1_000),
        new ChargingSuspendedByCharger(Session).At(Morning.AddMinutes(12)),
        new ChargingSuspendedByEv(Session).At(Morning.AddMinutes(13)),
        new ChargingResumed(Session).At(Morning.AddMinutes(15)),
        new SpendCapReached(Session, new Money(10.50m, Currency.Usd), Cap).At(Morning),
        new SessionEnded(Session, Depot, Connector, DriversCard, new Energy(21m)).At(Later),
    ];

    // StartSession: the factory

    [Fact]
    public void Given_no_session_when_started_then_the_tariff_and_the_spend_cap_are_locked()
    {
        Decide([], Start()).ShouldAccept(Started);
    }

    [Fact]
    public void Given_no_session_when_started_with_a_spend_cap_of_zero_then_it_is_refused()
    {
        var nothing = Money.Zero(Currency.Usd);

        Decide([], Start(spendCap: nothing))
            .ShouldReject(new SessionError.SpendCapNotPositive(nothing));
    }

    [Fact]
    public void Given_no_session_when_the_spend_cap_is_in_another_currency_then_it_is_refused()
    {
        Decide([], Start(spendCap: new Money(40m, Currency.Eur)))
            .ShouldReject(new CurrencyMismatch(Currency.Usd, Currency.Eur));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("suspended by EV")]
    [InlineData("suspended by charger")]
    [InlineData("ended")]
    public void Given_a_session_that_has_started_when_started_again_then_it_is_refused(
        string state)
    {
        Decide(History(state), Start()).ShouldReject(new SessionError.AlreadyStarted(Session));
    }

    [Theory]
    [InlineData("meter value")]
    [InlineData("suspend")]
    [InlineData("resume")]
    [InlineData("stop")]
    public void Given_no_session_when_anything_but_a_start_arrives_then_it_has_not_started(
        string command)
    {
        SessionCommand early = command switch
        {
            "meter value" => new RecordMeterValue(Command, Session, 1_200_100, Morning),
            "suspend" => Suspend("EV"),
            "resume" => new ResumeCharging(Command, Session),
            "stop" => new StopSession(Command, Session),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        Decide([], early).ShouldReject(new SessionError.NotStarted(Session));
    }

    // RecordMeterValue: facts that cannot be refused

    [Theory]
    [InlineData("active")]
    [InlineData("suspended by EV")]
    [InlineData("suspended by charger")]
    public void Given_a_running_session_when_a_higher_register_arrives_then_it_is_recorded(
        string state)
    {
        Decide(History(state), new RecordMeterValue(Command, Session, 1_205_000, Later))
            .ShouldAccept(new MeterValueRecorded(Session, 1_205_000, Later).At(Morning));
    }

    [Fact]
    public void Given_a_running_session_when_the_register_goes_backwards_then_it_is_an_anomaly()
    {
        Decide([Started, Recorded(1_210_000)], new RecordMeterValue(Command, Session, 50, Later))
            .ShouldAccept(new MeterAnomalyDetected(
                Session, 50, Later, 1_210_000, Morning.AddMinutes(10)).At(Morning));
    }

    [Fact]
    public void Given_a_running_session_when_an_older_reading_arrives_late_then_it_is_an_anomaly()
    {
        var early = Morning.AddMinutes(5);

        Decide([Started, Recorded(1_210_000)], Value(1_211_000, minutes: 5))
            .ShouldAccept(new MeterAnomalyDetected(
                Session, 1_211_000, early, 1_210_000, Morning.AddMinutes(10)).At(Morning));
    }

    [Fact]
    public void Given_a_running_session_when_the_latest_reading_arrives_twice_then_nothing_changes()
    {
        Decide([Started, Recorded(1_210_000)], Value(1_210_000)).ShouldAccept();
    }

    [Fact]
    public void Given_an_ended_session_when_a_late_reading_arrives_then_nothing_changes()
    {
        Decide(History("ended"), new RecordMeterValue(Command, Session, 1_230_000, Later))
            .ShouldAccept();
    }

    [Fact]
    public void Given_a_reading_that_crosses_the_spend_cap_then_the_same_decision_records_the_cap()
    {
        // 20 kWh at USD 0.50 is the cap of USD 10.00.
        Decide([Started, Recorded(1_210_000)], Value(1_220_000, minutes: 40)).ShouldAccept(
            new MeterValueRecorded(Session, 1_220_000, Morning.AddMinutes(40)).At(Morning),
            new SpendCapReached(Session, new Money(10.00m, Currency.Usd), Cap).At(Morning));
    }

    [Fact]
    public void Given_a_time_of_use_tariff_then_the_cap_counts_each_kWh_at_the_highest_energy_rate()
    {
        // USD 1.00 to start, then 15 kWh at the peak rate of USD 0.60: the cap of USD 10.00.
        var peak = new TimeOfUseBand("peak", new TimeOnly(16, 0), new TimeOnly(21, 0));
        var timeOfUse = Tariff with
        {
            Components =
            [
                new EnergyRate(new Money(0.30m, Currency.Usd)),
                new EnergyRate(new Money(0.60m, Currency.Usd), peak),
                new SessionFee(new Money(1.00m, Currency.Usd)),
            ],
        };

        Decide([Started with { Tariff = timeOfUse }], Value(1_215_000)).ShouldAccept(
            new MeterValueRecorded(Session, 1_215_000, Morning.AddMinutes(10)).At(Morning),
            new SpendCapReached(Session, new Money(10.00m, Currency.Usd), Cap).At(Morning));
    }

    [Fact]
    public void Given_a_spend_cap_reached_before_when_energy_flows_on_then_it_is_not_reached_again()
    {
        Decide([Started, Recorded(1_220_000, minutes: 40)], Value(1_220_500, minutes: 41))
            .ShouldAccept(
                new MeterValueRecorded(Session, 1_220_500, Morning.AddMinutes(41)).At(Morning));
    }

    // SuspendCharging and ResumeCharging: who suspended the charge?

    [Theory]
    [InlineData("active", "EV")]
    [InlineData("suspended by charger", "EV")]
    [InlineData("active", "charger")]
    [InlineData("suspended by EV", "charger")]
    public void Given_a_running_session_when_suspended_then_the_event_says_who_suspended_it(
        string state, string by)
    {
        Decide(History(state), Suspend(by), at: Later).ShouldAccept(Suspension(by).At(Later));
    }

    [Theory]
    [InlineData("suspended by EV", "EV")]
    [InlineData("suspended by charger", "charger")]
    public void Given_a_suspended_session_when_the_same_side_suspends_it_again_then_nothing_changes(
        string state, string by)
    {
        Decide(History(state), Suspend(by)).ShouldAccept();
    }

    [Theory]
    [InlineData("suspended by EV")]
    [InlineData("suspended by charger")]
    public void Given_a_suspended_session_when_resumed_then_charging_continues(string state)
    {
        Decide(History(state), new ResumeCharging(Command, Session), at: Later)
            .ShouldAccept(new ChargingResumed(Session).At(Later));
    }

    [Fact]
    public void Given_an_active_session_when_resumed_then_nothing_changes()
    {
        Decide(History("active"), new ResumeCharging(Command, Session)).ShouldAccept();
    }

    [Fact]
    public void Given_an_ended_session_when_suspended_or_resumed_then_it_has_already_ended()
    {
        Decide(History("ended"), Suspend("EV"))
            .ShouldReject(new SessionError.AlreadyEnded(Session));
        Decide(History("ended"), new ResumeCharging(Command, Session))
            .ShouldReject(new SessionError.AlreadyEnded(Session));
    }

    [Fact]
    public void Given_a_curtailment_and_then_a_full_car_then_the_driver_is_idle_from_when_full()
    {
        var curtailed = Morning.AddMinutes(23);
        var full = Morning.AddMinutes(48);

        var state = ChargingSession.Fold(
        [
            Started,
            new ChargingSuspendedByCharger(Session).At(curtailed),
            new ChargingSuspendedByEv(Session).At(full),
        ]);

        state.Value.ShouldBeOfType<SuspendedByEv>().Since.ShouldBe(full);
    }

    // StopSession: idempotent by state

    [Theory]
    [InlineData("active")]
    [InlineData("suspended by EV")]
    [InlineData("suspended by charger")]
    public void Given_a_running_session_when_stopped_then_it_ends_with_the_energy_delivered(
        string state)
    {
        var stop = new StopSession(Command, Session);

        Decide([.. History(state), Recorded(1_227_600)], stop, at: Later)
            .ShouldAccept(new SessionEnded(
                Session, Depot, Connector, DriversCard, new Energy(27.6m)).At(Later));
    }

    [Fact]
    public void Given_an_ended_session_when_stopped_again_then_nothing_changes()
    {
        Decide(History("ended"), new StopSession(Command, Session)).ShouldAccept();
    }

    // Evolve and Fold

    [Fact]
    public void Given_one_event_of_every_type_then_the_tests_cover_the_whole_hierarchy()
    {
        var everyType = typeof(SessionEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SessionEvent)) && !type.IsAbstract);

        OneOfEach.Select(@event => @event.GetType()).ShouldBe(everyType, ignoreOrder: true);
    }

    [Fact]
    public void Given_one_event_of_every_type_when_folded_then_the_session_has_ended()
    {
        var terms = new SessionTerms(
            Session, Depot, Connector, DriversCard, Tariff, Cap);

        ChargingSession.Fold(OneOfEach).ShouldBe(new Ended(terms, new Energy(21m), Later));
    }

    [Fact]
    public void Given_an_anomaly_when_folded_then_energy_counts_on_from_the_last_good_reading()
    {
        var state = ChargingSession.Fold(
            [Started, Recorded(1_210_000), Anomaly(50), Recorded(1_215_000, minutes: 20)]);

        state.Value.ShouldBeOfType<Active>().Meter.Delivered.ShouldBe(new Energy(15m));
    }

    [Fact]
    public void Given_the_primers_charge_when_folded_then_it_delivered_27_point_6_kilowatt_hours()
    {
        var primer = Started with
        {
            Tariff = StandardTariff(0.42m),
            SpendCap = new Money(40.00m, Currency.Usd),
        };
        var ended = Morning.AddMinutes(73);

        ChargingSession.Fold(
            [
                primer,
                Recorded(1_215_000, minutes: 20),
                new ChargingSuspendedByEv(Session).At(Morning.AddMinutes(48)),
                Recorded(1_227_600, minutes: 60),
                new SessionEnded(Session, Depot, Connector, DriversCard, new(27.6m)).At(ended),
            ])
            .Value.ShouldBeOfType<Ended>().EnergyDelivered.ShouldBe(new Energy(27.6m));
    }

    [Fact]
    public void Given_a_default_state_when_deciding_then_the_null_arm_reports_the_bug()
    {
        Should.Throw<InvalidOperationException>(() => ChargingSession.Decide(
            default, new StopSession(Command, Session), new DecisionContext(Morning)));
    }

    private static Result<ImmutableArray<SessionEvent>, SessionError> Decide(
        IEnumerable<SessionEvent> history, SessionCommand command, DateTimeOffset? at = null) =>
        ChargingSession.Decide(
            ChargingSession.Fold(history), command, new DecisionContext(at ?? Morning));

    private static SessionEvent[] History(string state) => state switch
    {
        "active" => [Started],
        "suspended by EV" =>
            [Started, new ChargingSuspendedByEv(Session).At(Morning.AddMinutes(5))],
        "suspended by charger" =>
            [Started, new ChargingSuspendedByCharger(Session).At(Morning.AddMinutes(5))],
        "ended" =>
        [
            Started,
            Recorded(1_220_000),
            new SessionEnded(Session, Depot, Connector, DriversCard, new Energy(20m)).At(Later),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static StartSession Start(Money? spendCap = null) =>
        new(Command, Session, Depot, Connector, DriversCard, Tariff, spendCap ?? Cap, 1_200_000);

    private static SuspendCharging Suspend(string by) => new(Command, Session, by switch
    {
        "EV" => new ByEv(),
        "charger" => new ByCharger(),
        _ => throw new ArgumentOutOfRangeException(nameof(by)),
    });

    private static SessionEvent Suspension(string by) => by switch
    {
        "EV" => new ChargingSuspendedByEv(Session),
        "charger" => new ChargingSuspendedByCharger(Session),
        _ => throw new ArgumentOutOfRangeException(nameof(by)),
    };

    private static SessionEvent Recorded(long registerWh, int minutes = 10) =>
        new MeterValueRecorded(Session, registerWh, Morning.AddMinutes(minutes))
            .At(Morning.AddMinutes(minutes));

    private static RecordMeterValue Value(long registerWh, int minutes = 10) =>
        new(Command, Session, registerWh, Morning.AddMinutes(minutes));

    private static SessionEvent Anomaly(long registerWh) =>
        new MeterAnomalyDetected(
                Session, registerWh, Morning.AddMinutes(11), 1_210_000, Morning.AddMinutes(10))
            .At(Morning.AddMinutes(11));
}
