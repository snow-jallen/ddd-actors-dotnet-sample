using System.Collections.Immutable;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Outbox;

/// <summary>
/// The journal is the outbox (section 16.1): Charging stores its domain events and nothing
/// else, and this projection translates the ones other services care about into integration
/// events, in <c>read_models.integration_outbox</c>, from where the charging feed serves them
/// (section 16.2). There is no second write to forget: the session's actor writes one event,
/// and the relay writes the outbox row in the same database transaction as its offset.
/// </summary>
/// <remarks>
/// At least once into the outbox, made exactly once by the event id: a session's
/// <c>SessionCompleted</c> always has the same id, and the outbox keeps the first. Rows are
/// numbered in the order they are written, and one relay writes them, one event at a time, so
/// a reader that has read up to a position never finds an earlier one appear later.
/// </remarks>
public sealed class IntegrationEventRelay(Func<ChargingIntegrationEvent, Task>? publish = null)
    : IProjectionHandler
{
    /// <summary>
    /// On branch <c>wrong/ch16-s01-dual-write</c> only: the event goes straight to a broker,
    /// outside the database transaction that stores the offset. Two writes, two systems.
    /// </summary>
    private readonly Func<ChargingIntegrationEvent, Task> _publish =
        publish ?? (@event => Console.Out.WriteLineAsync($"Published {@event}"));

    /// <summary>Durable: its offsets are kept under it.</summary>
    public const string Id = "integration-relay-v1";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    /// <summary>
    /// Billing invoices what this relay publishes: a session it cannot publish stops it, rather
    /// than leave a gap in what drivers are billed for.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new StopAfterRetries(MaxRetries: 5);

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        switch (envelope.Event)
        {
            case SessionStarted started:
                await OutboxTables.KeepSessionAsync(
                    transaction, RelayedSession.From(started), cancellationToken);
                break;
            case SessionEnded ended:
                var session = await OutboxTables.FindSessionAsync(
                    transaction, ended.SessionId.Value, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Session {ended.SessionId} ended, but the relay never saw it start.");
                // Wrong on purpose: published now, while the offset is not stored yet.
                await _publish(SessionCompletedTranslation.Translate(session, ended));
                await OutboxTables.ForgetSessionAsync(
                    transaction, ended.SessionId.Value, cancellationToken);
                break;
        }

        return [];
    }
}
