using System.Collections.Immutable;
using System.Numerics;
using CsCheck;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.Testing;

/// <summary>
/// Model-based property tests for any decider. A generator writes random scripts: commands, each
/// after a random wait. Every script is run as the shell would run it, deciding each command
/// against the state the earlier ones left and evolving by the events it accepts, and a property
/// is checked after every decision. When one fails, CsCheck shrinks the script to a short one
/// that still breaks it.
/// </summary>
public static class DeciderProperties<TDecider, TState, TCommand, TEvent, TError>
    where TDecider : IDecider<TState, TCommand, TEvent, TError>
{
    /// <summary>
    /// How many scripts each property runs. CsCheck shrinks a failure while it goes on sampling,
    /// so more scripts give a shorter counterexample; a thousand take well under a second here.
    /// </summary>
    private const long ScriptsPerProperty = 1_000;

    /// <summary>One decision of a run, with the states on either side of it.</summary>
    public sealed record Step(
        TState Before,
        TCommand Command,
        DateTimeOffset At,
        Result<ImmutableArray<TEvent>, TError> Decision,
        TState After)
    {
        /// <summary>The events the decision recorded; none when it refused.</summary>
        public ImmutableArray<TEvent> Events =>
            Decision is Ok<ImmutableArray<TEvent>> accepted ? accepted.Value : [];
    }

    /// <summary>A command and how long the clock runs before it is decided.</summary>
    public sealed record Timed(TimeSpan Wait, TCommand Command);

    /// <summary>
    /// Scripts that open with the given commands, such as the factory, and go on with up to
    /// <paramref name="length"/> commands from the generator, each after a wait from
    /// <paramref name="waits"/>.
    /// </summary>
    public static Gen<ImmutableArray<Timed>> Scripts(
        Gen<TCommand> commands, Gen<TimeSpan> waits, int length, params TCommand[] opening) =>
        Gen.Select(waits, commands, (wait, command) => new Timed(wait, command))
            .Array[0, length]
            .Select(rest => ImmutableArray.CreateRange(
                [.. opening.Select(command => new Timed(TimeSpan.Zero, command)), .. rest]));

    /// <summary>Runs a script from the initial state, with the clock starting at start.</summary>
    public static ImmutableArray<Step> Run(DateTimeOffset start, IEnumerable<Timed> script)
    {
        var steps = ImmutableArray.CreateBuilder<Step>();
        var state = TDecider.Initial;
        var now = start;

        foreach (var (wait, command) in script)
        {
            now += wait;
            var decision = TDecider.Decide(state, command, new DecisionContext(now));
            var after = decision is Ok<ImmutableArray<TEvent>> accepted
                ? Decider.Fold<TDecider, TState, TCommand, TEvent, TError>(state, accepted.Value)
                : state;

            steps.Add(new Step(state, command, now, decision, after));
            state = after;
        }

        return steps.ToImmutable();
    }

    /// <summary>
    /// The property holds after every decision of every script. A failure names the decision,
    /// and CsCheck prints the shortest script it found that fails.
    /// </summary>
    public static void AfterEveryDecision(
        Gen<ImmutableArray<Timed>> scripts, DateTimeOffset start, Action<Step> property) =>
        scripts.Sample(
            script =>
            {
                var steps = Run(start, script);
                foreach (var (index, step) in steps.Index())
                {
                    try
                    {
                        property(step);
                    }
                    catch (Exception broken)
                    {
                        throw new ShouldAssertException(
                            $"After decision {index + 1} of {steps.Length}, "
                            + $"{Show.Value(step.Command)}: {broken.Message}",
                            broken);
                    }
                }
            },
            iter: ScriptsPerProperty,
            print: Print);

    /// <summary>
    /// Evolve is total: it accepts every event Decide produces, in every state a script reaches.
    /// Running the scripts is the test; an exception from Evolve fails it.
    /// </summary>
    public static void EvolveIsTotal(Gen<ImmutableArray<Timed>> scripts, DateTimeOffset start) =>
        AfterEveryDecision(scripts, start, _ => { });

    /// <summary>
    /// Decide never records an event that Evolve ignores: each event changes the state it meets.
    /// An event that is remembered only in the journal, such as an anomaly kept for people to
    /// investigate, is named by <paramref name="journalOnly"/> and left out.
    /// </summary>
    public static void EveryEventChangesTheState(
        Gen<ImmutableArray<Timed>> scripts,
        DateTimeOffset start,
        Func<TEvent, bool>? journalOnly = null) =>
        AfterEveryDecision(scripts, start, step =>
        {
            var state = step.Before;
            foreach (var @event in step.Events)
            {
                var next = TDecider.Evolve(state, @event);
                if (journalOnly?.Invoke(@event) != true && Equals(next, state))
                {
                    throw new ShouldAssertException(
                        $"{Show.Value(@event)} changed nothing in {Show.Value(state)}.");
                }

                state = next;
            }
        });

    /// <summary>The invariant is true of every state that any decision leaves behind.</summary>
    public static void Invariant(
        Gen<ImmutableArray<Timed>> scripts,
        DateTimeOffset start,
        string invariant,
        Func<TState, bool> holds) =>
        AfterEveryDecision(scripts, start, step =>
        {
            if (!holds(step.After))
            {
                throw new ShouldAssertException(
                    $"{invariant} is false of {Show.Value(step.After)}.");
            }
        });

    /// <summary>The measure of the state never goes down, whatever is decided.</summary>
    public static void Monotonic<TMeasure>(
        Gen<ImmutableArray<Timed>> scripts,
        DateTimeOffset start,
        string measure,
        Func<TState, TMeasure> of)
        where TMeasure : IComparisonOperators<TMeasure, TMeasure, bool> =>
        AfterEveryDecision(scripts, start, step =>
        {
            if (of(step.After) < of(step.Before))
            {
                throw new ShouldAssertException(
                    $"{measure} went down from {Show.Value(of(step.Before))} to "
                    + $"{Show.Value(of(step.After))}.");
            }
        });

    private static string Print(ImmutableArray<Timed> script) =>
        Environment.NewLine + string.Join(
            Environment.NewLine,
            script.Select(line => $"    after {line.Wait:g}: {Show.Value(line.Command)}"));
}
