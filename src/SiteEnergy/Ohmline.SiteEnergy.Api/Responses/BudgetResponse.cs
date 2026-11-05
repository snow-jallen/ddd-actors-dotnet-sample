using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Api.Responses;

/// <summary>How a site's power is shared out now, in kW, with each open request in order.</summary>
public sealed record BudgetResponse(
    string SiteId,
    long Version,
    decimal EffectiveCapacityKw,
    decimal CurtailmentKw,
    IReadOnlyList<RequestResponse> Requests)
{
    public static BudgetResponse From(SitePowerBudgetView view) => new(
        view.SiteId.Value,
        view.Version,
        view.EffectiveCapacity.Kilowatts,
        view.Curtailment.Kilowatts,
        [
            .. view.Requests.Select(request => new RequestResponse(
                request.ConnectorId.ToString(),
                request.SessionId.Value,
                request.Requested.Kilowatts,
                request.Allocated?.Kilowatts)),
        ]);
}

/// <summary>A session's request; no allocation while it waits in the queue.</summary>
public sealed record RequestResponse(
    string ConnectorId, Guid SessionId, decimal RequestedKw, decimal? AllocatedKw);
