using Microsoft.FeatureManagement;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.Node;

/// <summary>
/// Charging's feature flags (section 21.8), evaluated by Microsoft.FeatureManagement in the
/// node's host, outside every actor: once before the node starts, then every
/// <see cref="RefreshInterval"/>. An entity reads the latest outcome in its turn, without
/// waiting, and passes it to its decider; the decider records what it decided in the event.
/// Each evaluation of a flag whose telemetry is on is traced on the
/// <c>Microsoft.FeatureManagement</c> source, which the service defaults listen to.
/// </summary>
public sealed class FeatureManagementFlags(IFeatureManager features, TimeProvider clock)
    : BackgroundService, IFeatureFlagSource
{
    /// <summary>How soon a flag changed in configuration reaches the deciders.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    private FeatureFlags _current = FeatureFlags.None;

    public FeatureFlags Current => Volatile.Read(ref _current);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        List<string> enabled = [];
        foreach (var feature in ChargingFeatures.All)
        {
            if (await features.IsEnabledAsync(feature))
            {
                enabled.Add(feature);
            }
        }

        Volatile.Write(ref _current, new FeatureFlags(enabled));
    }

    /// <summary>The first evaluation, before any actor can start a session.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }
}

public static class FeatureFlagExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Microsoft.FeatureManagement on the node's configuration (<c>feature_management</c>),
        /// and Charging's flags for its entities. Registered before the actor system, so the
        /// flags are evaluated before it starts.
        /// </summary>
        public IServiceCollection AddFeatureFlags()
        {
            services.AddFeatureManagement();
            services.AddSingleton<FeatureManagementFlags>();
            services.AddSingleton<IFeatureFlagSource>(provider =>
                provider.GetRequiredService<FeatureManagementFlags>());
            return services.AddHostedService(provider =>
                provider.GetRequiredService<FeatureManagementFlags>());
        }
    }
}
