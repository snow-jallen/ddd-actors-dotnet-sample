using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ohmline.Charging.Api.Tests;

public sealed class ChargingApiOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_no_setting_then_endpoints_wait_5_s()
    {
        using var host = Host(new Dictionary<string, string?>());
        await host.StartAsync(Ct);

        host.Services.GetRequiredService<IOptions<ChargingApiOptions>>().Value.AskTimeout
            .ShouldBe(TimeSpan.FromSeconds(5));
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:05:00")]
    public async Task Given_an_ask_timeout_out_of_range_then_the_host_does_not_start(
        string timeout)
    {
        using var host = Host(new() { ["Ohmline:Charging:AskTimeout"] = timeout });

        var failure = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync(Ct));

        failure.Message.ShouldContain("AskTimeout");
    }

    private static IHost Host(Dictionary<string, string?> settings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddChargingApi();
        return builder.Build();
    }
}
