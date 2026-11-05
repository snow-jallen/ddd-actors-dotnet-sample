using Ohmline.Mobile.Core.Outbox;
using static Ohmline.Mobile.Core.Tests.Outbox.Commands;

namespace Ohmline.Mobile.Core.Tests.Outbox;

public sealed class SqliteOutboxTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_queued_command_when_the_database_is_opened_again_then_it_is_there()
    {
        using var database = new TemporaryDatabase();
        var command = StartCharging();
        await new SqliteOutbox(database.ConnectionString).EnqueueAsync(command, Morning, Ct);

        // The app was killed: a new process opens the same file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var reopened = new SqliteOutbox(database.ConnectionString);

        var row = (await reopened.ReadQueuedAsync(Ct)).ShouldHaveSingleItem();
        (row.CommandId, row.Kind, row.Payload, row.Status, row.Attempts)
            .ShouldBe((command.CommandId, OutboxKinds.StartCharging, command.Payload, "queued", 0));
        row.CreatedUtc.ShouldBe(Morning);
    }

    [Fact]
    public async Task Given_the_same_command_twice_then_it_is_queued_once()
    {
        using var database = new TemporaryDatabase();
        var outbox = new SqliteOutbox(database.ConnectionString);
        var command = StartCharging();

        var first = await outbox.EnqueueAsync(command, Morning, Ct);
        var second = await outbox.EnqueueAsync(command, Morning.AddSeconds(1), Ct);

        (first, second).ShouldBe((true, false));
        (await outbox.ReadQueuedAsync(Ct)).ShouldHaveSingleItem().CreatedUtc.ShouldBe(Morning);
    }

    [Fact]
    public async Task Queued_commands_are_read_in_the_order_they_were_given()
    {
        using var database = new TemporaryDatabase();
        var outbox = new SqliteOutbox(database.ConnectionString);
        var (first, second, third) = (StartCharging(), StartCharging(), StartCharging());
        foreach (var command in new[] { first, second, third })
        {
            await outbox.EnqueueAsync(command, Morning, Ct);
        }

        (await outbox.ReadQueuedAsync(Ct)).Select(row => row.CommandId)
            .ShouldBe([first.CommandId, second.CommandId, third.CommandId]);
    }

    [Fact]
    public async Task Given_commands_older_than_the_cutoff_then_only_they_fail_to_sync()
    {
        using var database = new TemporaryDatabase();
        var outbox = new SqliteOutbox(database.ConnectionString);
        var (old, recent) = (StartCharging(), StartCharging());
        await outbox.EnqueueAsync(old, Morning, Ct);
        await outbox.EnqueueAsync(recent, Morning.AddHours(2), Ct);

        var expired = await outbox.ExpireAsync(Morning.AddHours(1), Ct);

        expired.ShouldBe([old.CommandId]);
        (await outbox.FindAsync(old.CommandId, Ct))!.Status.ShouldBe(OutboxStatus.FailedToSync);
        (await outbox.ReadQueuedAsync(Ct)).ShouldHaveSingleItem().CommandId
            .ShouldBe(recent.CommandId);
    }
}
