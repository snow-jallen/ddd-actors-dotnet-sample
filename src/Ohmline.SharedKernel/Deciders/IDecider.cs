using System.Collections.Immutable;

namespace Ohmline.SharedKernel.Deciders;

/// <summary>
/// The shape of every aggregate in Ohmline: where it starts, how it decides and how it changes.
/// The members are static: a decider has no instance and no fields, so nothing can hide between
/// two calls, and everything it knows arrives as arguments. One shape means one actor shell
/// (section 10.10) and one test harness (section 9.2) for every aggregate.
/// </summary>
public interface IDecider<TState, TCommand, TEvent, TError>
{
    /// <summary>The state before the first event.</summary>
    static abstract TState Initial { get; }

    /// <summary>The rules: the events a command causes in a state, or why it is refused.</summary>
    static abstract Result<ImmutableArray<TEvent>, TError> Decide(
        TState state, TCommand command, DecisionContext context);

    /// <summary>The memory: total, deterministic, and never checking anything again.</summary>
    static abstract TState Evolve(TState state, TEvent @event);
}
