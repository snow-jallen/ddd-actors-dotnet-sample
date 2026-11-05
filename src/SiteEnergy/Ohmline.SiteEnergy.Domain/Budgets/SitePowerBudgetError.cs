using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// Why a site's power budget refused a command. An invalid configuration is the shared kernel's
/// <see cref="ValidationError"/>, used as it is: a union can be a case of another union.
/// </summary>
public union SitePowerBudgetError(
    SitePowerBudgetError.NotConfigured,
    SitePowerBudgetError.AlreadyConfigured,
    ValidationError,
    SitePowerBudgetError.UnknownConnector,
    SitePowerBudgetError.ConnectorHasOpenRequest)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        NotConfigured error => $"Site {error.SiteId} is not configured.",
        AlreadyConfigured error => $"Site {error.SiteId} is already configured.",
        ValidationError error => $"The configuration is not valid: {error.Message}",
        UnknownConnector error => $"Connector {error.ConnectorId} is not at this site.",
        ConnectorHasOpenRequest error =>
            $"Connector {error.ConnectorId} already has a request, for session {error.SessionId}.",
        null => "This SitePowerBudgetError is default: it holds no case.",
    };

    public sealed record NotConfigured(SiteId SiteId);

    public sealed record AlreadyConfigured(SiteId SiteId);

    public sealed record UnknownConnector(ConnectorId ConnectorId);

    /// <summary>A connector hosts one session; its request must be released first.</summary>
    public sealed record ConnectorHasOpenRequest(ConnectorId ConnectorId, SessionId SessionId);
}
