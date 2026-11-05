using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;
using static Ohmline.Charging.Actors.Tests.Known;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

public sealed class NodeRestartTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_reservation_when_the_node_restarts_then_the_reservation_is_kept()
    {
        using var database = new TemporaryDatabase();
        var clock = new FakeTimeProvider(Morning);

        await using (var node = await SqliteNode.StartAsync(database, clock, Ct))
        {
            (await node.AskAsync<CommandReply>(Register(), Ct)).ShouldBe(new CommandAccepted(1));
            (await node.AskAsync<CommandReply>(Reserve(1, DriversCard), Ct))
                .ShouldBe(new CommandAccepted(2));
        }

        await using var restarted = await SqliteNode.StartAsync(database, clock, Ct);
        var status = await restarted.AskAsync<ChargePointStatus>(Status(), Ct);

        status.Version.ShouldBe(2);
        status.Connectors[0].State.ShouldBe(ConnectorStatus.Reserved);
        status.Connectors[0].ReservedFor.ShouldBe(DriversCard);
    }
}
