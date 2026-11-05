using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ohmline.ActorShell.Clustering;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// Charging as <c>Ohmline.Node</c> runs it in Development: a cluster of one, the real serializer,
/// the shard regions and Akka.Persistence.Sql on a SQLite file, in a generic host the test starts
/// and stops. Two nodes started one after the other on the same file are a node restart.
/// </summary>
internal sealed class SqliteNode : IAsyncDisposable
{
    /// <summary>
    /// A node on a SQLite file creates its tables and forms its cluster before the first answer,
    /// which takes seconds when every test assembly runs at once (errata, section 18.4).
    /// </summary>
    private static readonly TimeSpan AskTimeout =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private readonly IHost _host;

    private SqliteNode(IHost host) => _host = host;

    public ActorSystem System => _host.Services.GetRequiredService<ActorSystem>();

    public static async Task<SqliteNode> StartAsync(
        TemporaryDatabase database, TimeProvider clock, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(clock); // The container gives it to every entity.
        builder.Services.AddSingleton<ChargingMetrics>();
        builder.Services.AddAkka("ohmline", akka => akka
            .WithRemoting("localhost", 0) // Any free port: tests run in parallel.
            .WithClustering(new ClusterOptions { Roles = [ClusterRoles.Charging] })
            .AddStartup((system, _) =>
            {
                var cluster = Cluster.Get(system);
                cluster.Join(cluster.SelfAddress);
            })
            .WithSqlPersistence(
                connectionString: database.ConnectionString,
                providerName: ProviderName.SQLiteMS,
                autoInitialize: true,
                tagStorageMode: TagMode.TagTable,
                journalBuilder: journal => journal.AddChargingTagger())
            .AddChargingContext());

        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return new SqliteNode(host);
    }

    /// <summary>Sends to the charge point region without waiting for a reply.</summary>
    public async Task TellAsync(
        object message, IActorRef replyTo, CancellationToken cancellationToken)
    {
        var region = await ActorRegistry.For(System).GetAsync<ChargePointRegion>(cancellationToken);
        region.Tell(message, replyTo);
    }

    /// <summary>Sends to the charge point region and waits for the one reply.</summary>
    public Task<TReply> AskAsync<TReply>(object message, CancellationToken cancellationToken) =>
        AskAsync<ChargePointRegion, TReply>(message, cancellationToken);

    /// <summary>Sends to the region registered under the key and waits for the one reply.</summary>
    public async Task<TReply> AskAsync<TRegion, TReply>(
        object message, CancellationToken cancellationToken)
    {
        var region = await ActorRegistry.For(System).GetAsync<TRegion>(cancellationToken);
        return await region.Ask<TReply>(message, AskTimeout, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
