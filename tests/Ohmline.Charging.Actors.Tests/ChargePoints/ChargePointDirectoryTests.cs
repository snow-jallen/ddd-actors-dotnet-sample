using Akka.Persistence.TestKit;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.ChargePoints;

public sealed class ChargePointDirectoryTests(ITestOutputHelper output)
    : PersistenceTestKit(
        ActorTestConfig.With(Serializers.Charging), nameof(ChargePointDirectoryTests), output)
{
    private static readonly ChargePointId OtherCharger = new("OHM-SLC-0043");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_two_charge_points_when_both_are_registered_then_each_has_its_own_actor()
    {
        var directory = Sys.ActorOf(ChargePointDirectory.Props(new FakeTimeProvider(Morning)));

        directory.Tell(Register(Charger), TestActor);
        directory.Tell(Register(OtherCharger), TestActor);

        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
        await ExpectMsgAsync(new CommandAccepted(1), cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_registered_charge_point_when_asked_via_the_directory_then_it_answers()
    {
        var directory = Sys.ActorOf(ChargePointDirectory.Props(new FakeTimeProvider(Morning)));
        directory.Tell(Register(Charger), TestActor);
        await ExpectMsgAsync<CommandAccepted>(cancellationToken: Ct);

        directory.Tell(Status(Charger), TestActor);
        var registered = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);
        directory.Tell(Status(OtherCharger), TestActor);
        var unknown = await ExpectMsgAsync<ChargePointStatus>(cancellationToken: Ct);

        registered.Version.ShouldBe(1);
        unknown.Connectors.ShouldBeEmpty();
    }
}
