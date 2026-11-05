using Akka;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Npgsql;

namespace Ohmline.Charging.ReadModels.Projections;

/// <summary>
/// Runs one projection stream (section 13.4): loads the stored offset, reads the tag's events
/// after it, hands each to the projection, and stores the new offset. A failure anywhere
/// restarts the whole stream with backoff, from the offset stored last.
/// </summary>
/// <remarks>
/// The projection's rows and the offset are written in one transaction (section 13.5): a crash
/// undoes both, and the event comes again. The projection may still see an event twice, after
/// a failed attempt, or when its offset is reset to rebuild it, so it must not mind: delivery is
/// at least once.
/// </remarks>
public sealed class ProjectionRunner(
    IEventsByTagQuery readJournal,
    NpgsqlDataSource dataSource,
    IOffsetStore offsets,
    IProjectionHandler projection,
    string tag,
    RestartSettings backoff,
    ILoggingAdapter log)
{
    private readonly ProjectionStream _stream = new(projection.ProjectionId, tag);

    /// <summary>The backoff Ohmline's projections use unless told otherwise.</summary>
    public static RestartSettings DefaultBackoff { get; } =
        RestartSettings.Create(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 0.2);

    /// <summary>
    /// Starts the stream on <paramref name="materializer"/>, the system's shared one (never
    /// disposed: in Akka.NET 1.5 that would break every other stream of the system).
    /// </summary>
    public RunningProjection Start(IMaterializer materializer)
    {
        var (killSwitch, completion) = RestartSource
            .OnFailuresWithBackoff(EventsAfterStoredOffset, backoff)
            .ViaMaterialized(KillSwitches.Single<long>(), Keep.Right)
            .ToMaterialized(Sink.Ignore<long>(), Keep.Both)
            .Run(materializer);
        return new RunningProjection(_stream, killSwitch, completion);
    }

    /// <summary>
    /// One run of the stream. Every restart calls it again, so the offset is loaded afresh.
    /// </summary>
    private Source<long, NotUsed> EventsAfterStoredOffset() =>
        Source.FromTask(offsets.LoadAsync(_stream, CancellationToken.None))
            .ConcatMany(offset => readJournal.EventsByTag(_stream.Tag, Offset.Sequence(offset)))
            .SelectAsync(1, HandleAsync);

    private async Task<long> HandleAsync(EventEnvelope envelope)
    {
        var offset = ((Sequence)envelope.Offset).Value;
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await projection.HandleAsync(envelope, transaction, CancellationToken.None);
            await offsets.SaveAsync(_stream, offset, transaction, CancellationToken.None);
            await transaction.CommitAsync();
            return offset;
        }
        catch (Exception exception)
        {
            log.Error(
                exception,
                "Projection {0} failed on {1} #{2} at offset {3}; it restarts from its offset",
                _stream,
                envelope.PersistenceId,
                envelope.SequenceNr,
                offset);
            throw;
        }
    }
}
