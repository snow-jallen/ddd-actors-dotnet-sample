using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ohmline.Charging.Api.Queries;

/// <summary>
/// The end of the read side's pipeline: endpoints that answer from the read models' tables and
/// never touch an aggregate. A host maps them only when it has a read-model database.
/// </summary>
public static class QueryEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapChargingQueryEndpoints()
        {
            endpoints.MapGet("/projections/status", ProjectionStatusEndpoint.GetAsync);
            endpoints.MapGet("/sessions/{id:guid}", SessionEndpoint.GetAsync);
            endpoints.MapGet("/sites/{id}/live-board", SiteLiveBoardEndpoint.GetAsync);
            endpoints.MapGet("/tokens/{uid}/sessions", TokenSessionsEndpoint.GetAsync);
            return endpoints;
        }
    }
}
