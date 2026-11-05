using System.Collections.Immutable;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;

namespace Ohmline.Charging.ReadModels.Journeys;

/// <summary>
/// Brings a session's start and end to its charging journey (section 15.4): a projection with
/// no table, on the session events, that tells each to the journey region. The session knows
/// nothing of journeys; the journey's region routes the event to the journey the session is
/// named after. A session that no journey started finds a journey that never started, which
/// ignores it.
/// </summary>
/// <remarks>
/// Each event waits for the journey's answer, which comes once the journey has stored what the
/// event changed; only then does the runner store the offset. An event that gets no answer is
/// sent again, and the journey ignores one it has already heard: at least once, made safe.
/// </remarks>
public sealed class JourneySessionRelay(ActorSystem? system = null) : IProjectionHandler
{
    /// <summary>Durable: its offsets are kept under it.</summary>
    public const string Id = "journey-session-relay-v1";

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    /// <summary>
    /// A journey that misses its session's start gives up after its energy-flow timeout; one
    /// stuck relay would make every journey do so. It goes on, and records what it skipped.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new RecordAndSkip(MaxRetries: 5);

    /// <summary>In a node, the events go to the journey region in its actor registry.</summary>
    public IProjectionHandler RunningIn(ActorSystem running) => new JourneySessionRelay(running);

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is SessionStarted or SessionEnded)
        {
            var running = system ?? throw new InvalidOperationException(
                $"{nameof(JourneySessionRelay)} tells journeys only when it runs in a node.");
            var journeys = await ActorRegistry.For(running)
                .GetAsync<ChargingJourneyRegion>(cancellationToken);
            await journeys.AskTraced<CommandReply>(envelope.Event, AskTimeout, cancellationToken);
        }

        return [];
    }
}
