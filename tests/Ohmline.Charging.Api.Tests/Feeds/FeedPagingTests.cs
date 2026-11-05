using Ohmline.Charging.Api.Feeds;

namespace Ohmline.Charging.Api.Tests.Feeds;

public sealed class FeedPagingTests
{
    [Fact]
    public void Given_a_page_with_events_then_the_next_one_starts_after_its_last()
    {
        FeedPaging.NextLink("/feeds/charging", after: 100, limit: 50, last: 117)
            .ShouldBe("/feeds/charging?after=117&limit=50");
    }

    [Fact]
    public void Given_the_end_of_the_feed_then_the_next_page_is_the_same_question_again()
    {
        FeedPaging.NextLink("/feeds/charging", after: 117, limit: 50, last: null)
            .ShouldBe("/feeds/charging?after=117&limit=50");
    }

    [Fact]
    public void A_page_is_named_by_where_it_starts_and_its_last_event()
    {
        FeedPaging.ETag(after: 100, last: 117).ShouldBe("\"100-117\"");
        FeedPaging.ETag(after: 117, last: null).ShouldBe("\"117-117\"");
        FeedPaging.ETag(after: 100, last: 118).ShouldNotBe(FeedPaging.ETag(100, 117));
    }

    [Theory]
    [InlineData(100, 100, "public, max-age=31536000, immutable")]
    [InlineData(17, 100, "no-cache")]
    [InlineData(0, 100, "no-cache")]
    public void A_full_page_is_cached_for_good_and_a_page_with_room_is_asked_again(
        int count, int limit, string cacheControl)
    {
        FeedPaging.CacheControl(count, limit).ShouldBe(cacheControl);
    }
}
