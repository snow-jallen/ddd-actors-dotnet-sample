using Akka.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ohmline.Charging.Api.Live;

/// <summary>Charging's live updates for app clients in a node: the bridge and the hub.</summary>
public static class LiveUpdatesExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// Starts this node's <see cref="LiveUpdatesBridge"/>. The host adds SignalR, which gives
        /// the bridge its hub context.
        /// </summary>
        public AkkaConfigurationBuilder AddChargingLiveUpdates() =>
            akka.WithActors((system, registry, resolver) => registry.Register<LiveUpdatesBridge>(
                system.ActorOf(resolver.Props<LiveUpdatesBridge>(), LiveUpdatesBridge.Name)));
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public HubEndpointConventionBuilder MapChargingHub() =>
            endpoints.MapHub<ChargingHub>(ChargingHub.Path);
    }
}
