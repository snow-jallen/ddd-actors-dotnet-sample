using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Testing;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// A journal row that cannot be read stops the charge point every time it recovers: the next
/// message starts it again, and it fails again. Here the row carries a manifest no version of
/// Charging ever wrote, as a newer node's event would after a rollback.
/// </summary>
public sealed class PoisonEventTests
{
    private const string Poison = "charging.connector-teleported.v1";

    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(5 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_unreadable_event_then_every_recovery_fails_and_says_which()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);
        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            (await node.AskAsync<CommandReply>(Register(), Ct)).ShouldBe(new CommandAccepted(1));
        }

        await AppendUnreadableEventAsync(database, sequenceNumber: 2);

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var errors = new TestProbe(restarted.System, new XunitAssertions());
        var replies = new TestProbe(restarted.System, new XunitAssertions());
        restarted.System.EventStream.Subscribe(errors.Ref, typeof(Error));

        await restarted.TellAsync(Status(), replies.Ref, Ct);
        var first = await errors.ExpectMsgAsync<Error>(Patience, cancellationToken: Ct);
        var second = await NextFailureAsync(restarted, errors, replies);

        foreach (var failure in new[] { first, second })
        {
            failure.Message.ToString().ShouldBe(
                "Recovery of charge-point-OHM-SLC-0042 failed after sequence number 0, on an " +
                $"event with the unknown manifest '{Poison}'. The actor stops; see " +
                "docs/runbooks/poison-event.md");
        }

        await replies.ExpectNoMsgAsync(TimeSpan.Zero, Ct); // Nobody got an answer.
    }

    /// <summary>
    /// Keeps asking, as callers would, until the charge point fails again. A message that
    /// reaches the stopped actor before its parent notices is a dead letter, so it may take more
    /// than one.
    /// </summary>
    private static async Task<Error> NextFailureAsync(
        SqliteNode node, TestProbe errors, TestProbe replies)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await node.TellAsync(Status(), replies.Ref, Ct);
            if (await errors.ReceiveOneAsync(TimeSpan.FromMilliseconds(200), Ct) is Error error)
            {
                return error;
            }
        }

        throw new TimeoutException("The charge point did not fail a second time.");
    }

    /// <summary>What no code path in Ohmline does: a row written by hand.</summary>
    private static async Task AppendUnreadableEventAsync(
        TemporaryDatabase database, long sequenceNumber)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(Ct);
        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO journal (persistence_id, sequence_number, created, deleted,
                                 manifest, identifier, writer_uuid, message)
            VALUES ('charge-point-OHM-SLC-0042', $sequenceNumber, $created, 0,
                    $manifest, 9101, 'by-hand', CAST('{}' AS BLOB))
            """;
        insert.Parameters.AddWithValue("$sequenceNumber", sequenceNumber);
        insert.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        insert.Parameters.AddWithValue("$manifest", Poison);
        await insert.ExecuteNonQueryAsync(Ct);
    }
}
