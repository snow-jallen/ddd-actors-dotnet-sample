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
using Npgsql;
using Ohmline.ActorShell.Clustering;
using Ohmline.Charging.Actors;
using Ohmline.Testing;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>
/// Charging's write side as the node runs it, on a SQLite file of the test's own: a cluster of
/// one, the shard regions, the serializer and the tagger. The tests write through the regions
/// and read the same file through the read journal.
/// </summary>
internal sealed class JournalNode : IAsyncDisposable
{
    /// <summary>
    /// A node on a SQLite file creates its tables and forms its cluster before the first answer,
    /// which takes seconds when every test assembly runs at once.
    /// </summary>
    private static readonly TimeSpan AskTimeout =
        TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    private readonly IHost _host;

    private JournalNode(IHost host) => _host = host;

    public ActorSystem System => _host.Services.GetRequiredService<ActorSystem>();

    /// <summary>
    /// Starts the node. With <paramref name="readModels"/>, it also runs Charging's projections
    /// as the node does, as sharded daemons, into that database (section 13.6).
    /// </summary>
    public static async Task<JournalNode> StartAsync(
        TemporaryDatabase database,
        TimeProvider clock,
        CancellationToken cancellationToken,
        NpgsqlDataSource? readModels = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(clock);
        if (readModels is not null)
        {
            builder.Services.AddSingleton(readModels);
        }

        builder.Services.AddAkka("ohmline", akka =>
        {
            akka
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
                .AddChargingContext();
            if (readModels is not null)
            {
                akka.AddChargingReadModels();
            }
        });

        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return new JournalNode(host);
    }

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
