using System.Net;
using System.Net.Http.Json;
using Akka;
using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ch02.Hosted.Tests;

// Each test starts the real host on a free port and talks to it over HTTP.
public sealed class ChargerAppTests : IAsyncLifetime
{
    private readonly CapturingLoggerProvider _logs = new();
    private WebApplication _app = null!;
    private HttpClient _http = null!;

    public async ValueTask InitializeAsync()
    {
        _app = ChargerApp.Create(
            ["--urls", "http://127.0.0.1:0"],
            builder => builder.Logging.AddProvider(_logs));
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _http = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Given_a_charger_that_has_not_booted_when_its_status_is_read_then_503()
    {
        var response = await _http.GetAsync(
            "/chargers/OHM-SLC-0042/status", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Given_a_booted_charger_when_its_status_is_read_then_the_twin_answers()
    {
        var ct = TestContext.Current.CancellationToken;
        var boot = await _http.PostAsJsonAsync(
            "/chargers/OHM-SLC-0042/boot-notification",
            new { vendor = "Voltaro", model = "VX-22", firmwareVersion = "4.2.1" },
            ct);
        boot.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var status = await _http.GetStringAsync("/chargers/OHM-SLC-0042/status", ct);

        status.ShouldBe(
            """{"chargePointId":"OHM-SLC-0042","status":"Available","firmwareVersion":"4.2.1"}""");
    }

    [Fact]
    public async Task Given_an_unknown_charger_when_its_status_is_read_then_404()
    {
        var response = await _http.GetAsync(
            "/chargers/OHM-SLC-9999/status", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Given_a_running_host_when_it_stops_then_coordinated_shutdown_drains_first()
    {
        // Akka.NET's logger is an actor that stops with the actor system and sends any line it
        // has not handled yet to stdout instead. Hold back termination until the line is through.
        CoordinatedShutdown.Get(_app.Services.GetRequiredService<ActorSystem>()).AddTask(
            CoordinatedShutdown.PhaseBeforeActorSystemTerminate,
            "wait-for-the-drain-log-line",
            () =>
            {
                SpinWait.SpinUntil(
                    () => _logs.Messages.Any(m => m.Contains("draining charger twins")),
                    TimeSpan.FromSeconds(5));
                return Task.FromResult(Done.Instance);
            });

        await _app.StopAsync(TestContext.Current.CancellationToken);

        _logs.Messages.ShouldContain(m => m.Contains("draining charger twins"));
    }
}
