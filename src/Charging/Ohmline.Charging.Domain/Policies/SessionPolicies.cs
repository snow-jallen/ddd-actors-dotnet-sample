using System.Collections.Immutable;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Domain.Policies;

/// <summary>
/// Every policy that watches one session, as one policy: combining policies gives a policy, so
/// the shell holds one and asks it once per event. The intents come in the order of the
/// policies.
/// </summary>
public sealed class SessionPolicies(ImmutableArray<ISessionPolicy> policies) : ISessionPolicy
{
    /// <summary>
    /// The policies for a session under these terms: every session releases what it held and
    /// stops at its spend cap, and the idle fee is watched only if the tariff has one.
    /// </summary>
    public static SessionPolicies For(SessionTerms terms) => new(
    [
        new ReleaseOnSessionEnded(),
        new SpendCapPolicy(),
        .. terms.Tariff.Components.OfType<IdleFee>().Select(fee => new IdleFeePolicy(fee)),
    ]);

    public ImmutableArray<ChargingIntent> React(SessionEvent @event) =>
        [.. policies.SelectMany(policy => policy.React(@event))];
}
