using Akka.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Api.Tariffs;
using Ohmline.Charging.Contracts.ChargePoints;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Development;

/// <summary>
/// Temporary, and for Development only: until the Device Gateway connects real chargers
/// (section 16.7 deletes this file), these endpoints pretend to be a charger and report what a
/// charger reports: a car plugged in or unplugged, a fault, a repair. They also supply a tariff
/// catalog until the tariff book exists (section 12.8).
/// </summary>
public static class DevScaffoldingEndpoints
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddDevScaffolding() =>
            services.AddSingleton<ITariffCatalog, DevelopmentTariffCatalog>();
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapDevScaffoldingEndpoints()
        {
            var connector = endpoints
                .MapGroup("/dev/charge-points/{id}/connectors/{number:int}")
                .WithTags("Development: a pretend charger");
            connector.MapPost("/plug-in", PlugInAsync);
            connector.MapPost("/unplug", UnplugAsync);
            connector.MapPost("/fault", FaultAsync);
            connector.MapPost("/repaired", RepairedAsync);
            return endpoints;
        }
    }

    /// <summary>A car is plugged in, and the token presented starts this session.</summary>
    public sealed record PlugIn(Guid SessionId, string TokenUid);

    /// <summary>The charger's own error code, such as <c>GroundFailure</c>.</summary>
    public sealed record Fault(string ErrorCode);

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        PlugInAsync([AsParameters] Charger charger, PlugIn plugIn)
    {
        var fields = new RequestFields();
        var sessionId = fields.Read("sessionId", SessionId.TryCreate(plugIn.SessionId));
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(plugIn.TokenUid));
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        var number = charger.Number;
        return await charger.ReportAsync((command, chargePoint) =>
            new OccupyConnector(command, chargePoint, number, sessionId, tokenUid));
    }

    public static Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        UnplugAsync([AsParameters] Charger charger) =>
        charger.ReportAsync((command, chargePoint) =>
            new ReleaseConnector(command, chargePoint, charger.Number));

    public static Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        FaultAsync([AsParameters] Charger charger, Fault fault) =>
        charger.ReportAsync((command, chargePoint) =>
            new ReportConnectorFault(command, chargePoint, charger.Number, fault.ErrorCode));

    public static Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        RepairedAsync([AsParameters] Charger charger) =>
        charger.ReportAsync((command, chargePoint) =>
            new ClearConnectorFault(command, chargePoint, charger.Number));

    /// <summary>The connector in the route, and the services every report needs.</summary>
    public readonly record struct Charger(
        string Id,
        int Number,
        IRequiredActor<ChargePointRegion> ChargePoints,
        TimeProvider Clock,
        IOptions<ChargingApiOptions> Options,
        CancellationToken Aborted)
    {
        public async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
            ReportAsync(Func<CommandId, ChargePointId, ChargePointCommand> report)
        {
            if (!ChargePointId.TryCreate(Id).TryGet(out var chargePointId))
            {
                return Problems.NotFound($"'{Id}' is not a charge point id.");
            }

            return await Edge.SendAsync(
                ChargePoints.ActorRef,
                report(CommandId.New(Clock), chargePointId),
                $"/charge-points/{chargePointId}",
                Options.Value.AskTimeout,
                Aborted);
        }
    }
}
