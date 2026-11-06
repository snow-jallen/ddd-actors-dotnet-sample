using Ohmline.SharedKernel.Deciders;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// Where an actor shell reads the feature flags it passes to its decider (section 21.8). Read
/// synchronously, inside the actor's turn: the host evaluates the flags outside any actor and
/// keeps the latest outcome here, so an actor never awaits a feature manager in a handler.
/// </summary>
public interface IFeatureFlagSource
{
    /// <summary>A source with every flag off, for a test or a host that has none.</summary>
    static readonly IFeatureFlagSource None = new Fixed(FeatureFlags.None);

    /// <summary>The flags as last evaluated.</summary>
    FeatureFlags Current { get; }

    /// <summary>A source whose flags never change.</summary>
    static IFeatureFlagSource Of(params string[] enabled) => new Fixed(new FeatureFlags(enabled));

    private sealed class Fixed(FeatureFlags flags) : IFeatureFlagSource
    {
        public FeatureFlags Current => flags;
    }
}
