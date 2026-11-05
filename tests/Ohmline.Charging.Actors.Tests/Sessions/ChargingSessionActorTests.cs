using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.Sessions;
using Ohmline.Charging.Actors.Tests.Serialization;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sessions;

/// <summary>
/// The session's shell: the same command path as the charge point's, through DeciderActor.
/// Only what the shell does is tested here; the session's rules are the decider's tests.
/// </summary>
public sealed class ChargingSessionActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargingSessionActorTests), output)
{
    private static readonly DateTimeOffset Later = Morning.AddMinutes(10);

    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario ChargingSession => Shell(
        ChargingSessionActor.Props(Session, _clock), ChargingSessionActor.PersistenceIdOf(Session));

    [Fact]
    public void The_persistence_id_is_the_durable_type_name_and_the_session_id() =>
        ChargingSessionActor.PersistenceIdOf(Session).ShouldBe($"charging-session-{Session}");

    [Fact]
    public async Task Given_no_session_when_it_starts_then_its_terms_are_recorded_at_version_1()
    {
        var begin = Begin();

        await ChargingSession.Given()
            .When(begin)
            .ThenAsync(new CommandAccepted(1), Started(by: begin.CommandId));
    }

    [Fact]
    public async Task Given_a_started_session_when_metered_then_the_reading_is_recorded()
    {
        var meter = new RecordMeterValue(Command, Session, 1_207_400, Later);

        await ChargingSession.Given(Started())
            .When(meter)
            .ThenAsync(new CommandAccepted(2), Metered(by: meter.CommandId));
    }

    [Fact]
    public async Task Given_a_metered_session_when_stopped_then_it_ends_with_the_energy_delivered()
    {
        _clock.SetUtcNow(Later);

        var stop = new StopSession(Command, Session);
        await ChargingSession.Given(Started(), Metered())
            .When(stop)
            .ThenAsync(
                new CommandAccepted(3),
                Stamped(
                    new SessionEnded(
                        Session, Depot, ChargingSamples.Connector, DriversCard, new Energy(7.4m)),
                    Later,
                    stop.CommandId));
    }

    [Fact]
    public async Task Given_a_started_session_when_it_is_started_again_then_it_is_rejected() =>
        await ChargingSession.Given(Started())
            .When(Begin())
            .ThenAsync<CommandRejected>(rejected => rejected.Code.ShouldBe("already-started"));

    [Fact]
    public async Task Given_no_session_when_a_meter_value_arrives_then_it_is_rejected() =>
        await ChargingSession.Given()
            .When(new RecordMeterValue(Command, Session, 1_207_400, Later))
            .ThenAsync<CommandRejected>(rejected => rejected.Code.ShouldBe("not-started"));

    [Fact]
    public async Task Given_an_ended_session_in_the_journal_then_the_new_incarnation_remembers() =>
        await ChargingSession.Given(
                Started(),
                Stamped(new SessionEnded(
                    Session, Depot, ChargingSamples.Connector, DriversCard, Energy.Zero)))
            .When(new SuspendCharging(Command, Session, new ByEv()))
            .ThenAsync<CommandRejected>(rejected => rejected.Code.ShouldBe("already-ended"));

    private static StartSession Begin() => new(
        Command,
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000);

    private static SessionEvent Started(CommandId? by = null) => Stamped(new SessionStarted(
        Session,
        Depot,
        ChargingSamples.Connector,
        DriversCard,
        ChargingSamples.Tariff,
        ChargingSamples.SpendCap,
        MeterStartWh: 1_200_000),
        by: by);

    private static SessionEvent Metered(CommandId? by = null) =>
        Stamped(new MeterValueRecorded(Session, Energy.FromWattHours(1_207_400), Later), by: by);

    private static SessionEvent Stamped(
        SessionEvent @event, DateTimeOffset? at = null, CommandId? by = null) =>
        @event with { CommandId = by ?? Command, OccurredAt = at ?? Morning };
}
