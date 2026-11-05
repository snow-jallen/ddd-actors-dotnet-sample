using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Journeys;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts;

namespace Ohmline.Charging.Domain.Journeys;

/// <summary>
/// The charging journey as a pure state machine: a process manager with no actor in it. Given
/// where the journey is and what it hears, <see cref="Next"/> says where it is now, what to
/// record and what to do. It reads no clock, sends nothing and stores nothing; the shell does
/// all three, in that order: store the step, then carry out its effects.
/// </summary>
/// <remarks>
/// Most effects belong to the step the journey enters: whoever enters
/// <see cref="AwaitingHold"/> asks for a hold. So <see cref="Next"/> decides the events, folds
/// them into the new state with <see cref="Evolve"/>, and asks that state what to do. Every
/// request carries a key derived from the journey and the step, so asking again is safe.
/// </remarks>
public static class ChargingJourney
{
    /// <summary>How long the charger has to start delivering energy once it is told to.</summary>
    public static readonly TimeSpan EnergyFlowTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How many times a capture is tried before the amount is recorded as owed.</summary>
    public const int MaxCaptureAttempts = 4;

    /// <summary>
    /// How long a step waits for another context's answer before it asks again (section 15.6).
    /// </summary>
    public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long a step asks again before the journey gives up on the answer.</summary>
    public static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(1);

    public static JourneyState Initial => JourneyState.Initial;

    /// <summary>
    /// One step: the events that record what <paramref name="input"/> changes, stamped with the
    /// context's time, the state they lead to, and what to do once they are stored. An input
    /// that changes nothing gives no events and no effects.
    /// </summary>
    public static JourneyStep Next(
        JourneyState state, JourneyInput input, DecisionContext context)
    {
        ImmutableArray<JourneyEvent> events =
            [
                .. Decide(state, input, context.Now)
                    .Select(@event => @event with { OccurredAt = context.Now }),
            ];
        if (events.IsEmpty)
        {
            return new JourneyStep(state, [], Again(state, input, context.Now));
        }

        var next = events.Aggregate(state, Evolve);
        return new JourneyStep(next, events, Enter(next, context.Now));
    }

    /// <summary>
    /// Applies a recorded step. Total: an event that does not fit changes nothing.
    /// </summary>
    public static JourneyState Evolve(JourneyState state, JourneyEvent @event) =>
        (state, @event) switch
        {
            (NotStarted, JourneyStarted started) =>
                new AwaitingTokenLock(TermsOf(started), started.OccurredAt),
            (AwaitingTokenLock waiting, TokenSecured secured) =>
                new AwaitingHold(waiting.Terms, secured.Driver, secured.OccurredAt),
            (AwaitingHold waiting, HoldSecured secured) =>
                new AwaitingAllocation(waiting.Terms, secured.HoldId, secured.OccurredAt),
            (AwaitingAllocation waiting, PowerSecured secured) => new AwaitingEnergyFlow(
                waiting.Terms, waiting.HoldId, secured.Allocated, secured.OccurredAt),
            (AwaitingEnergyFlow waiting, EnergyFlowStarted flowing) =>
                new Charging(waiting.Terms, waiting.HoldId, flowing.Tariff),
            (Charging charging, SettlementDue due) =>
                new Settling(charging.Terms, charging.HoldId, due.Amount),
            (Settling settling, CaptureAttemptFailed failed) =>
                settling with { FailedCaptures = failed.Attempt },
            (Settling settling, JourneyCompleted completed) =>
                new Completed(settling.Terms, completed.Captured),
            (Settling settling, DebtRecorded debt) => new Completed(
                settling.Terms, Money.Zero(debt.Amount.Currency), Owed: debt.Amount),
            (AwaitingTokenLock or AwaitingHold or AwaitingAllocation or AwaitingEnergyFlow,
                JourneyAbandoned abandoned) =>
                new Abandoned(state, abandoned.Reason, abandoned.Detail),

            (null, _) => throw DefaultState(),
            _ => state,
        };

    public static JourneyState Fold(IEnumerable<JourneyEvent> events) =>
        events.Aggregate(Initial, Evolve);

