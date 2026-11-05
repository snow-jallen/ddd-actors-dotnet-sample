using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Actors.Journeys.Ports;

namespace Ohmline.IntegrationTests;

/// <summary>
/// One Ohmline node in memory, as <c>Program</c> builds it, on the test's PostgreSQL: a
/// development node, so a cluster of one, on a free remoting port. Disposing it shuts the node
/// down; a new one on the same database is the same node after a restart. Neither Billing nor
/// the Device Gateway is running: the journey's payments stay in memory, its chargers agree to
/// everything, and a test reports the chargers' side itself (the system tests of section 16.9
/// run them all).
/// </summary>
public sealed class OhmlineNode(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ohmline", postgres.ConnectionString)
            .UseSetting("Akka:Remote:Port", "0")
            .ConfigureTestServices(services => services
                .AddSingleton<IPaymentPort, InMemoryPaymentPort>()
                .AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>());
}
