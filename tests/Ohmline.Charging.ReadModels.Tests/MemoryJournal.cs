using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Query.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ohmline.Charging.Actors;

namespace Ohmline.Charging.ReadModels.Tests;

/// <summary>
/// Charging's events in Akka.NET's in-memory journal, tagged as the node tags them, and the
/// in-memory read journal over it (section 14.7): no file, no database and no cluster, and live
/// queries that look for new events every 50 ms. The tests write through Charging's own actors.
/// </summary>
/// <remarks>
/// The in-memory read journal numbers each tag's events 0, 1, 2...: an offset of its own, where
/// Akka.Persistence.Sql uses the journal's global ordering from 1. Both are exclusive
/// <see cref="Sequence"/> offsets, except that the in-memory journal reads
/// <c>Offset.Sequence(0)</c> as "from the start". A runner that stores 0 for "nothing yet" gets
/// the first event again after a restart, which an idempotent projection does not mind.
/// </remarks>
internal sealed class MemoryJournal : IAsyncDisposable
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

    private readonly IHost _host;

    private MemoryJournal(IHost host) => _host = host;

    public ActorSystem System => _host.Services.GetRequiredService<ActorSystem>();

    public InMemoryReadJournal ReadJournal =>
        PersistenceQuery.Get(System)
            .ReadJournalFor<InMemoryReadJournal>(InMemoryReadJournal.Identifier);

    public static async Task<MemoryJournal> StartAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddAkka("ohmline", akka => akka
            .WithInMemoryJournal(journal => journal.AddChargingTagger())
            .WithInMemorySnapshotStore()
            .AddHocon(
                "akka.persistence.query.journal.inmem.refresh-interval = 50ms",
                HoconAddMode.Prepend));
        var host = builder.Build();
        await host.StartAsync(cancellationToken);
        return new MemoryJournal(host);
    }

    /// <summary>Sends to the actor and waits for its one reply.</summary>
    public Task<TReply> AskAsync<TReply>(
        IActorRef actor, object message, CancellationToken cancellationToken) =>
        actor.Ask<TReply>(message, AskTimeout, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
