using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

/// <summary>
/// The shell, not the rules: given what is in the journal, what the actor replies and what it
/// adds to the journal. The rules themselves are the decider's tests. Every message, and every
/// event on its way to the test journal, goes through Charging's serializer.
/// </summary>
public sealed class ChargePointActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargePointActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario ChargePoint =>
        Shell(ChargePointActor.Props(Charger, _clock), ChargePointActor.PersistenceIdOf(Charger));

    [Fact]
    public void Every_message_is_serialized_on_its_way_and_props_are_not()
    {
        Sys.Settings.SerializeAllMessages.ShouldBeTrue();
        Sys.Settings.SerializeAllCreators.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_new_charge_point_when_registered_then_it_is_accepted_at_version_1() =>
        await ChargePoint.Given()
            .When(Register())
            .ThenAsync(new CommandAccepted(1), Registered());

    [Fact]
    public async Task Given_accepted_commands_then_each_version_counts_the_events_persisted()
    {
        var later = Morning.AddMinutes(16);
        _clock.SetUtcNow(later);

        // The lapsed reservation expires and the new one is made: two events in one decision.
        await ChargePoint.Given(Registered(), Reserved(1, DriversCard))
            .When(Reserve(1, SomeoneElsesCard))
            .ThenAsync(
                new CommandAccepted(4),
                Stamped(new ReservationExpired(Charger, 1, DriversCard), later),
                Reserved(1, SomeoneElsesCard, later));
    }

    [Fact]
    public async Task Given_a_reservation_when_someone_else_plugs_in_then_nothing_is_persisted() =>
        await ChargePoint.Given(Registered(), Reserved(1, DriversCard))
            .When(Occupy(1, SomeoneElsesCard))
            .ThenAsync<CommandRejected>(
                rejected => rejected.Code.ShouldBe("reserved-for-another-token"));

    [Fact]
    public async Task Given_a_command_that_changes_nothing_then_it_is_accepted_at_the_same_version()
    {
        await ChargePoint.Given(Registered())
            .When(Release(2))
            .ThenAsync(new CommandAccepted(1));
    }

    [Fact]
    public async Task Given_a_reservation_when_the_status_is_asked_then_it_shows_the_reservation()
    {
        await ChargePoint.Given(Registered(), Reserved(1, DriversCard))
            .When(Status())
            .ThenAsync(new ChargePointStatus(
                Charger,
                Version: 2,
                Depot,
                [
                    new ConnectorStatus(
                        1, new(22m), ConnectorStatus.Reserved, DriversCard, Morning.AddMinutes(15)),
                    new ConnectorStatus(2, new(22m), ConnectorStatus.Available),
                ]));
    }

    [Fact]
    public async Task Given_no_registration_when_the_status_is_asked_then_there_are_no_connectors()
    {
        await ChargePoint.Given()
            .When(Status())
            .ThenAsync(new ChargePointStatus(Charger, 0, SiteId: null, Connectors: []));
    }
}
