using System.Net;
using System.Text.Json;
using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Mobile.Core.Api;
using Ohmline.Mobile.Core.ReadModels;
using static Ohmline.Mobile.Core.Tests.ReadModels.Cards;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

public sealed class FeedCatchUpTests : IDisposable
{
    private readonly TemporaryDatabase _database = new();
    private readonly FakeServer _server = new();
    private readonly SqliteReadModelCache _cache;
    private readonly FeedCatchUp _catchUp;

    public FeedCatchUpTests()
    {
        _cache = new SqliteReadModelCache(_database.ConnectionString);
        _catchUp = new FeedCatchUp(new ChargingApiClient(_server.Client()), _cache);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_reconnect_then_only_what_was_missed_is_read_and_applied()
    {
        _server.Answers(HttpStatusCode.OK, Page(Entry(1, At(1)), Entry(2, At(2, 7.35m))))
            .Answers(HttpStatusCode.OK, Page())
            // The phone was away while the session ended; no push reached it.
            .Answers(HttpStatusCode.OK, Page(Entry(5, At(3, 12m, ended: true))))
            .Answers(HttpStatusCode.OK, Page());

        var first = await _catchUp.CatchUpAsync(Token, Ct);
        var afterReconnect = await _catchUp.CatchUpAsync(Token, Ct);

        (first, afterReconnect).ShouldBe((2, 1));
        _server.Requests.Select(request => request.Path).ShouldBe(
        [
            $"/app/v1/tokens/{Token}/feed?after=0",
            $"/app/v1/tokens/{Token}/feed?after=2",
            $"/app/v1/tokens/{Token}/feed?after=2",
            $"/app/v1/tokens/{Token}/feed?after=5",
        ]);
        (await _cache.ReadCardsAsync(Token, Ct)).ShouldHaveSingleItem()
            .ShouldBe(At(3, 12m, ended: true));
    }

    public void Dispose() => _database.Dispose();

    private static DriverFeedEntry Entry(long position, DriverSessionCard card) =>
        new(position, card);

    private static string Page(params DriverFeedEntry[] entries) => JsonSerializer.Serialize(
        new DriverFeedPage(entries, entries is [.., var last] ? last.Position : 0),
        ChargingClientJsonContext.Default.DriverFeedPage);
}
