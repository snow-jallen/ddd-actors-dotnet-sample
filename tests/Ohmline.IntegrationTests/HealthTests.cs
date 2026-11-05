using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.Migrator;
using Ohmline.ServiceDefaults;
using Testcontainers.PostgreSql;

namespace Ohmline.IntegrationTests;

/// <summary>
/// Health checks that tell the truth (section 18.5): with its database gone, a node is still
/// live, so nobody restarts it, and no longer ready, so nobody sends it work. A PostgreSQL of
/// its own, because the test stops it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HealthTests : IAsyncLifetime
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("ohmline").Build();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_the_journals_database_stops_then_the_node_is_live_but_not_ready()
    {
        await using var node = new Node(_postgres.GetConnectionString());
        var http = node.CreateClient();
        await StatusAsync(http, ServiceDefaultsExtensions.ReadyPath, HttpStatusCode.OK);

        await _postgres.StopAsync(Ct);

        await StatusAsync(
            http, ServiceDefaultsExtensions.ReadyPath, HttpStatusCode.ServiceUnavailable);
        (await http.GetAsync(ServiceDefaultsExtensions.LivePath, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(Ct);
        Migrations.Apply(
                MigrationTarget.Ohmline, _postgres.GetConnectionString(), NullLogger.Instance)
            .Successful.ShouldBeTrue();
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    /// <summary>Asks until the endpoint answers with the status, or patience runs out.</summary>
    private static async Task StatusAsync(HttpClient http, string path, HttpStatusCode expected)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        patience.CancelAfter(Patience);
        while (true)
        {
            var status = (await http.GetAsync(path, Ct)).StatusCode;
            if (status == expected || patience.IsCancellationRequested)
            {
                status.ShouldBe(expected, $"GET {path}");
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        }
    }

    /// <summary><see cref="OhmlineNode"/>, on a database of the test's own.</summary>
    private sealed class Node(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder
                .UseEnvironment("Development")
                .UseSetting("ConnectionStrings:ohmline", connectionString)
                .UseSetting("Akka:Remote:Port", "0")
                .ConfigureTestServices(services => services
                    .AddSingleton<IPaymentPort, InMemoryPaymentPort>()
                    .AddSingleton<IChargerCommandPort, LoggingChargerCommandPort>());
    }
}
