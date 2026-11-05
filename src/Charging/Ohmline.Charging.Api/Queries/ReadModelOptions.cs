using System.ComponentModel.DataAnnotations;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// How the query endpoints use the read models, under <c>Ohmline:ReadModels</c>. Checked when
/// the host starts, like the edge's other settings.
/// </summary>
public sealed class ReadModelOptions
{
    public const string Section = "Ohmline:ReadModels";

    /// <summary>
    /// How long a read with <c>?minVersion=</c> waits for the read model to catch up before it
    /// answers 202 (section 14.1). A few times the projection's usual lag: long enough that a
    /// client almost never sees 202, short enough that a stuck projection does not hold
    /// requests open. Zero never waits.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:00:10")]
    public TimeSpan ReadYourWritesTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Which version of the session history answers (section 20.9): 1 until
    /// <c>session-history-v2</c> has caught up with the journal, then 2. Switching back is as
    /// easy, which is the point of building the new version beside the old.
    /// </summary>
    [Range(1, 2)]
    public int SessionHistoryVersion { get; set; } = 1;
}
