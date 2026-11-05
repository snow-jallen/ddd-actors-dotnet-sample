using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.ActorShell.Sharding;
using Ohmline.Charging.Actors.Journeys;
using Ohmline.Charging.Actors.Tests.JournalTour;
using Ohmline.Charging.Actors.Tests.Journeys;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Journeys.TheJourney;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// A start the driver app sends again (section 19.3), against a real journal: the journey is
/// told the same <see cref="StartCharging"/> three times, and stopped and recovered from the
/// SQLite file between the second and the third, as after a passivation or a node's restart.
/// The journal holds one start, and every answer is the first one.
/// </summary>
public sealed class JourneySentTwiceTests(ITestOutputHelper output)
{
    /// <summary>The node makes its tables before the first answer (errata, section 18.4).</summary>
    private static readonly TimeSpan AskTimeout =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_same_start_three_times_and_a_restart_then_one_journey_started()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        var start = Start();

        var journey = Incarnation(node.System, clock, "first");
        var first = await journey.Ask<CommandReply>(start, AskTimeout, Ct);
        var second = await journey.Ask<CommandReply>(start, AskTimeout, Ct);
        await StopAsync(node.System, journey);
        var third = await Incarnation(node.System, clock, "recovered")
            .Ask<CommandReply>(start, AskTimeout, Ct);

        first.ShouldBe(new CommandAccepted(1));
        second.ShouldBe(first);
        third.ShouldBe(first);
        (await ManifestsAsync(database)).ShouldBe(["charging.journey-started.v1"]);
    }

    [Fact]
    public async Task Given_a_started_journey_when_another_command_starts_it_then_journey_exists()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        var journey = Incarnation(node.System, clock, "journey");
        await journey.Ask<CommandReply>(Start(), AskTimeout, Ct);

        var another = await journey.Ask<CommandReply>(Start(), AskTimeout, Ct);

        another.ShouldBeOfType<CommandRejected>().Code.ShouldBe(JourneyRefusals.JourneyExists);
        (await ManifestsAsync(database)).ShouldHaveSingleItem();
    }

    /// <summary>
    /// The journey, outside a shard region so that the test decides when it stops; Driver
    /// Accounts and Site Energy are probes that never answer, so it waits for its token lock.
    /// </summary>
    private static IActorRef Incarnation(ActorSystem system, TimeProvider clock, string name) =>
        system.ActorOf(
            ChargingJourneyActor.Props(
                Journey,
                clock,
                new Region<IdTokenRegion>(Probe(system).Ref),
                new Region<SitePowerBudgetRegion>(Probe(system).Ref),
                new StubPaymentPort(),
                new StubChargerPort()),
            name);

    private async Task StopAsync(ActorSystem system, IActorRef journey)
    {
        var watcher = Probe(system);
        await watcher.WatchAsync(journey);
        journey.Tell(Stop.Instance);
        await watcher.ExpectTerminatedAsync(journey, cancellationToken: Ct);
        output.WriteLine($"{journey.Path.Name} stopped; a new incarnation recovers from the file.");
    }

    private static async Task<IReadOnlyList<string>> ManifestsAsync(TemporaryDatabase database)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var rows = await JournalTourQueries.ReadJournalAsync(
            connection, ChargingJourneyActor.PersistenceIdOf(Journey), Ct);
        return [.. rows.Select(row => row.Manifest)];
    }

    private static TestProbe Probe(ActorSystem system) => new(system, new XunitAssertions());
}
