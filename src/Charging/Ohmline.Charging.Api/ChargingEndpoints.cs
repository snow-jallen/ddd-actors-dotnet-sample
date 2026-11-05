using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        /// <summary>The edge's settings, from <c>Ohmline:Charging</c>, checked at start.</summary>
        public IServiceCollection AddChargingApi()
        {
            services.AddOptions<ChargingApiOptions>()
                .BindConfiguration(ChargingApiOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
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

            return endpoints;
        }
    }
}
