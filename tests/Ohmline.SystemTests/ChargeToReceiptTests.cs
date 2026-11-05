using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Ohmline.Billing.Contracts.Receipts;
using Ohmline.Simulator;

namespace Ohmline.SystemTests;

/// <summary>
/// Plug in, charge, get a receipt (section 16.9): every service as it runs on a laptop, a
/// simulated charger on the Device Gateway's WebSocket, its driver on Charging's API, and the
/// receipt in Billing at the end. Nothing is faked but the charger, TallyPay and Northgate.
/// </summary>
[Trait("Category", "System")]
public sealed class ChargeToReceiptTests
{
    private static readonly TimeSpan Startup = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_charger_and_a_driver_then_a_charge_ends_with_a_receipt()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Ohmline_AppHost>(Ct);
        await using var app = await appHost.BuildAsync(Ct).WaitAsync(Startup, Ct);
        await app.StartAsync(Ct).WaitAsync(Startup, Ct);
        // In the order the AppHost starts them: each waits for what it needs (WaitFor), and
        // healthy means an Up member of the cluster for a node and the gateway.
        foreach (var resource in new[] { "node", "billing", "gateway" })
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, Ct)
                .WaitAsync(Startup, Ct);
        }

        var options = new SimulatorOptions(
            app.GetEndpoint("gateway", "http"),
            app.GetEndpoint("node", "http"),
            Chargers: 1,
            SimulatorOptions.PlugInAndCharge);
        var charged = await Scenarios.PlugInAndChargeAsync(options, number: 1, Ct);

        charged.Outcome.ShouldBe("completed");
        var receipt = await ReceiptAsync(app.CreateHttpClient("billing"), charged.SessionId!.Value);
        receipt.ConnectorId.ShouldBe($"{charged.ChargePointId}/1");
        receipt.EnergyKwh.ShouldBe(12.5m);
        receipt.Total.ShouldBe(5.25m);
        receipt.Lines.ShouldBe([new ReceiptLine("energy", 12.5m, "kWh", 0.42m, 5.25m)]);
    }

    private static async Task<ReceiptResponse> ReceiptAsync(HttpClient billing, Guid session)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            var answer = await billing.GetAsync($"/receipts/{session}", Ct);
            if (answer.StatusCode == HttpStatusCode.OK)
            {
                return (await answer.Content.ReadFromJsonAsync<ReceiptResponse>(Ct))!;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Billing has no receipt for session {session}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), Ct);
        }
    }
}
