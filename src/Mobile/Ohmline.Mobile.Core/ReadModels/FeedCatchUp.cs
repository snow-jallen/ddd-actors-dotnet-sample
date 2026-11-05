using Ohmline.Mobile.Core.Api;

namespace Ohmline.Mobile.Core.ReadModels;

/// <summary>
/// Reads what the app missed from the token's feed, <c>GET /app/v1/tokens/{uid}/feed?after=</c>,
/// from the position the cache has reached, page by page, into the cache (section 19.7).
/// SignalR does not replay what was pushed while the phone was away, so the app pulls after
/// every start, reconnect and resume; the pushes it did get are applied again, harmlessly.
/// </summary>
public sealed class FeedCatchUp(ChargingApiClient api, SqliteReadModelCache cache)
{
    /// <summary>Applies every entry after the cached position; returns how many it read.</summary>
    public async Task<int> CatchUpAsync(string tokenUid, CancellationToken cancellationToken)
    {
        var read = 0;
        while (true)
        {
            var after = await cache.ReadPositionAsync(tokenUid, cancellationToken);
            var page = await api.GetFeedAsync(tokenUid, after, cancellationToken);
            if (page.Entries.Count == 0)
            {
                return read;
            }

            foreach (var entry in page.Entries)
            {
                await cache.ApplyAsync(entry.Card, entry.Position, cancellationToken);
            }

            read += page.Entries.Count;
        }
    }
}
