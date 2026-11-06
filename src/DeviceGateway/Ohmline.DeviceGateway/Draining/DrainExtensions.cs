using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ohmline.DeviceGateway.Connections;

namespace Ohmline.DeviceGateway.Draining;

/// <summary>The gateway's way out (section 21.7).</summary>
public static class DrainExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddConnectionDrain() =>
            services.AddSingleton<OpenConnections>().AddSingleton<ConnectionDrain>();
    }

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// The drain as a coordinated-shutdown task in <c>service-unbind</c>, and coordinated
        /// shutdown started as soon as the host begins to stop. The order matters: the web
        /// server stops first, and it waits for every open request, a WebSocket among them,
        /// until the host's shutdown timeout. Were coordinated shutdown left to start after it,
        /// as Akka.Hosting starts it, every charger would wait 30 seconds and then be cut off
        /// at the same moment: the reconnection storm the drain is there to prevent.
        /// </summary>
        public AkkaConfigurationBuilder WithConnectionDrain() =>
            akka.WithActors((system, _, resolver) =>
            {
                var drain = resolver.GetService<ConnectionDrain>();
                var shutdown = CoordinatedShutdown.Get(system);
                shutdown.AddTask(
                    CoordinatedShutdown.PhaseServiceUnbind,
                    "drain-charger-connections",
                    () => drain.DrainAsync());
                resolver.GetService<IHostApplicationLifetime>().ApplicationStopping.Register(
                    () => shutdown.Run(CoordinatedShutdown.ClrExitReason.Instance));
            });
    }
}
