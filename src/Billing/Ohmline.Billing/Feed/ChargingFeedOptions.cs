using System.ComponentModel.DataAnnotations;

namespace Ohmline.Billing.Feed;

/// <summary>How Billing reads Charging's feed, under <c>Ohmline:Billing:Feed</c>.</summary>
public sealed class ChargingFeedOptions
{
    public const string Section = "Ohmline:Billing:Feed";

    /// <summary>
    /// Where Charging's nodes answer. <c>http://node</c> is the Aspire resource's name, which
    /// service discovery resolves.
    /// </summary>
    [Required]
    public Uri BaseAddress { get; set; } = new("http://node");

    /// <summary>How long to wait before asking again when the feed had nothing new.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>How long to wait after Charging or the database did not answer.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 500)]
    public int PageSize { get; set; } = 100;
}
