namespace Ohmline.Charging.Contracts.Live;

/// <summary>
/// The DistributedPubSub topics Charging's projections publish on. Durable: a node that
/// subscribes to an old name after a rename hears nothing, and says nothing about it.
/// </summary>
public static class LiveTopics
{
    /// <summary>Every <see cref="SessionSummaryChanged"/>, of every session.</summary>
    public const string SessionSummaries = "session-summaries";

    /// <summary>The topic an update is published on.</summary>
    public static string Of(LiveUpdate update) => update switch
    {
        SessionSummaryChanged => SessionSummaries,
    };
}
