using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Charging.Actors.Tests.Sqlite;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using static Ohmline.Charging.Actors.Tests.Known;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.JournalTour;

/// <summary>
/// What happens if the one-writer rule is ever broken: two live actors with the same
/// persistence id, started here on purpose under two names. Both recover to the same sequence
/// number and both try to write the next one; the journal's unique index on (persistence_id,
/// sequence_number) refuses the second, whose actor stops. A safety net, not a strategy.
/// </summary>
public sealed class TwoIncarnationsTests(ITestOutputHelper output)
{
    /// <summary>
    /// The first answer waits for the node to start on its SQLite file, which takes seconds
    /// when every test assembly runs at once.
    /// </summary>
    private static readonly TimeSpan AskTimeout =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_two_writers_when_both_write_the_next_event_then_the_first_one_wins()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using var node = await SqliteNode.StartAsync(database, clock, Ct);
        var system = node.System;
        var first = system.ActorOf(ChargePointActor.Props(Charger, clock), "first");
        await AcceptAsync(first, Register(), 1);
        await AcceptAsync(first, Reserve(1, DriversCard), 2);

        var later = new FakeTimeProvider(Morning.AddMinutes(16));
        var second = system.ActorOf(ChargePointActor.Props(Charger, later), "second");
        (await second.Ask<ChargePointStatus>(Status(), AskTimeout, Ct)).Version.ShouldBe(2);

        var errors = Probe(system);
        system.EventStream.Subscribe(errors.Ref, typeof(Error));
        var deadLetters = Probe(system);
        system.EventStream.Subscribe(deadLetters.Ref, typeof(DeadLetter));
        var watcher = Probe(system);
        await watcher.WatchAsync(second);

        // Both are at sequence number 2. The first writes 3.
        await AcceptAsync(first, Reserve(2, DriversCard), 3);

        // The second decides from what it recovered: the reservation of connector 1 has lapsed,
        // so it writes ReservationExpired and ConnectorReserved as 3 and 4, in one batch.
        second.Tell(Reserve(1, SomeoneElsesCard), ActorRefs.NoSender);
        second.Tell(Status(), ActorRefs.NoSender); // Waits in the internal stash.

        var failure = await errors.FishForMessageAsync<Error>(
            error => $"{error.Message}".Contains("Failed to persist"), cancellationToken: Ct);
        output.WriteLine($"{failure.LogSource}: {failure.Message}");
        output.WriteLine($"Cause: {failure.Cause?.GetBaseException().Message}");
        await watcher.ExpectTerminatedAsync(second, cancellationToken: Ct);
        var lost = await deadLetters.FishForMessageAsync<DeadLetter>(
            letter => letter.Message is GetChargePointStatus, cancellationToken: Ct);
        output.WriteLine($"Dead letter: {lost.Message}");

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var rows = await JournalTourQueries.ReadJournalAsync(
            connection, ChargePointActor.PersistenceIdOf(Charger), Ct);
        foreach (var row in rows)
        {
            output.WriteLine(row.ToString());
        }

        rows.Select(row => row.SequenceNumber).ShouldBe([1, 2, 3]); // No 4: the batch failed.
        rows.Select(row => row.Manifest).ShouldBe(
        [
            "charging.charge-point-registered.v1",
            "charging.connector-reserved.v1",
            "charging.connector-reserved.v1",
        ]);
        rows.Select(row => row.WriterUuid).Distinct().ShouldHaveSingleItem();
        failure.Cause.ShouldNotBeNull().GetBaseException().ShouldBeOfType<SqliteException>()
            .SqliteErrorCode.ShouldBe(19); // SQLITE_CONSTRAINT

        var fresh = system.ActorOf(ChargePointActor.Props(Charger, later), "fresh");
        var status = await fresh.Ask<ChargePointStatus>(Status(), AskTimeout, Ct);
        status.Version.ShouldBe(3);
        status.Connectors.Select(connector => connector.ReservedFor)
            .ShouldBe([DriversCard, DriversCard]);
    }

    private static TestProbe Probe(ActorSystem system) => new(system, new XunitAssertions());

    private static async Task AcceptAsync(IActorRef chargePoint, object command, long version) =>
        (await chargePoint.Ask<CommandReply>(command, AskTimeout, Ct))
            .ShouldBe(new CommandAccepted(version));
}
