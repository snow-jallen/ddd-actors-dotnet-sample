using Akka.Streams;
using Npgsql;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.Charging.ReadModels.Query;
using Ohmline.Testing;

namespace Ohmline.Charging.ReadModels.Tests.Projections;

/// <summary>Runs one projection stream over a test's journal until it reaches an offset.</summary>
internal static class ProjectionRun
{
    public static readonly RestartSettings QuickBackoff =
        RestartSettings.Create(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), 0);

    private static readonly TimeSpan Patience =
        TimeSpan.FromSeconds(15 * ActorTestConfig.TimeFactor);

    /// <summary>Starts the runner, waits until it has stored the offset, and stops it.</summary>
    public static async Task UntilOffsetAsync(
        JournalNode node,
        NpgsqlDataSource database,
        IOffsetStore offsets,
        IProjectionHandler projection,
        string tag,
        long offset,
        CancellationToken cancellationToken)
    {
        var runner = new ProjectionRunner(
            node.System.ReadJournal,
            database,
            offsets,
            projection,
            tag,
            QuickBackoff,
            node.System.Log);
        await using var running = runner.Start(node.System.Materializer());

        var stream = new ProjectionStream(projection.ProjectionId, tag);
        var stored = new NpgsqlOffsetStore(database);
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (await stored.LoadAsync(stream, cancellationToken) < offset)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"{stream} did not reach offset {offset}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
