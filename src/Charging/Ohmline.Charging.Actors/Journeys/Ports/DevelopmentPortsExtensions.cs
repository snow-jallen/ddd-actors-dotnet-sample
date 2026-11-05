using Microsoft.Extensions.DependencyInjection;

namespace Ohmline.Charging.Actors.Journeys.Ports;

/// <summary>
/// The journey's ports are interfaces resolved from the container, like the clock: the region
/// has the container make each journey, and the container decides what is behind each port.
/// </summary>
public static class DevelopmentPortsExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The stand-in left for Development (section 15.4): charge points that only log, until
        /// the Device Gateway connects real ones (section 16.7). Payments go to Billing since
        /// section 16.5.
        /// </summary>
        public IServiceCollection AddDevelopmentJourneyPorts() =>
            services.AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>();
    }
}
