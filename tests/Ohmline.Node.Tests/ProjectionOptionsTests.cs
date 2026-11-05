using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ohmline.Node.Tests;

/// <summary>The projections' polling settings, as the node reads and checks them (14.3).</summary>
public sealed class ProjectionOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Ohmline:Projections:RefreshInterval", "00:00:00.001")]
    [InlineData("Ohmline:Projections:QueryDelay", "00:01:00")]
    public async Task Given_a_polling_setting_out_of_range_then_the_node_does_not_start(
        string key, string value)
    {
        using var host = Host(new() { [key] = value });

        var failure = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync(Ct));

        failure.Message.ShouldContain(key.Split(':')[^1]);
    }

    private static IHost Host(Dictionary<string, string?> settings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddOhmlineReadModels(builder.Configuration);
        return builder.Build();
    }
}
