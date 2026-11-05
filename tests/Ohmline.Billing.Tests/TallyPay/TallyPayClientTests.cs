using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Billing.TallyPay;

namespace Ohmline.Billing.Tests.TallyPay;

/// <summary>
/// The TallyPay client as the host wires it, with the host's default resilience handler on
/// every client (as <c>AddServiceDefaults</c> adds it) and TallyPay's own in its place.
/// </summary>
public sealed class TallyPayClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_TallyPay_failing_twice_then_the_third_try_is_authorized_under_one_key()
    {
        var tallyPay = new TallyPayServer().Failing(times: 2);

        var outcome = await Client(tallyPay)
            .PreauthorizeAsync("019a-hold", "drv-7f3a91c2", 50m, "USD", Ct);

        outcome.Value.ShouldBeOfType<Authorized>();
        tallyPay.Requests.Select(request => request.Key)
            .ShouldBe(["019a-hold", "019a-hold", "019a-hold"]);
    }

    [Fact]
    public async Task Given_TallyPay_down_then_one_retry_loop_tries_four_times_and_gives_up()
    {
        var tallyPay = new TallyPayServer().Failing();

        var outcome = await Client(tallyPay)
            .CaptureAsync("019a-hold", "TP12345", 12.60m, "USD", Ct);

        outcome.Value.ShouldBeOfType<ProviderUnavailable>();
        tallyPay.Requests.Count.ShouldBe(4); // Not 16: the host's handler was taken off.
        tallyPay.Requests.ShouldAllBe(request => request.Key == "019a-hold-capture");
    }

    [Fact]
    public async Task Amounts_and_references_reach_TallyPay_in_its_own_terms()
    {
        var tallyPay = new TallyPayServer();

        await Client(tallyPay).CaptureAsync("019a-hold", "TP12345", 12.60m, "USD", Ct);

        var (path, _, body) = tallyPay.Requests.ShouldHaveSingleItem();
        path.ShouldBe("/v1/capture");
        body["AUTH_ID"]!.GetValue<string>().ShouldBe("TP12345");
        body["AMT_MINOR"]!.GetValue<long>().ShouldBe(1260);
        body["CCY"]!.GetValue<string>().ShouldBe("USD");
    }

    private static TallyPayClient Client(TallyPayServer tallyPay)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
        services.AddTallyPay();
        services.AddHttpClient<TallyPayClient>().ConfigurePrimaryHttpMessageHandler(() => tallyPay);
        return services.BuildServiceProvider().GetRequiredService<TallyPayClient>();
    }
}