    /// <summary>
    /// What a journey recovered from its journal does again (section 15.3): the effects of the
    /// state it is in, as if it had just entered it, with its timers started again for the time
    /// they have left. The journey may have stopped after storing a step and before acting on
    /// it, and it cannot tell; so it acts again, and its receivers must not mind.
    /// </summary>
    public static ImmutableArray<JourneyEffect> Resume(JourneyState state, DateTimeOffset now) =>
        Enter(state, now);

    /// <summary>
    /// The decision table: what each input means in each state. A reply is matched to the
    /// journey's own session, so another session's reply, or one that comes late or twice,
    /// finds no row and changes nothing.
    /// </summary>
    private static ImmutableArray<JourneyEvent> Decide(
        JourneyState state, JourneyInput input, DateTimeOffset now) =>
        (state, input) switch
        {
            (NotStarted, StartCharging start) => [Started(start)],

            (AwaitingTokenLock waiting, TokenLocked locked)
                when locked.SessionId == waiting.Terms.SessionId =>
                [new TokenSecured(waiting.Terms.JourneyId, locked.Driver)],
            (AwaitingTokenLock waiting, TokenLockRefused refused)
                when refused.SessionId == waiting.Terms.SessionId =>
                [Abandon(waiting.Terms, AbandonReasons.TokenRefused, refused.Message)],

            (AwaitingHold waiting, HoldPlaced placed) =>
                [new HoldSecured(waiting.Terms.JourneyId, placed.HoldId)],
            (AwaitingHold waiting, HoldDeclined declined) =>
                [Abandon(waiting.Terms, AbandonReasons.HoldDeclined, declined.Message)],

            (AwaitingAllocation waiting, PowerAllocated allocated)
                when allocated.SessionId == waiting.Terms.SessionId =>
                [new PowerSecured(waiting.Terms.JourneyId, allocated.Allocated)],
            (AwaitingAllocation waiting, QueuedForPower queued)
                when queued.SessionId == waiting.Terms.SessionId =>
                [new PowerSecured(waiting.Terms.JourneyId, Power.Zero)],
            (AwaitingAllocation waiting, BudgetCommandRejected refused) =>
                [Abandon(waiting.Terms, AbandonReasons.AllocationRefused, refused.Message)],

            // The charger waits for the car; the session's start is what moves the journey on.
            (AwaitingEnergyFlow, ChargerAccepted) => [],
            (AwaitingEnergyFlow waiting, ChargerRejected rejected) =>
                [Abandon(waiting.Terms, AbandonReasons.ChargerRejected, rejected.Reason)],
            (AwaitingEnergyFlow waiting, SessionStarted started)
                when started.SessionId == waiting.Terms.SessionId =>
                [new EnergyFlowStarted(waiting.Terms.JourneyId, started.Tariff)],
            (AwaitingEnergyFlow waiting, JourneyTimerElapsed { Timer: JourneyTimer.EnergyFlow }) =>
            [
                Abandon(
                    waiting.Terms,
                    AbandonReasons.EnergyFlowTimedOut,
                    $"No energy flowed within {EnergyFlowTimeout.TotalMinutes:0} minutes."),
            ],

            (Charging charging, SessionEnded ended)
                when ended.SessionId == charging.Terms.SessionId =>
                [
                    new SettlementDue(
                        charging.Terms.JourneyId,
                        ended.EnergyDelivered,
                        CostOf(charging, ended.EnergyDelivered)),
                ],

            // No answer for a whole step: a lost message, asked again, and lost again.
            (AwaitingTokenLock or AwaitingHold or AwaitingAllocation,
                JourneyTimerElapsed { Timer: JourneyTimer.Reply })
                when now >= SinceOf(state) + StepTimeout =>
            [
                Abandon(
                    state.Terms!,
                    AbandonReasons.NoReply,
                    $"No answer within {StepTimeout.TotalMinutes:0} minute, asked every " +
                    $"{ReplyTimeout.TotalSeconds:0} seconds."),
            ],

            (Settling settling, PaymentCaptured captured) =>
                [new JourneyCompleted(settling.Terms.JourneyId, captured.Amount)],
            (Settling settling, CaptureFailed failed)
                when settling.FailedCaptures + 1 < MaxCaptureAttempts =>
            [
                new CaptureAttemptFailed(
                    settling.Terms.JourneyId,
                    settling.FailedCaptures + 1,
                    failed.Code,
                    failed.Message),
            ],
            (Settling settling, CaptureFailed failed) =>
            [
                new DebtRecorded(
                    settling.Terms.JourneyId,
                    settling.Amount,
                    $"Capture failed {MaxCaptureAttempts} times; last: {failed.Message}"),
            ],

            (null, _) => throw DefaultState(),
            (_, null) => throw new ArgumentException("The input is default: it holds no case."),

            // Anything else changes nothing: a reply that is late, repeated or for another
            // session, a void's confirmation, a timer this step does not use.
            _ => [],
        };

