using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Actors.Journeys.Ports;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Petabridge.Cmd, bound safely (section 18.8): the node's command host answers on the
/// loopback interface and on no other, so only someone already on the machine, or forwarded to
/// it, can tell the cluster what to do.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresFixture.OneNodeAtATime)]
public sealed class OperatorCommandsTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_running_node_then_pbm_is_reachable_on_loopback_and_nowhere_else()
    {
        var port = FreePort();
        await using var node = new Node(postgres.ConnectionString, port);
        await node.CreateClient().GetAsync("/health/live", Ct); // Starts the node.

        (await ConnectsAsync(IPAddress.Loopback, port, Patience)).ShouldBeTrue();
        foreach (var address in OtherAddresses())
        {
            (await ConnectsAsync(address, port, TimeSpan.FromSeconds(1)))
                .ShouldBeFalse($"{address}:{port}");
        }
    }

    /// <summary>Tries to open a TCP connection until it succeeds or patience runs out.</summary>
    private static async Task<bool> ConnectsAsync(IPAddress address, int port, TimeSpan patience)
    {
        var deadline = DateTimeOffset.UtcNow + patience;
        do
        {
            using var client = new TcpClient(address.AddressFamily);
            try
            {
                await client.ConnectAsync(address, port, Ct);
                return true;
            }
            catch (SocketException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
            }
        }
        while (DateTimeOffset.UtcNow < deadline);

        return false;
    }

    /// <summary>This machine's IPv4 addresses on its network interfaces, loopback aside.</summary>
    private static IEnumerable<IPAddress> OtherAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(address));

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary><see cref="OhmlineNode"/>, with pbm on a port of the test's choosing.</summary>
    private sealed class Node(string connectionString, int port) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder
                .UseEnvironment("Development")
                .UseSetting("ConnectionStrings:ohmline", connectionString)
                .UseSetting("Akka:Remote:Port", "0")
                .UseSetting("PetabridgeCmd:Port", port.ToString(CultureInfo.InvariantCulture))
                .ConfigureTestServices(services => services
                    .AddSingleton<IPaymentPort, InMemoryPaymentPort>()
                    .AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>());
    }
}
