using System.ComponentModel.DataAnnotations;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// How often the projections' read journal asks the database for new events, under
/// <c>Ohmline:Projections</c> (section 14.3). Akka.Persistence.Sql's defaults (1 s each) give
/// 500 to 1,200 ms of lag; 100 ms each gives a few hundred, for one cheap query per stream and
/// one sequence query per node every 100 ms. Lower is possible, at more load on the database.
/// </summary>
/// <remarks>
/// The two only work together: <see cref="RefreshInterval"/> is how often an idle stream asks
/// for events, but it is only given events up to the highest ordering without gaps, which is
/// found every <see cref="QueryDelay"/>. Tuning one alone changes nothing.
/// </remarks>
public sealed class ProjectionOptions
{
    public const string Section = "Ohmline:Projections";

    /// <summary><c>akka.persistence.query.journal.sql.refresh-interval</c>.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:00:10")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// <c>akka.persistence.query.journal.sql.journal-sequence-retrieval.query-delay</c>.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:00:10")]
    public TimeSpan QueryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The settings as HOCON for the read journal.</summary>
    public string ToHocon() => $$"""
        akka.persistence.query.journal.sql {
          refresh-interval = {{RefreshInterval.TotalMilliseconds:0}}ms
          journal-sequence-retrieval.query-delay = {{QueryDelay.TotalMilliseconds:0}}ms
        }
        """;
}
