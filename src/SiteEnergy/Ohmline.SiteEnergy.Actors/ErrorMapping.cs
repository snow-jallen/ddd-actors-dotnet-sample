using Ohmline.SharedKernel;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Domain.Budgets;

namespace Ohmline.SiteEnergy.Actors;

/// <summary>
/// From the budget's reasons to Site Energy's wire reply, with an exhaustive switch. Codes are
/// published vocabulary: add new ones, never change or reuse one.
/// </summary>
public static class ErrorMapping
{
    extension(SitePowerBudgetError error)
    {
        public string Code => error switch
        {
            SitePowerBudgetError.NotConfigured => "not-configured",
            SitePowerBudgetError.AlreadyConfigured => "already-configured",
            ValidationError => "invalid-configuration",
            SitePowerBudgetError.UnknownConnector => "unknown-connector",
            SitePowerBudgetError.ConnectorHasOpenRequest => "connector-has-open-request",
            null => throw new InvalidOperationException(
                "This SitePowerBudgetError is default: it holds no case to report."),
        };

        public BudgetCommandRejected ToReply() => new(error.Code, error.Message);
    }
}
