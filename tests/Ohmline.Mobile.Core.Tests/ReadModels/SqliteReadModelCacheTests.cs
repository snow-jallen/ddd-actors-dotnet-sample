using Ohmline.Mobile.Core.ReadModels;
using static Ohmline.Mobile.Core.Tests.ReadModels.Cards;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

public sealed class SqliteReadModelCacheTests : IDisposable
{
    private readonly TemporaryDatabase _database = new();
    private readonly SqliteReadModelCache _cache;

    public SqliteReadModelCacheTests() => _cache = new(_database.ConnectionString);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_newer_version_then_the_card_is_replaced()
    {
        await _cache.ApplyAsync(At(1), position: 1, Ct);

        var kept = await _cache.ApplyAsync(At(2, energyKwh: 7.35m), position: 2, Ct);

        kept.ShouldBeTrue();
        (await _cache.ReadCardsAsync(Token, Ct)).ShouldHaveSingleItem()
            .ShouldBe(At(2, energyKwh: 7.35m));
    }

    [Fact]
    public async Task Given_a_push_that_arrives_late_or_twice_then_nothing_changes()
    {
        await _cache.ApplyAsync(At(3, energyKwh: 12m, ended: true), position: 3, Ct);

        var late = await _cache.ApplyAsync(At(2, energyKwh: 7.35m), position: 2, Ct);
        var twice = await _cache.ApplyAsync(At(3, energyKwh: 12m, ended: true), position: 3, Ct);

        (late, twice).ShouldBe((false, false));
        (await _cache.ReadCardsAsync(Token, Ct)).ShouldHaveSingleItem()
            .ShouldBe(At(3, energyKwh: 12m, ended: true));
        (await _cache.ReadPositionAsync(Token, Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task Given_no_entry_yet_then_the_feed_is_read_from_the_start()
    {
        (await _cache.ReadPositionAsync(Token, Ct)).ShouldBe(0);
    }

    public void Dispose() => _database.Dispose();
}
