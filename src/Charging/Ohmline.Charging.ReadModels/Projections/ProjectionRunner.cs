using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using Akka;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Npgsql;
using Ohmline.Charging.Contracts.Live;

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
/// at least once. An event the projection keeps failing on is dealt with by its
/// <see cref="ProjectionFailurePolicy"/> (section 13.7). What the projection says has changed is
/// handed to <paramref name="publish"/> after the commit, never before (section 14.4).
/// </remarks>
public sealed class ProjectionRunner(
    IEventsByTagQuery readJournal,
    NpgsqlDataSource dataSource,
    IOffsetStore offsets,
    IProjectionHandler projection,
    string tag,
    RestartSettings backoff,
    Action<LiveUpdate> publish,
    ILoggingAdapter log)
{
    private readonly ProjectionStream _stream = new(projection.ProjectionId, tag);
    private UniqueKillSwitch? _killSwitch;
    private (long Offset, int Attempts) _failing;

    /// <summary>The backoff Ohmline's projections use unless told otherwise.</summary>
    public static RestartSettings DefaultBackoff { get; } =
        RestartSettings.Create(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 0.2);

    /// <summary>
    /// Starts the stream on <paramref name="materializer"/>, the system's shared one (never
    /// disposed: in Akka.NET 1.5 that would break every other stream of the system). A runner
    /// runs once.
    /// </summary>
    public RunningProjection Start(IMaterializer materializer)
    {
        var (killSwitch, completion) = RestartSource
            .OnFailuresWithBackoff(EventsAfterStoredOffset, backoff)
            .ViaMaterialized(KillSwitches.Single<long>(), Keep.Right)
            .ToMaterialized(Sink.Ignore<long>(), Keep.Both)
            .Run(materializer);
        _killSwitch = killSwitch;
        return new RunningProjection(_stream, killSwitch, Unwrapped(completion));
    }

    /// <summary>
    /// The sink's task fails with an <see cref="AggregateException"/> around the cause; the
    /// stream's completion fails with the cause itself.
    /// </summary>
    private static async Task Unwrapped(Task completion)
    {
        try
        {
            await completion;
        }
        catch (AggregateException failed) when (failed.Flatten().InnerExceptions is [var cause])
        {
            ExceptionDispatchInfo.Throw(cause);
        }
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
        var offset = OffsetOf(envelope);
        try
        {
            await WriteAsync(
                envelope,
                transaction =>
                    projection.HandleAsync(envelope, transaction, CancellationToken.None));
            _failing = default;
            return offset;
        }
        catch (Exception exception) when (!IsTransient(exception))
        {
            _failing = _failing.Offset == offset ? (offset, _failing.Attempts + 1) : (offset, 1);
            if (_failing.Attempts <= projection.FailurePolicy.MaxRetries)
            {
                log.Error(
                    exception,
                    "Projection {0} failed on {1} #{2} at offset {3} ({4} of {5} retries); it " +
                    "restarts from its offset",
                    _stream,
                    envelope.PersistenceId,
                    envelope.SequenceNr,
                    offset,
                    _failing.Attempts,
                    projection.FailurePolicy.MaxRetries);
                throw;
            }

            return await GiveUpAsync(envelope, offset, exception);
        }
    }

    /// <summary>
    /// The projection's rows and the offset: both, or neither. The offset row also records when
    /// the event was written to the journal, and its <c>updated_at</c> when it was handled.
    /// What changed is published after the commit: a client that is told about a version and
    /// asks for it at once finds it in the read model. A crash between the two loses the
    /// notification, never the row; clients catch up by reading.
    /// </summary>
    private async Task WriteAsync(
        EventEnvelope envelope, Func<NpgsqlTransaction, Task<ImmutableArray<LiveUpdate>>> write)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var changes = await write(transaction);
        await offsets.SaveAsync(
            _stream,
            OffsetOf(envelope),
            WrittenAt(envelope),
            transaction,
            CancellationToken.None);
        await transaction.CommitAsync();
        foreach (var change in changes)
        {
            publish(change);
        }
    }

    private static long OffsetOf(EventEnvelope envelope) => ((Sequence)envelope.Offset).Value;

    /// <summary>
    /// Akka.Persistence.Sql stamps each journal row with the writing node's
    /// <see cref="DateTime.UtcNow"/>, in ticks.
    /// </summary>
    private static DateTimeOffset WrittenAt(EventEnvelope envelope) =>
        new(envelope.Timestamp, TimeSpan.Zero);

    private Task<long> GiveUpAsync(EventEnvelope envelope, long offset, Exception exception) =>
        projection.FailurePolicy switch
        {
            RecordAndSkip => SkipAsync(envelope, offset, exception),
            StopAfterRetries => Task.FromException<long>(Stop(envelope, offset, exception)),
            null => throw new InvalidOperationException(
                $"Projection {_stream} has no failure policy."),
        };

    private async Task<long> SkipAsync(EventEnvelope envelope, long offset, Exception exception)
    {
        await WriteAsync(
            envelope, transaction => RecordFailureAsync(envelope, exception, transaction));
        log.Error(
            exception,
            "Projection {0} skipped {1} #{2} at offset {3}; it is recorded in " +
            "read_models.projection_failures",
            _stream,
            envelope.PersistenceId,
            envelope.SequenceNr,
            offset);
        _failing = default;
        return offset;
    }

    /// <summary>
    /// Fails the whole stream, restarts and all: the kill switch sits outside the restarts, and
    /// the failure it returns stops this run at the event, so no later event is handled and the
    /// stored offset stays before it.
    /// </summary>
    private ProjectionStoppedException Stop(
        EventEnvelope envelope, long offset, Exception exception)
    {
        log.Error(
            exception,
            "Projection {0} stopped at {1} #{2}, offset {3}, after {4} retries. Its read model " +
            "stays as it is until the projection is fixed and the node restarted",
            _stream,
            envelope.PersistenceId,
            envelope.SequenceNr,
            offset,
            projection.FailurePolicy.MaxRetries);
        var stopped = new ProjectionStoppedException(_stream, offset, exception);
        _killSwitch?.Abort(stopped);
        return stopped;
    }

    private async Task<ImmutableArray<LiveUpdate>> RecordFailureAsync(
        EventEnvelope envelope, Exception exception, NpgsqlTransaction transaction)
    {
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO read_models.projection_failures
                (projection_id, persistence_id, sequence_nr, error)
            VALUES (@projection_id, @persistence_id, @sequence_nr, @error)
            ON CONFLICT (projection_id, persistence_id, sequence_nr)
            DO UPDATE SET error = excluded.error, failed_at = now()
            """,
            transaction.Connection,
            transaction);
        insert.Parameters.AddWithValue("projection_id", _stream.ProjectionId);
        insert.Parameters.AddWithValue("persistence_id", envelope.PersistenceId);
        insert.Parameters.AddWithValue("sequence_nr", envelope.SequenceNr);
        insert.Parameters.AddWithValue("error", exception.ToString());
        await insert.ExecuteNonQueryAsync();
        return [];
    }

    /// <summary>
    /// The database cannot be reached, or the connection broke: nothing is wrong with the
    /// event, so it does not count against it, and the stream backs off for as long as it takes.
    /// </summary>
    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException { IsTransient: true };
}
