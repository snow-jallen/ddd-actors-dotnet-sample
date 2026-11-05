using Akka.Actor;
using Akka.Cluster;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Ohmline.Charging.Actors;
using Ohmline.Charging.Actors.Journeys.Ports;
using Ohmline.DriverAccounts.Actors;
using Ohmline.SiteEnergy.Actors;
using Ohmline.Tariffs.Actors;
using HostingPersistenceTestKit = Akka.Hosting.TestKit.PersistenceTestKit;

namespace Ohmline.Node.Tests;

/// <summary>
/// The node's Akka.NET wiring in a test, as <c>Program.cs</c> composes it: the node's own
/// cluster setup on a free port, which joins itself in Development, and every bounded context
/// with its regions and singleton, on the persistence test kit's journal and snapshot store
/// instead of SQL. A test reaches the aggregates as the endpoints do: through the regions in
/// the actor registry.
/// </summary>
public abstract class ShardedNode(ITestOutputHelper output)
    : HostingPersistenceTestKit("ohmline", output)
{
    public static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    protected static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected FakeTimeProvider Clock { get; } = new(Morning);

    protected override void ConfigureServices(
        HostBuilderContext context, IServiceCollection services)
    {
        base.ConfigureServices(context, services);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddDevelopmentJourneyPorts(); // As the node does in Development.
        // Billing is not in the test: payments in memory, behind the same port.
        services.AddSingleton<IPaymentPort, InMemoryPaymentPort>();
    }

    protected override void ConfigureAkka(
        AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        base.ConfigureAkka(builder, provider); // The test journal and snapshot store.
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Akka:Remote:Hostname"] = "localhost",
                ["Akka:Remote:Port"] = "0", // Any free port: test classes run in parallel.
            })
            .Build();
        builder
            .WithOhmlineCluster(settings, new Development())
            .AddChargingContext()
            .AddSiteEnergyContext()
            .AddDriverAccountsContext()
            .AddTariffsContext();
    }

    /// <summary>Every test starts on a cluster of one that is up.</summary>
    protected override async Task BeforeTestStart()
    {
        await base.BeforeTestStart();
        await AwaitConditionAsync(
            () => Task.FromResult(Cluster.Get(Sys).SelfMember.Status == MemberStatus.Up),
            Patience,
            cancellationToken: Ct);
    }

    /// <summary>Asks the region or proxy registered under the key, as an endpoint would.</summary>
    protected Task<TReply> AskAsync<TKey, TReply>(object message) =>
        AskAsync<TKey, TReply>(message, Patience);

    /// <summary>
    /// Asks with a timeout of the test's choosing: inside <c>AwaitAssertAsync</c>, shorter than
    /// the assertion's own patience, so that a question lost to a stopping entity leaves time
    /// for another attempt.
    /// </summary>
    protected async Task<TReply> AskAsync<TKey, TReply>(object message, TimeSpan timeout)
    {
        var region = await ActorRegistry.GetAsync<TKey>(Ct);
        return await region.Ask<TReply>(message, timeout, Ct);
    }

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Ohmline.Node.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
