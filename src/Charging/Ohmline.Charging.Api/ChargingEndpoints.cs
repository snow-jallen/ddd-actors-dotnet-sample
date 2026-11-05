using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ohmline.Charging.Api.Queries;
using Ohmline.Charging.Api.Tariffs;

namespace Ohmline.Charging.Api;

/// <summary>
/// Charging's HTTP edge: application services that turn a request into a command or a
/// question for a shard region, and its answer into a response. They hold no business rule;
/// the aggregates do.
/// </summary>
public static class ChargingEndpoints
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The edge's settings, from <c>Ohmline:Charging</c> and <c>Ohmline:ReadModels</c>,
        /// checked at start, read-your-writes for the query endpoints, and the tariff book as
        /// the catalog a starting session copies its tariff from.
        /// </summary>
        public IServiceCollection AddChargingApi()
        {
            services.AddOptions<ChargingApiOptions>()
                .BindConfiguration(ChargingApiOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddOptions<ReadModelOptions>()
                .BindConfiguration(ReadModelOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<ReadYourWrites>();
            services.AddSingleton<ITariffCatalog, TariffBookCatalog>();
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapChargingEndpoints()
        {
            var chargePoints = endpoints.MapGroup("/charge-points").WithTags("Charge points");
            chargePoints.MapPost("", ChargePointEndpoints.RegisterAsync);
            chargePoints.MapGet("/{id}", ChargePointEndpoints.GetAsync);
            chargePoints.MapPost(
                "/{id}/connectors/{number:int}/reservations", ChargePointEndpoints.ReserveAsync);
            chargePoints.MapGet("/{id}/listing", ListingEndpoints.GetAsync);
            chargePoints.MapPut("/{id}/listing", ListingEndpoints.ReviseAsync);

            var sessions = endpoints.MapGroup("/sessions").WithTags("Sessions");
            sessions.MapPost("", SessionEndpoints.StartAsync);
            sessions.MapPost("/{id:guid}/meter-values", SessionEndpoints.RecordMeterValueAsync);
            sessions.MapPost("/{id:guid}/stop", SessionEndpoints.StopAsync);

            var tariffs = endpoints.MapGroup("/tariffs").WithTags("Tariffs");
            tariffs.MapPost("", TariffEndpoints.PublishAsync);
            tariffs.MapGet("/{id}", TariffEndpoints.GetAsync);

            return endpoints;
        }
    }
}
