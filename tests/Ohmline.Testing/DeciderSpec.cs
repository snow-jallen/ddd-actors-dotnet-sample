using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.Testing;

/// <summary>
/// Given-When-Then for any decider: given the events it has recorded, when it decides a command
/// at a moment, then it records these events, or it refuses for this reason. The history is
/// folded with the decider's own Evolve, as recovery folds the journal, so a test never writes a
/// state down and does not break when the state's shape changes. Errors are compared by their
/// case (<c>.Value</c>), and a failure prints the whole scenario.
/// </summary>
public sealed class DeciderSpec<TDecider, TState, TCommand, TEvent, TError>
    where TDecider : IDecider<TState, TCommand, TEvent, TError>
{
    private readonly ImmutableArray<TEvent> history;
    private readonly DecisionContext? context;

    private DeciderSpec(ImmutableArray<TEvent> history, DecisionContext? context)
    {
        this.history = history;
        this.context = context;
    }

    /// <summary>The events recorded so far, oldest first; none for a new aggregate.</summary>
    public static DeciderSpec<TDecider, TState, TCommand, TEvent, TError> Given(
        params IEnumerable<TEvent> history) => new([.. history], null);

    /// <summary>When the decision is made. There is no default time.</summary>
    public DeciderSpec<TDecider, TState, TCommand, TEvent, TError> At(DateTimeOffset now) =>
        With(new DecisionContext(now));

    /// <summary>Everything the decider is told about the world: a fake clock's reading.</summary>
    public DeciderSpec<TDecider, TState, TCommand, TEvent, TError> With(
        DecisionContext decisionContext) => new(history, decisionContext);

    public Decision When(TCommand command)
    {
        var decisionContext = context ?? throw new InvalidOperationException(
            "Say when the decision is made: call At(...) or With(...) before When(...).");
        var state = Decider.Fold<TDecider, TState, TCommand, TEvent, TError>(history);

        return new Decision(
            history, command, decisionContext, TDecider.Decide(state, command, decisionContext));
    }

    /// <summary>What the decider answered, and the assertions on it.</summary>
    public sealed class Decision
    {
        private readonly ImmutableArray<TEvent> history;
        private readonly TCommand command;
        private readonly DecisionContext context;
        private readonly Result<ImmutableArray<TEvent>, TError> result;

        internal Decision(
            ImmutableArray<TEvent> history,
            TCommand command,
            DecisionContext context,
            Result<ImmutableArray<TEvent>, TError> result)
        {
            this.history = history;
            this.command = command;
            this.context = context;
            this.result = result;
        }

        /// <summary>
        /// Accepted, with exactly these events in this order. Returns them for anything more a
        /// test wants to say about them.
        /// </summary>
        public ImmutableArray<TEvent> Then(params TEvent[] expected)
        {
            if (result is Ok<ImmutableArray<TEvent>> accepted
                && accepted.Value.SequenceEqual(expected))
            {
                return accepted.Value;
            }

            throw Failure(
                "Then it should record", Show.Events(expected), FirstDifference(expected));
        }

        /// <summary>Accepted with no events: the command asked for what is already true.</summary>
        public void ThenNothingChanges() => Then();

        /// <summary>Refused, with this error. Unions are compared by their case.</summary>
        public void ThenRejects(TError expected)
        {
            if (result is Err<TError> refused
                && Equals(Show.CaseOf(refused.Error), Show.CaseOf(expected)))
            {
                return;
            }

            throw Failure("Then it should refuse", $"    {Show.Value(expected)}");
        }

        /// <summary>
        /// Refused for a reason of this case, whatever its data. Returns the case, so a test can
        /// check the one detail it is about.
        /// </summary>
        public TReason ThenRejects<TReason>()
        {
            if (result is Err<TError> refused && Show.CaseOf(refused.Error) is TReason reason)
            {
                return reason;
            }

            throw Failure("Then it should refuse", $"    with a {typeof(TReason).Name}");
        }

        private string? FirstDifference(TEvent[] expected)
        {
            if (result is not Ok<ImmutableArray<TEvent>> { Value: var actual }
                || actual.Length != expected.Length)
            {
                return null;
            }

            var index = Enumerable.Range(0, expected.Length)
                .First(position => !Equals(actual[position], expected[position]));
            return $"The first difference is event {index + 1} of {expected.Length}.";
        }

        private ShouldAssertException Failure(
            string expectation, string expected, string? hint = null)
        {
            string[] answer = result switch
            {
                Ok<ImmutableArray<TEvent>> accepted =>
                    ["But it recorded", Show.Events(accepted.Value)],
                Err<TError> refused => ["But it refused", $"    {Show.Value(refused.Error)}"],
                null => ["But the decision was a default Result"],
            };

            return new ShouldAssertException(string.Join(Environment.NewLine,
            [
                "",
                "Given",
                Show.Events(history),
                $"When, at {Show.Value(context.Now)}",
                $"    {Show.Value(command)}",
                expectation,
                expected,
                .. answer,
                .. hint is null ? Array.Empty<string>() : [hint],
            ]));
        }
    }
}
