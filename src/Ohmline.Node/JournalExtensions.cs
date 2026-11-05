using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Ohmline.ActorShell.Deciders;
using Ohmline.ActorShell.Health;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Actors;
using Ohmline.SiteEnergy.Actors;

namespace Ohmline.Node;

/// <summary>Where the node keeps its journal and snapshots.</summary>
public static class JournalExtensions
{
    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>
        /// PostgreSQL when <c>ConnectionStrings:ohmline</c> is set, as Aspire and the integration
        /// tests set it; otherwise the SQLite file in <c>ConnectionStrings:journal</c>, for a
        /// single development node (section 10.7). The same tables on both, with tags in a table
        /// of their own, written by each context's tagger (section 13.2), and for a traced event,
        /// stored in its envelope, by the node's <see cref="TracedEventTagger"/> (section 17.7).
        /// Charging's events stored in older shapes are read through its adapter (section 20.4).
        /// </summary>
        /// <remarks>
        /// Readiness asks the database itself (section 18.5). Akka.Persistence's own journal
        /// check asks the journal actor, which is alive and answers while PostgreSQL is down, so
        /// it stays Healthy through an outage; the connectivity check opens a connection.
        /// </remarks>
        public AkkaConfigurationBuilder WithOhmlineJournal(IConfiguration configuration)
        {
            var (connectionString, provider) = configuration.GetConnectionString("ohmline") is
                { } postgres
                ? (postgres, ProviderName.PostgreSQL15)
                : (configuration.GetConnectionString("journal")
                    ?? throw new InvalidOperationException(
                        "Neither ConnectionStrings:ohmline nor ConnectionStrings:journal is " +
                        "set. In Development, appsettings.Development.json sets the second."),
                    ProviderName.SQLiteMS);

            return akka
                .WithOptionalSnapshots() // Snapshots are a cache (section 20.6).
                .WithSqlPersistence(
                    connectionString: connectionString,
                    providerName: provider,
                    // PostgreSQL's tables are the migrator's (section 20.8); only the single
                    // development node's SQLite file is still made by the journal itself.
                    autoInitialize: provider == ProviderName.SQLiteMS,
                    tagStorageMode: TagMode.TagTable,
                    journalBuilder: journal => journal
                        .AddChargingTagger()
                        .AddChargingLegacyEvents()
                        .AddSiteEnergyTagger()
                        .AddTraceEnvelopeTagger<TracedEventTagger>()
                        .WithConnectivityCheck(tags: [HealthTags.Ready, "akka", "persistence"]),
                    snapshotBuilder: snapshots => snapshots
                        .WithConnectivityCheck(tags: [HealthTags.Ready, "akka", "persistence"]));
        }
    }
}
