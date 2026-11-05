namespace Ohmline.SharedKernel.Deciders;

/// <summary>What can be written once for every decider.</summary>
public static class Decider
{
    /// <summary>Recovery from nothing: the initial state, evolved by each event in order.</summary>
    public static TState Fold<TDecider, TState, TCommand, TEvent, TError>(
        IEnumerable<TEvent> events)
        where TDecider : IDecider<TState, TCommand, TEvent, TError> =>
        Fold<TDecider, TState, TCommand, TEvent, TError>(TDecider.Initial, events);

    /// <summary>
    /// Recovery from a state that is already known, such as a snapshot, and the events written
    /// after it. Folding the tail onto the snapshot gives what folding everything would give.
    /// </summary>
    public static TState Fold<TDecider, TState, TCommand, TEvent, TError>(
        TState state, IEnumerable<TEvent> events)
        where TDecider : IDecider<TState, TCommand, TEvent, TError> =>
        events.Aggregate(state, TDecider.Evolve);
}
