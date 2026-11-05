using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Data.Sqlite;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.Charging;
using Ohmline.Mobile.Core.Platform;
using Ohmline.Mobile.Core.ReadModels;

namespace Ohmline.IntegrationTests;

/// <summary>
/// The driver app's read side against the node as it runs (section 19.7): the app's own
/// <see cref="LiveUpdatesClient"/>, connected to the node's hub, catches up from the feed when
/// it starts and then follows the pushes into its SQLite cache.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class DriverAppLiveTests(PostgresFixture postgres) : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly string _database =
        Path.Combine(Path.GetTempPath(), $"ohmline-driver-itg-{Guid.NewGuid():N}.db3");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_session_then_the_apps_cache_catches_up_and_follows_its_pushes()
    {
        await using var node = new OhmlineNode(postgres);
        var http = node.CreateClient();
        var (_, token, session) = await DriverAppTests.ChargeAsync(node, http, postgres);
        var cache = new SqliteReadModelCache($"Data Source={_database}");
        var hub = LiveUpdatesClient.Connect(node.Server.BaseAddress, options =>
        {
            options.HttpMessageHandlerFactory = _ => node.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling; // The test server has no sockets.
        });
        await using var live = new LiveUpdatesClient(
            hub,
            new DriverToken(token),
            cache,
            new FeedCatchUp(new ChargingApiClient(http), cache),
            new Inline(),
            TimeProvider.System);

        await live.StartAsync(Ct);
        var caughtUp = (await cache.ReadCardsAsync(token, Ct)).ShouldHaveSingleItem();
        (await http.PostAsync($"/sessions/{session}/stop", content: null, Ct))
            .EnsureSuccessStatusCode();
        var pushed = await EventuallyAsync(async () =>
            (await cache.ReadCardsAsync(token, Ct)).Single() is { Version: 2 } card ? card : null);

        (caughtUp.SessionId, caughtUp.Version, caughtUp.Status).ShouldBe((session, 1, "charging"));
        pushed.Status.ShouldBe("ended");
        pushed.EndedAt.ShouldNotBeNull();
        (await cache.ReadPositionAsync(token, Ct)).ShouldBeGreaterThan(0);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_database + suffix);
        }
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> read)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            if (await read() is { } value)
            {
                return value;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The app's cache did not get the push in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    private sealed class Inline : IAppDispatcher
    {
        public void Post(Action action) => action();
    }
}
