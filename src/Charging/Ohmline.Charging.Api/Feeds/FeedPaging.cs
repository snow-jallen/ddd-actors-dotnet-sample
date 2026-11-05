namespace Ohmline.Charging.Api.Feeds;

/// <summary>
/// The rules of the charging feed's pages (section 16.2), apart from the database: which page
/// comes next, what identifies a page's content, and whether a page can still change.
/// </summary>
public static class FeedPaging
{
    public const int DefaultLimit = 100;

    public const int MaxLimit = 500;

    /// <summary>
    /// The page after this one: from its last position, or from where this one started when it
    /// is empty, so a consumer at the end of the feed asks the same question again.
    /// </summary>
    public static string NextLink(string path, long after, int limit, long? last) =>
        $"{path}?after={last ?? after}&limit={limit}";

    /// <summary>
    /// The events after <paramref name="after"/> up to <paramref name="last"/>: positions are
    /// never reused or filled in later, so these two numbers name the page's content exactly.
    /// </summary>
    public static string ETag(long after, long? last) => $"\"{after}-{last ?? after}\"";

    /// <summary>
    /// A full page never changes again: one relay writes the outbox, in order, so no event can
    /// appear below a position already served. A page with room left gains events as they are
    /// published, and must be asked again.
    /// </summary>
    public static string CacheControl(int count, int limit) =>
        count == limit ? "public, max-age=31536000, immutable" : "no-cache";
}
