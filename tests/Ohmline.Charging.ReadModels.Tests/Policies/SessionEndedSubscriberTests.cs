using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Policies;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Policies;

/// <summary>
/// The choreography's translation (section 15.1): a session's end, as Site Energy's and Driver
/// Accounts' commands, with ids that are the same however often the event is read.
/// </summary>
public sealed class SessionEndedSubscriberTests
{
    private static readonly CommandId Stop =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    private static readonly ConnectorId Connector = new(Charger, 1);

    private static readonly SessionEnded Ended =
        new SessionEnded(Session, Depot, Connector, DriversCard, new Energy(11m)) with
        {
            CommandId = Stop,
            OccurredAt = Morning.AddMinutes(30),
        };

    [Fact]
    public void Whenever_a_session_ends_then_site_energy_releases_and_driver_accounts_unlocks() =>
        SessionEndedSubscriber.CommandsFor(Ended).Select(command => command.Value).ShouldBe(
        [
            new ReleaseAllocation(
                CommandId.Derive(Stop, "release-allocation"), Depot, Connector, Session),
            new UnlockFromSession(CommandId.Derive(Stop, "unlock-token"), DriversCard, Session),
        ]);

    [Fact]
    public void Given_the_same_event_read_twice_then_its_commands_carry_the_same_ids() =>
        SessionEndedSubscriber.CommandsFor(Ended).Select(command => command.Value)
            .ShouldBe(SessionEndedSubscriber.CommandsFor(Ended).Select(command => command.Value));

    [Fact]
    public void Given_any_other_session_event_then_nothing_is_sent() =>
        SessionEndedSubscriber.CommandsFor(new ChargingSuspendedByEv(Session) with
            {
                CommandId = Stop,
            })
            .ShouldBeEmpty();
}
