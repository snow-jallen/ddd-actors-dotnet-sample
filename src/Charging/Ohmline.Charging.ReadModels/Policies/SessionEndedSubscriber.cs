using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Policies;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.ReadModels.Policies;

/// <summary>
/// Choreography (section 15.1): whenever a session ends, Site Energy releases its allocation and
/// Driver Accounts unlocks its token, and nobody coordinates them. A projection with no table of
/// its own: it reads the session events like any projection, runs
/// <see cref="ReleaseOnSessionEnded"/> on each, and sends what the policy intends as the other
/// contexts' commands. The runner stores its offset like any other's.
/// </summary>
/// <remarks>
/// The commands go out before the offset is committed, so after a crash or a failed commit they
/// go out again: at least once. Their ids are derived from the event's, so a command sent twice
/// carries the same id, and its receiver answers it again without deciding again (section
/// 12.6). Each send waits for the receiver's answer. No answer fails the event, and the runner
/// tries it again; a refusal is an answer, and there is nothing more to do about it here.
/// </remarks>
public sealed class SessionEndedSubscriber(ActorSystem? system = null) : IProjectionHandler
{
    /// <summary>Durable: its offsets are kept under it.</summary>
    public const string Id = "session-ended-subscriber-v1";

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);
    private static readonly ReleaseOnSessionEnded Policy = new();

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    /// <summary>
    /// A release that never arrives keeps one connector's power from the others, which is bad;
    /// a subscriber that stops keeps every connector's power from them, which is worse. An event
    /// it gives up on is in <c>read_models.projection_failures</c> for someone to send by hand.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new RecordAndSkip(MaxRetries: 5);

    /// <summary>In a node, the commands go to the shard regions in its actor registry.</summary>
    public IProjectionHandler RunningIn(ActorSystem running) => new SessionEndedSubscriber(running);

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is SessionEvent @event)
        {
            // The session is named after its journey: one conversation (section 17.7).
            Activity.Current?.SetTag(
                "messaging.message.conversation_id", @event.SessionId.ToString());
            foreach (var command in CommandsFor(@event))
            {
                await SendAsync(command, cancellationToken);
            }
        }

        return [];
    }

    /// <summary>
    /// What the policy intends for this event, as Site Energy's and Driver Accounts' commands,
    /// each with an id derived from the id of the command that ended the session.
    /// </summary>
    public static ImmutableArray<SessionEndedCommand> CommandsFor(SessionEvent @event) =>
    [
        .. Policy.React(@event)
            .Select(intent => Translate(intent, @event.CommandId))
            .OfType<SessionEndedCommand>(),
    ];

    /// <summary>
    /// Charging's intents in the other contexts' words: the subscriber is where Charging conforms
    /// to their contracts, so the policy never has to.
    /// </summary>
    private static SessionEndedCommand? Translate(ChargingIntent intent, CommandId cause) =>
        intent switch
        {
            ChargingIntent.ReleaseSiteAllocation release => new ReleaseAllocation(
                CommandId.Derive(cause, "release-allocation"),
                release.SiteId,
                release.ConnectorId,
                release.SessionId),
            ChargingIntent.UnlockToken unlock => new UnlockFromSession(
                CommandId.Derive(cause, "unlock-token"), unlock.TokenUid, unlock.SessionId),

            // Not this subscriber's: ReleaseOnSessionEnded never intends them.
            ChargingIntent.StopCharging or ChargingIntent.WarnOfIdleFee => null,
            null => null,
        };

    private Task SendAsync(SessionEndedCommand command, CancellationToken cancellationToken) =>
        command switch
        {
            ReleaseAllocation release =>
                AskAsync<SitePowerBudgetRegion, BudgetCommandReply>(release, cancellationToken),
            UnlockFromSession unlock =>
                AskAsync<IdTokenRegion, TokenCommandReply>(unlock, cancellationToken),
            null => Task.CompletedTask,
        };

    private async Task AskAsync<TRegion, TReply>(
        object command, CancellationToken cancellationToken)
    {
        var running = system ?? throw new InvalidOperationException(
            $"{nameof(SessionEndedSubscriber)} sends commands only when it runs in a node.");
        var region = await ActorRegistry.For(running).GetAsync<TRegion>(cancellationToken);
        await region.AskTraced<TReply>(command, AskTimeout, cancellationToken);
    }
}

