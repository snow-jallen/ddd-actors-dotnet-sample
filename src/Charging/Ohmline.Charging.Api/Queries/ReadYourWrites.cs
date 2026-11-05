using Microsoft.Extensions.Options;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// Read-your-writes over a read model that lags behind the journal (section 14.1). A client that
/// was just told "version 7" asks for at least version 7: the read model answers as soon as it
/// has caught up, is asked again every 50 ms until <c>Ohmline:ReadModels:ReadYourWritesTimeout</c>,
/// and after that the answer says honestly that it is still behind, and how far it has got.
/// </summary>
/// <remarks>
/// The other way to read your own write is to ask the aggregate. That is always up to date, but
/// it answers for one entity at a time and wakes a passivated one; this costs a few cheap
/// queries of one row, and only while the projection is behind.
/// </remarks>
public sealed class ReadYourWrites(TimeProvider clock, IOptions<ReadModelOptions> options)
{
    /// <summary>How often the read model is asked again while it is behind.</summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Reads with <paramref name="read"/> until the row's version is at least
    /// <paramref name="minVersion"/>, or the timeout has passed. A missing row is version 0.
    /// </summary>
    public async Task<CatchUp<T>> ReadAsync<T>(
        Func<CancellationToken, Task<T?>> read,
        Func<T, long> versionOf,
        long minVersion,
        CancellationToken cancellationToken)
        where T : class
    {
        var deadline = clock.GetUtcNow() + options.Value.ReadYourWritesTimeout;
        while (true)
        {
            var row = await read(cancellationToken);
            var version = row is null ? 0 : versionOf(row);
            if (row is not null && version >= minVersion)
            {
                return new CaughtUp<T>(row);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return new StillBehind(version);
            }

            await Task.Delay(PollInterval, clock, cancellationToken);
        }
    }
}
