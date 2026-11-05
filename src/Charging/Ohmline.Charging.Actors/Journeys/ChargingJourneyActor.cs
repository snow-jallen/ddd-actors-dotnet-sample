using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.ActorShell.Sharding;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.Actors.Journeys;

/// <summary>
/// The charging journey in its shell (section 15.3): a persistent entity of the
/// <c>charging-journey</c> shard region, with timers. Every rule is in
/// <see cref="ChargingJourney"/>. The shell turns each message into a
/// <see cref="JourneyInput"/>, stores the step's events, and only then carries out its effects:
/// through Driver Accounts' and Site Energy's shard regions, through the ports, and with its
/// timers. Recovered from its journal, it does again what its step needs.
/// </summary>
/// <remarks>
/// The region remembers its entities, so a journey that is waiting for something is started
/// again on another node after a rebalance or a crash, instead of waiting for a message that
/// may never come. The price is that a journey is never passivated for being idle: one that has
/// nothing left to do passivates itself, and is forgotten.
/// </remarks>
public sealed class ChargingJourneyActor : TracedPersistentActor, IWithTimers
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "charging-journey";

    private readonly JourneyId _journeyId;
    private readonly TimeProvider _clock;
    private readonly IRequiredActor<IdTokenRegion> _tokens;
    private readonly IRequiredActor<SitePowerBudgetRegion> _budgets;
    private readonly IPaymentPort _payments;
    private readonly IChargerCommandPort _chargers;
    private readonly ChargingMetrics _metrics;
    private readonly ILogger _log;
    private JourneyState _state = ChargingJourney.Initial;
    private DateTimeOffset? _startedAt;

    public ChargingJourneyActor(
        JourneyId journeyId,
        TimeProvider clock,
        IRequiredActor<IdTokenRegion> tokens,
        IRequiredActor<SitePowerBudgetRegion> budgets,
        IPaymentPort payments,
        IChargerCommandPort chargers,
        ChargingMetrics metrics,
        ILogger<ChargingJourneyActor> log)
    {
        PersistenceId = PersistenceIdOf(journeyId);
        _journeyId = journeyId;
        _clock = clock;
        _tokens = tokens;
        _budgets = budgets;
        _payments = payments;
        _chargers = chargers;
        _metrics = metrics;
        _log = log;

        Recover<JourneyEvent>(@event =>
        {
            _state = ChargingJourney.Evolve(_state, @event);
            Remember(@event);
        });
        Recover<RecoveryCompleted>(_ => Resume());

        Command<GetJourney>(_ =>
        {
            Sender.Tell(_state.ToView(_journeyId, LastSequenceNr));
            PassivateIfDone();
        });

        // The edge's command and the session's events are answered once their step is stored.
        Command<JourneyCommand>(command => Hear(
            command switch
            {
                StartCharging start => start,
            },
            Sender));
        Command<SessionEvent>(@event => Hear(
            @event switch
            {
                SessionStarted started => started,
                SessionEnded ended => ended,
                _ => null,
            },
            Sender));

        // The answers to the journey's own requests. Those to its releases and unlocks change
        // nothing it decides.
        Command<TokenCommandReply>(reply => Hear(reply switch
        {
            TokenLocked locked => locked,
            TokenLockRefused refused => refused,
            TokenCommandAccepted or TokenCommandRejected => null,
        }));
        Command<BudgetCommandReply>(reply => Hear(reply switch
        {
            PowerAllocated allocated => allocated,
            QueuedForPower queued => queued,
            BudgetCommandRejected refused => refused,
            BudgetCommandAccepted => null,
        }));
        Command<PaymentPortResult>(result => Hear(result switch
        {
            HoldPlaced placed => placed,
            HoldDeclined declined => declined,
            HoldVoided voided => voided,
            PaymentCaptured captured => captured,
            CaptureFailed failed => failed,
        }));
        Command<ChargerCommandResult>(result => Hear(result switch
        {
            ChargerAccepted accepted => accepted,
            ChargerRejected rejected => rejected,
        }));
        Command<JourneyTimerElapsed>(elapsed => Hear(elapsed));

        // A port whose task failed: no answer, as if the message had been lost. A hold is
        // asked for again when the step's reply timer elapses, under the same key; a charger
        // that never answers runs into the energy-flow timeout (section 15.6).
        Command<Status.Failure>(failure => _log.PortFailed(_journeyId, failure.Cause));

        // From the shard: passivation, a hand-off or shutdown (section 12.4).
        Command<Stop>(_ => Context.Stop(Self));
    }

    public override string PersistenceId { get; }

    public ITimerScheduler Timers { get; set; } = null!;

    /// <summary>
    /// The journey's id on every span it processes a message in: the one thread through the
    /// traces of a charge, from the request that started it to the capture (section 17.7).
    /// </summary>
    protected override string ConversationId => _journeyId.ToString();

    /// <summary>
    /// A timer that fires answers no one: it starts a trace of its own, linked to the step
    /// that started it, rather than make that request's trace minutes long.
    /// </summary>
    protected override bool ContinuesTrace(object message) => message is not JourneyTimerElapsed;

    /// <summary>Durable vocabulary: <c>charging-journey-{guid}</c>, for ever.</summary>
    public static string PersistenceIdOf(JourneyId journeyId) => $"{TypeName}-{journeyId}";

    /// <summary>
    /// For a test that starts the actor itself, with instruments and a logger of its choosing
    /// or none. In a node, the shard region has the container make it, with the node's clock,
    /// shard regions, ports, instruments and logging.
    /// </summary>
    public static Props Props(
        JourneyId journeyId,
        TimeProvider clock,
        IRequiredActor<IdTokenRegion> tokens,
        IRequiredActor<SitePowerBudgetRegion> budgets,
        IPaymentPort payments,
        IChargerCommandPort chargers,
        ChargingMetrics? metrics = null,
        ILogger<ChargingJourneyActor>? log = null)
    {
        var instruments = metrics ?? ChargingMetrics.Unhosted;
        var logger = log ?? NullLogger<ChargingJourneyActor>.Instance;
        return Akka.Actor.Props.Create(() => new ChargingJourneyActor(
            journeyId, clock, tokens, budgets, payments, chargers, instruments, logger));
    }

    /// <summary>
    /// One step: decide with the time read once, store the events, then act, and answer whoever
    /// waits. Persist before acting: a journey that crashes in between does again, when it
    /// recovers, what its stored step needs; one that acted first could not know it had.
    /// </summary>
    private void Hear(JourneyInput? input, IActorRef? replyTo = null)
    {
        var step = input is { } heard
            ? ChargingJourney.Next(_state, heard, DecisionContext.From(_clock))
            : new JourneyStep(_state, [], []);
        if (step.Events.IsEmpty)
        {
            Done(step, replyTo);
            return;
        }

        PersistAllTraced(step.Events, Apply, () => Done(step, replyTo));
    }

    private void Apply(JourneyEvent @event)
    {
        var before = _state;
        _state = ChargingJourney.Evolve(_state, @event);
        Remember(@event);
        Measure(before, @event);
        switch (@event)
        {
            case JourneyAbandoned abandoned:
                _log.JourneyAbandoned(_journeyId, abandoned.Reason, abandoned.Detail);
                break;
            case CaptureAttemptFailed failed:
                _log.CaptureAttemptFailed(_journeyId, failed.Attempt, failed.Code);
                break;
            case DebtRecorded debt:
                _log.DebtRecorded(_journeyId, debt.Amount, debt.Reason);
                break;
        }
    }

    /// <summary>
    /// When the journey started, from its first event, stored or replayed: a journey that
    /// recovers halfway still knows how long its driver has waited.
    /// </summary>
    private void Remember(JourneyEvent @event)
    {
        if (@event is JourneyStarted started)
        {
            _startedAt = started.OccurredAt;
        }
    }

    /// <summary>
    /// The SLI of section 18.9, from the journey's own events as they are stored, never as
    /// they are replayed: from <see cref="JourneyStarted"/> to <see cref="EnergyFlowStarted"/>,
    /// or to <see cref="JourneyAbandoned"/> if the journey was given up before energy flowed.
    /// </summary>
    private void Measure(JourneyState before, JourneyEvent @event)
    {
        if (_startedAt is not { } startedAt)
        {
            return;
        }

        switch (@event, before.Value)
        {
            case (EnergyFlowStarted flowing, _):
                _metrics.EnergyWaitEnded(
                    flowing.OccurredAt - startedAt, ChargingMetrics.EnergyFlowing);
                break;
            case (JourneyAbandoned abandoned,
                AwaitingTokenLock or AwaitingHold or AwaitingAllocation or AwaitingEnergyFlow):
                _metrics.EnergyWaitEnded(abandoned.OccurredAt - startedAt, abandoned.Reason);
                break;
        }
    }

    private void Done(JourneyStep step, IActorRef? replyTo)
    {
        foreach (var effect in step.Effects)
        {
            CarryOut(effect);
        }

        replyTo?.Tell(new CommandAccepted(LastSequenceNr));
        PassivateIfDone();
    }

    /// <summary>
    /// The last message of recovery: the journey does again what its step needs, and starts
    /// its timers again for the time they have left.
    /// </summary>
    private void Resume()
    {
        foreach (var effect in ChargingJourney.Resume(_state, _clock.GetUtcNow()))
        {
            CarryOut(effect);
        }
    }

    /// <summary>
    /// Each effect, carried out. A switch expression, so that a new kind of effect does not
    /// compile until the shell knows how to carry it out. Answers come back to this actor as
    /// messages: from the regions because it is the sender, from the ports by PipeTo.
    /// </summary>
    /// <remarks>
    /// Another context's command takes its id from the effect's key, so a request sent again
    /// is the same command, and the receiver's shell answers it as it did the first time.
    /// </remarks>
    private void CarryOut(JourneyEffect effect) => _ = effect switch
    {
        JourneyEffect.LockToken lockToken => Send(
            _tokens,
            new LockForSession(
                lockToken.Key.CommandId, lockToken.TokenUid, lockToken.SessionId)),
        JourneyEffect.PlaceHold hold => Pipe(_payments.PlaceHoldAsync(
            hold.Key, hold.Driver, hold.Amount, CancellationToken.None)),
        JourneyEffect.RequestAllocation request => Send(
            _budgets,
            new RequestAllocation(
                request.Key.CommandId,
                request.SiteId,
                request.ConnectorId,
                request.SessionId,
                request.Requested)),
        JourneyEffect.StartCharger start => Pipe(_chargers.RemoteStartAsync(
            start.Key,
            start.ConnectorId,
            start.TokenUid,
            start.SessionId,
            start.Limit,
            CancellationToken.None)),
        JourneyEffect.StartTimer timer => StartTimer(timer),
        JourneyEffect.CancelTimer timer => CancelTimer(timer),
        JourneyEffect.ReleaseAllocation release => Send(
            _budgets,
            new ReleaseAllocation(
                release.Key.CommandId, release.SiteId, release.ConnectorId, release.SessionId)),
        JourneyEffect.VoidHold hold =>
            Pipe(_payments.VoidAsync(hold.Hold, CancellationToken.None)),
        JourneyEffect.CapturePayment capture => Pipe(
            _payments.CaptureAsync(
                capture.Key, capture.HoldId, capture.Amount, CancellationToken.None),
            // Captured or not, nobody knows; the capture's key makes asking again safe.
            failure: cause => new CaptureFailed(capture.HoldId, "port-failed", cause.Message)),
        JourneyEffect.UnlockToken unlock => Send(
            _tokens,
            new UnlockFromSession(unlock.Key.CommandId, unlock.TokenUid, unlock.SessionId)),
        null => throw new InvalidOperationException("The effect is default: it holds no case."),
    };

    private Unit Send<TRegion>(IRequiredActor<TRegion> region, object command)
    {
        region.ActorRef.TellTraced(command, Self);
        return Unit.Value;
    }

    private Unit Pipe<TResult>(Task<TResult> answer, Func<Exception, object>? failure = null)
    {
        answer.PipeTo(Self, failure: failure);
        return Unit.Value;
    }

    /// <summary>
    /// The timer's message carries the span that started it, and is processed minutes later in
    /// a trace of its own, linked to that span (<see cref="ContinuesTrace"/>).
    /// </summary>
    private Unit StartTimer(JourneyEffect.StartTimer timer)
    {
        Timers.StartSingleTimer(
            timer.Timer, TraceEnvelope.Wrap(new JourneyTimerElapsed(timer.Timer)), timer.Delay);
        return Unit.Value;
    }

    private Unit CancelTimer(JourneyEffect.CancelTimer timer)
    {
        Timers.Cancel(timer.Timer);
        return Unit.Value;
    }

    /// <summary>
    /// A journey that never started, or is over, leaves memory now and is forgotten by the
    /// region. Never <c>Context.Stop(Self)</c>: the shard would keep routing to it.
    /// </summary>
    private void PassivateIfDone()
    {
        if (_state.Value is NotStarted or Completed or Abandoned)
        {
            Context.Parent.Tell(new Passivate(Stop.Instance));
        }
    }
}
