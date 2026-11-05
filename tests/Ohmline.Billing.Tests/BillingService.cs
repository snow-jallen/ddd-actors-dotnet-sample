using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Billing.Feed;
using Ohmline.Billing.TallyPay;

namespace Ohmline.Billing.Tests;

/// <summary>
/// Billing as <c>Program</c> builds it, in memory, on a test's database, reading the charging
/// feed from <paramref name="feed"/> instead of a node and paying through
/// <paramref name="tallyPay"/> (an approving one if none is given).
/// </summary>
internal sealed class BillingService(
    string connectionString, FeedServer feed, TallyPayServer? tallyPay = null)
    : WebApplicationFactory<Program>
{
    public TallyPayServer TallyPay { get; } = tallyPay ?? new TallyPayServer();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("ConnectionStrings:billing", connectionString)
            .UseSetting("Ohmline:Billing:Feed:PollInterval", "00:00:00.050")
            .ConfigureTestServices(services =>
            {
                services.AddHttpClient<ChargingFeedClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => feed);
                services.AddHttpClient<TallyPayClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => TallyPay);
            });
}
