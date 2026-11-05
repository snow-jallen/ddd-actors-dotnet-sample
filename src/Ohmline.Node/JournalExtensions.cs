using Akka.Hosting;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
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
        /// of their own, written by each context's tagger (section 13.2).
        /// </summary>
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

            return akka.WithSqlPersistence(
                connectionString: connectionString,
                providerName: provider,
                autoInitialize: true, // Creates missing tables; Chapter 20 owns the schema instead.
                tagStorageMode: TagMode.TagTable,
                journalBuilder: journal => journal.AddChargingTagger().AddSiteEnergyTagger());
        }
    }
}
