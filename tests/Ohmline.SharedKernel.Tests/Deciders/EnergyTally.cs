using System.Collections.Immutable;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Units;

namespace Ohmline.SharedKernel.Tests.Deciders;

/// <summary>
/// The smallest useful decider, for testing the abstraction without any one context's model: it
/// counts energy and refuses to count past a limit.
/// </summary>
public sealed class EnergyTally : IDecider<Energy, Count, Counted, OverLimit>
{
    private EnergyTally()
    {
    }

    public static Energy Limit { get; } = new(100m);

    public static Energy Initial => Energy.Zero;

    public static Result<ImmutableArray<Counted>, OverLimit> Decide(
        Energy state, Count command, DecisionContext context) =>
        state + command.Energy > Limit
            ? Result.Fail(new OverLimit(state + command.Energy))
            : Result.Ok(ImmutableArray.Create(new Counted(command.Energy, context.Now)));

    public static Energy Evolve(Energy state, Counted @event) => state + @event.Energy;
}

public sealed record Count(Energy Energy);

public sealed record Counted(Energy Energy, DateTimeOffset At);

public sealed record OverLimit(Energy Total);
