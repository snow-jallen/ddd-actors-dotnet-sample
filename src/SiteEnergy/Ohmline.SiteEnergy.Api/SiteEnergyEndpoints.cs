using System.Collections.Immutable;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Api.Requests;
using Ohmline.SiteEnergy.Api.Responses;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Api;

/// <summary>Site Energy's HTTP edge for operators: configure a site, read its budget.</summary>
public static class SiteEnergyEndpoints
{
    extension(IServiceCollection services)
    {
        /// <summary>Settings from <c>Ohmline:SiteEnergy</c>, checked at the start.</summary>
        public IServiceCollection AddSiteEnergyApi()
        {
            services.AddOptions<SiteEnergyApiOptions>()
                .BindConfiguration(SiteEnergyApiOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapSiteEnergyEndpoints()
        {
            var sites = endpoints.MapGroup("/sites").WithTags("Sites");
            sites.MapPost("", ConfigureAsync);
            sites.MapGet("/{id}/budget", GetBudgetAsync);
            return endpoints;
        }
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        ConfigureAsync(
            SiteRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<SitePowerBudgetRegion> budgets,
            TimeProvider clock,
            IOptions<SiteEnergyApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        var siteId = fields.Read("siteId", SiteId.TryCreate(request.SiteId));
        var gridLimit = fields.Read("gridLimitKw", Power.TryCreate(request.GridLimitKw));
        var margin = fields.Read("safetyMarginKw", Power.TryCreate(request.SafetyMarginKw));
        ImmutableArray<ConnectorLimits> connectors =
            [.. (request.Connectors ?? []).Select((connector, index) =>
                Limits(fields, $"connectors[{index}]", connector))];
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        var configure = new ConfigureSite(
            commandId, siteId, gridLimit, margin, request.TimeZoneId ?? "", connectors);
        var timeout = options.Value.AskTimeout;
        try
        {
            var reply = await budgets.ActorRef.AskTraced<BudgetCommandReply>(
                configure, timeout, cancellationToken);
            return reply switch
            {
                BudgetCommandAccepted accepted => TypedResults.Accepted(
                    $"/sites/{siteId}/budget", new CommandAcceptedResponse(accepted.Version)),
                BudgetCommandRejected rejected => Problems.Rejected(rejected),
                PowerAllocated or QueuedForPower => throw new InvalidOperationException(
                    $"ConfigureSite is not answered with {reply.GetType().Name}."),
            };
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }

    /// <summary>The budget as the aggregate knows it now: never stale.</summary>
    public static async Task<Results<Ok<BudgetResponse>, ProblemHttpResult>> GetBudgetAsync(
        string id,
        IRequiredActor<SitePowerBudgetRegion> budgets,
        IOptions<SiteEnergyApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!SiteId.TryCreate(id).TryGet(out var siteId))
        {
            return Problems.NotFound($"'{id}' is not a site id.");
        }

        var timeout = options.Value.AskTimeout;
        try
        {
            var view = await budgets.ActorRef.AskTraced<SitePowerBudgetView>(
                new GetSitePowerBudget(siteId), timeout, cancellationToken);
            return view.Version > 0
                ? TypedResults.Ok(BudgetResponse.From(view))
                : Problems.NotFound($"Site {siteId} is not configured.");
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }

    private static ConnectorLimits Limits(
        RequestFields fields, string field, ConnectorRequest connector) => new(
        fields.Read($"{field}.connectorId", ConnectorId.TryCreate(connector.ConnectorId)),
        fields.Read($"{field}.minPowerKw", Power.TryCreate(connector.MinPowerKw)),
        fields.Read($"{field}.maxPowerKw", Power.TryCreate(connector.MaxPowerKw)),
        connector.Priority switch
        {
            "fleet" => new Fleet(),
            "public" => new Public(),
            _ => Unknown(fields, $"{field}.priority", connector.Priority),
        });

    private static PriorityClass Unknown(RequestFields fields, string field, string? priority)
    {
        fields.Invalid(field, $"Priority must be fleet or public, not '{priority}'.");
        return new Public();
    }
}
