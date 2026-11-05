using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ohmline.Charging.Actors.ChargePoints;
using Ohmline.Testing;

namespace Ohmline.Charging.Actors.Tests.Sqlite;

/// <summary>
/// Charging as <c>Ohmline.Node</c> runs it in Development: the real serializer, the charge point
/// directory and Akka.Persistence.Sql on a SQLite file, in a generic host the test starts and
/// stops. Two nodes started one after the other on the same file are a node restart.
/// </summary>
internal sealed class SqliteNode : IAsyncDisposable
{
    /// <summary>
    /// The first answer waits for the node to start on its SQLite file, which takes seconds
    /// when every test assembly runs at once.
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
        builder.Services.AddSingleton(clock);
        builder.Services.AddAkka("ohmline", akka => akka
            .WithSqlPersistence(
                connectionString: database.ConnectionString,
                providerName: ProviderName.SQLiteMS,
                autoInitialize: true,
                tagStorageMode: TagMode.TagTable)
            .AddChargingContext());

        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return new SqliteNode(host);
    }

    /// <summary>Sends to the charge point directory without waiting for a reply.</summary>
    public async Task TellAsync(
        object message, IActorRef replyTo, CancellationToken cancellationToken)
    {
        var directory = await ActorRegistry.For(System)
            .GetAsync<ChargePointDirectory>(cancellationToken);
        directory.Tell(message, replyTo);
    }

    /// <summary>Sends to the charge point directory and waits for the one reply.</summary>
    public async Task<TReply> AskAsync<TReply>(object message, CancellationToken cancellationToken)
    {
        var directory = await ActorRegistry.For(System)
            .GetAsync<ChargePointDirectory>(cancellationToken);
        return await directory.Ask<TReply>(message, AskTimeout, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
