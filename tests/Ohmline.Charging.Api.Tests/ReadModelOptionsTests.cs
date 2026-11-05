using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Queries;

namespace Ohmline.Charging.Api.Tests;

public sealed class ReadModelOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_no_setting_then_a_read_your_writes_read_waits_up_to_2_s()
    {
        using var host = Host(new Dictionary<string, string?>());
        await host.StartAsync(Ct);

        host.Services.GetRequiredService<IOptions<ReadModelOptions>>().Value.ReadYourWritesTimeout
            .ShouldBe(TimeSpan.FromSeconds(2));
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData("-00:00:01")]
    [InlineData("00:01:00")]
    public async Task Given_a_read_your_writes_timeout_out_of_range_then_the_host_does_not_start(
        string timeout)
    {
        using var host = Host(new() { ["Ohmline:ReadModels:ReadYourWritesTimeout"] = timeout });

        var failure = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync(Ct));

        failure.Message.ShouldContain("ReadYourWritesTimeout");
    }

    [Fact]
    public async Task Given_no_setting_then_the_first_version_of_the_session_history_answers()
    {
        using var host = Host(new Dictionary<string, string?>());
        await host.StartAsync(Ct);

        var version = host.Services.GetRequiredService<IOptions<ReadModelOptions>>().Value
            .SessionHistoryVersion;
        SessionHistorySource.Of(version).ShouldBe(SessionHistorySource.Version1);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Given_version_2_then_the_second_version_s_table_answers()
    {
        using var host = Host(new() { ["Ohmline:ReadModels:SessionHistoryVersion"] = "2" });
        await host.StartAsync(Ct);

        var version = host.Services.GetRequiredService<IOptions<ReadModelOptions>>().Value
            .SessionHistoryVersion;
        SessionHistorySource.Of(version).Table.ShouldBe("read_models.session_history_v2");
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    public async Task Given_a_session_history_version_that_does_not_exist_then_no_start(
        string version)
    {
        using var host = Host(new() { ["Ohmline:ReadModels:SessionHistoryVersion"] = version });

        var failure = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync(Ct));

        failure.Message.ShouldContain("SessionHistoryVersion");
    }

    private static IHost Host(Dictionary<string, string?> settings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddChargingApi();
        return builder.Build();
    }
}
