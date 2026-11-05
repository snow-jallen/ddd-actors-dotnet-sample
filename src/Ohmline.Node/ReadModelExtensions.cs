using Akka.Hosting;
using Npgsql;
using Ohmline.Charging.Api.Queries;
using Ohmline.Charging.ReadModels;
using Ohmline.Charging.ReadModels.Projections;

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
        /// <summary>
        /// The read models' database, for projections and query endpoints, and the projections'
        /// polling settings from <c>Ohmline:Projections</c>, checked at start.
        /// </summary>
        public IServiceCollection AddOhmlineReadModels(IConfiguration configuration)
        {
            services.AddOptions<ProjectionOptions>()
                .BindConfiguration(ProjectionOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            return configuration.GetConnectionString("ohmline") is { } postgres
                ? services.AddSingleton(_ => NpgsqlDataSource.Create(postgres))
                : services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>The query endpoints, if there is a database to answer from.</summary>
        public IEndpointRouteBuilder MapOhmlineReadModels(IConfiguration configuration) =>
            configuration.GetConnectionString("ohmline") is null
                ? endpoints
                : endpoints.MapChargingQueryEndpoints();
    }

    extension(AkkaConfigurationBuilder akka)
    {
        /// <summary>The projections, as sharded daemons, if there is a database for them.</summary>
        public AkkaConfigurationBuilder WithOhmlineReadModels(IConfiguration configuration) =>
            configuration.GetConnectionString("ohmline") is null
                ? akka
                : akka.AddChargingReadModels(
                    configuration.GetSection(ProjectionOptions.Section).Get<ProjectionOptions>());
    }
}