    /// <summary>
    /// What an input that changes nothing still asks for: a retry timer that elapses asks for
    /// its request again. Nothing is recorded; the request carries what it carried before.
    /// </summary>
    private static ImmutableArray<JourneyEffect> Again(
        JourneyState state, JourneyInput input, DateTimeOffset now) =>
        (state, input) switch
        {
            (Settling settling, JourneyTimerElapsed { Timer: JourneyTimer.CaptureRetry }) =>
                [Capture(settling)],
            (AwaitingTokenLock or AwaitingHold or AwaitingAllocation,
                JourneyTimerElapsed { Timer: JourneyTimer.Reply }) => Enter(state, now),
            _ => [],
        };

    /// <summary>
    /// How long to wait after the capture failed this many times: 30 seconds, then twice as
    /// long each time.
    /// </summary>
    public static TimeSpan CaptureBackoff(int failedCaptures) =>
        TimeSpan.FromSeconds(30 * Math.Pow(2, failedCaptures - 1));

    /// <summary>
    /// What to do on entering a state: ask for what the step waits for, or give back what an
    /// abandoned journey had obtained. Timers run from when the step began.
    /// </summary>
    private static ImmutableArray<JourneyEffect> Enter(JourneyState state, DateTimeOffset now) =>
        state switch
        {
            NotStarted or Completed => [],
            AwaitingTokenLock { Terms: var terms } =>
            [
                new JourneyEffect.LockToken(
                    Key(terms, IdempotencyKey.Lock), terms.TokenUid, terms.SessionId),
                AwaitReply,
            ],
            AwaitingHold { Terms: var terms } waiting =>
            [
                new JourneyEffect.PlaceHold(
                    Key(terms, IdempotencyKey.Hold), waiting.Driver, terms.Hold),
                AwaitReply,
            ],
            AwaitingAllocation { Terms: var terms } =>
            [
                new JourneyEffect.RequestAllocation(
                    Key(terms, IdempotencyKey.Allocation),
                    terms.SiteId,
                    terms.ConnectorId,
                    terms.SessionId,
                    terms.MaxPower),
                AwaitReply,
            ],
            AwaitingEnergyFlow { Terms: var terms } waiting =>
            [
                new JourneyEffect.CancelTimer(JourneyTimer.Reply),
                new JourneyEffect.StartCharger(
                    Key(terms, IdempotencyKey.Charger),
                    terms.ConnectorId,
                    terms.TokenUid,
                    terms.SessionId,
                    waiting.Allocated),
                StartTimer(JourneyTimer.EnergyFlow, waiting.Since + EnergyFlowTimeout, now),
            ],
            Charging => [new JourneyEffect.CancelTimer(JourneyTimer.EnergyFlow)],
            Settling { FailedCaptures: 0 } settling => [Capture(settling)],
            Settling settling =>
            [
                new JourneyEffect.StartTimer(
                    JourneyTimer.CaptureRetry, CaptureBackoff(settling.FailedCaptures)),
            ],
            Abandoned abandoned => GiveBack(abandoned.From),
            null => throw DefaultState(),
        };

