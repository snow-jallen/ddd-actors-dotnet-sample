using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.SessionHistory;
using Ohmline.Testing;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>
/// The projections as the node runs them (section 13.6): <c>AddChargingReadModels()</c> starts
/// the sharded daemon process, and nobody in the test starts a runner or creates a table.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollection.Name)]
public sealed class ProjectionDaemonTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_daemon_process_then_a_started_session_reaches_its_history()
    {
        using var journal = new TemporaryDatabase();
        await using var database = await postgres.CreateDatabaseAsync(Ct);
        await using var node = await JournalNode.StartAsync(
            journal, new FakeTimeProvider(Morning), Ct, readModels: database);

        (await node.AskAsync<ChargingSessionRegion, CommandReply>(Begin(), Ct))
            .ShouldBe(new CommandAccepted(1));

        var row = await EventuallyAsync(database);
        row.Status.ShouldBe("charging");
        row.LastSequenceNr.ShouldBe(1);
    }

    /// <summary>Reads the session's row until it is there; the daemon creates the table.</summary>
    private static async Task<SessionHistoryRow> EventuallyAsync(NpgsqlDataSource database)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            try
            {
                await using var connection = await database.OpenConnectionAsync(Ct);
                await using var transaction = await connection.BeginTransactionAsync(Ct);
                if (await SessionHistoryTable.FindAsync(transaction, Session.Value, Ct) is { } row)
                {
                    return row;
                }
            }
            catch (PostgresException missing) when (missing.SqlState == "42P01")
            {
                // The daemon has not created the table yet.
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The session's history row never appeared.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        }
    }
}
