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
        /// Stand-ins for Development (section 15.4): payments in memory, and charge points that
        /// only log. Chapter 16 puts Billing and the Device Gateway behind the same ports.
        /// </summary>
        public IServiceCollection AddDevelopmentJourneyPorts() =>
            services
                .AddSingleton<IPaymentPort, InMemoryPaymentPort>()
                .AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>();
    }
}