    /// <summary>
    /// The compensations: everything the journey asked for up to the step it was abandoned in,
    /// given back, the last first. What it asked for, not what it was told it got: an answer
    /// may have been lost, and giving back what was never given changes nothing, because every
    /// receiver ignores what does not apply (section 15.6).
    /// </summary>
    private static ImmutableArray<JourneyEffect> GiveBack(JourneyState from) => from switch
    {
        AwaitingTokenLock { Terms: var terms } => [Unlock(terms)],
        AwaitingHold { Terms: var terms } => [Void(terms), Unlock(terms)],
        AwaitingAllocation { Terms: var terms } => [Release(terms), Void(terms), Unlock(terms)],
        AwaitingEnergyFlow { Terms: var terms } =>
        [
            new JourneyEffect.CancelTimer(JourneyTimer.EnergyFlow),
            Release(terms),
            Void(terms),
            Unlock(terms),
        ],

        // A step a journey is never abandoned in.
        NotStarted or Charging or Settling or Completed or Abandoned => [],
        null => throw DefaultState(),
    };

    /// <summary>
    /// The journey's session is named after the journey, so that the session's events find
    /// their journey without a lookup (section 15.3).
    /// </summary>
    public static SessionId SessionOf(JourneyId journeyId) => new(journeyId.Value);

    private static JourneyStarted Started(StartCharging start) => new(
        start.JourneyId,
        start.CommandId,
        SessionOf(start.JourneyId),
        start.TokenUid,
        start.ConnectorId,
        start.SiteId,
        start.MaxPower,
        start.Hold);

    private static JourneyTerms TermsOf(JourneyStarted started) => new(
        started.JourneyId,
        started.SessionId,
        started.TokenUid,
        started.ConnectorId,
        started.SiteId,
        started.MaxPower,
        started.Hold);

    private static JourneyAbandoned Abandon(JourneyTerms terms, string reason, string detail) =>
        new(terms.JourneyId, reason, detail);

    /// <summary>
    /// What the energy cost under the session's tariff, as the session's own terms count it:
    /// the same figure its spend cap is checked against.
    /// </summary>
    private static Money CostOf(Charging charging, Energy delivered) =>
        new SessionTerms(
                charging.Terms.SessionId,
                charging.Terms.SiteId,
                charging.Terms.ConnectorId,
                charging.Terms.TokenUid,
                charging.Tariff,
                charging.Terms.Hold)
            .CostOf(delivered);

    private static IdempotencyKey Key(JourneyTerms terms, string step) =>
        IdempotencyKey.Of(terms.JourneyId, step);

    private static JourneyEffect.StartTimer AwaitReply { get; } =
        new(JourneyTimer.Reply, ReplyTimeout);

    private static JourneyEffect.CapturePayment Capture(Settling settling) =>
        new(Key(settling.Terms, IdempotencyKey.Capture), settling.HoldId, settling.Amount);

    private static JourneyEffect.ReleaseAllocation Release(JourneyTerms terms) =>
        new(Key(terms, IdempotencyKey.Release), terms.SiteId, terms.ConnectorId, terms.SessionId);

    private static JourneyEffect.VoidHold Void(JourneyTerms terms) =>
        new(Key(terms, IdempotencyKey.Hold));

    private static JourneyEffect.UnlockToken Unlock(JourneyTerms terms) =>
        new(Key(terms, IdempotencyKey.Unlock), terms.TokenUid, terms.SessionId);

    /// <summary>When the step that waits for an answer began.</summary>
    private static DateTimeOffset SinceOf(JourneyState state) => state switch
    {
        AwaitingTokenLock waiting => waiting.Since,
        AwaitingHold waiting => waiting.Since,
        AwaitingAllocation waiting => waiting.Since,
        _ => throw new InvalidOperationException("This step waits for no answer."),
    };

    /// <summary>A timer that elapses at <paramref name="at"/>, or at once if it is past.</summary>
    private static JourneyEffect.StartTimer StartTimer(
        JourneyTimer timer, DateTimeOffset at, DateTimeOffset now) =>
        new(timer, at > now ? at - now : TimeSpan.Zero);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from ChargingJourney.Initial.");
}
