using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ohmline.SharedKernel;

namespace Ohmline.Charging.Domain.Tests;

/// <summary>
/// Assertions on a decider's answer: the events it accepted, in order, or the error it gave.
/// Unions are compared and printed by their case (<c>.Value</c>), because a union's own
/// ToString prints only the union's name. Section 9.2 grows this into a full harness.
/// </summary>
internal static class DecisionAssertions
{
    extension<TEvent, TError>(Result<ImmutableArray<TEvent>, TError> decision)
    {
        public void ShouldAccept(params TEvent[] expected)
        {
            var accepted = decision.Value.ShouldBeOfType<Ok<ImmutableArray<TEvent>>>(
                $"The decision was {Describe(decision.Value)}.");
            accepted.Value.ToArray().ShouldBe(expected);
        }

        public void ShouldReject(TError expected)
        {
            var rejected = decision.Value.ShouldBeOfType<Err<TError>>(
                $"The decision was {Describe(decision.Value)}.");
            CaseOf(rejected.Error).ShouldBe(CaseOf(expected));
        }
    }

    private static object? CaseOf(object? value) => value is IUnion union ? union.Value : value;

    private static string Describe(object? decision) => decision switch
    {
        IUnion union => Describe(union.Value),
        null => "default",
        _ when decision.GetType().GetProperty("Error") is { } error =>
            $"a rejection: {CaseOf(error.GetValue(decision))}",
        _ => $"an acceptance: {decision}",
    };
}
