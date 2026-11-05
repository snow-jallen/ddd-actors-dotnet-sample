using Ohmline.SharedKernel;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Domain.Budgets;
using static Ohmline.SiteEnergy.Actors.Tests.TheDepot;

namespace Ohmline.SiteEnergy.Actors.Tests;

public sealed class ErrorMappingTests
{
    private static readonly Dictionary<string, SitePowerBudgetError> Reasons = new()
    {
        [nameof(SitePowerBudgetError.NotConfigured)] = new SitePowerBudgetError.NotConfigured(Site),
        [nameof(SitePowerBudgetError.AlreadyConfigured)] =
            new SitePowerBudgetError.AlreadyConfigured(Site),
        [nameof(ValidationError)] = Result.Fail<ValidationError>(new Missing("Time zone")).Error,
        [nameof(SitePowerBudgetError.UnknownConnector)] =
            new SitePowerBudgetError.UnknownConnector(Fleet1),
        [nameof(SitePowerBudgetError.ConnectorHasOpenRequest)] =
            new SitePowerBudgetError.ConnectorHasOpenRequest(Fleet1, BusSession),
    };

    public static readonly TheoryData<string, string> Codes = new()
    {
        { nameof(SitePowerBudgetError.NotConfigured), "not-configured" },
        { nameof(SitePowerBudgetError.AlreadyConfigured), "already-configured" },
        { nameof(ValidationError), "invalid-configuration" },
        { nameof(SitePowerBudgetError.UnknownConnector), "unknown-connector" },
        { nameof(SitePowerBudgetError.ConnectorHasOpenRequest), "connector-has-open-request" },
    };

    [Theory]
    [MemberData(nameof(Codes))]
    public void Given_a_reason_when_mapped_then_the_reply_carries_its_code_and_message(
        string reason, string code)
    {
        var error = Reasons[reason];

        error.ToReply().ShouldBe(new BudgetCommandRejected(code, error.Message));
    }

    [Fact]
    public void Every_reason_a_budget_can_give_has_a_row()
    {
        var cases = typeof(SitePowerBudgetError).GetConstructors()
            .Select(constructor => constructor.GetParameters().Single().ParameterType.Name);

        Reasons.Keys.ShouldBe(cases, ignoreOrder: true);
    }
}
