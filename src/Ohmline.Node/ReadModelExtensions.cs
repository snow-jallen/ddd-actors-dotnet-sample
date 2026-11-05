using Akka.Hosting;
using Npgsql;
using Ohmline.Charging.ReadModels;

namespace Ohmline.Node;

/// <summary>
/// Where the node keeps its read models: in PostgreSQL, beside the journal, when
/// <c>ConnectionStrings:ohmline</c> is set. The single SQLite development node of section 10.7
/// has no read side.
/// </summary>
public static class ReadModelExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>The read models' database, for projections and query endpoints.</summary>
        public IServiceCollection AddOhmlineReadModels(IConfiguration configuration) =>
            configuration.GetConnectionString("ohmline") is { } postgres
                ? services.AddSingleton(_ => NpgsqlDataSource.Create(postgres))
                : services;
    }

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>The projections, as sharded daemons, if there is a database for them.</summary>
        public AkkaConfigurationBuilder WithOhmlineReadModels(IConfiguration configuration) =>
            configuration.GetConnectionString("ohmline") is null
                ? akka
                : akka.AddChargingReadModels();
    }
}
