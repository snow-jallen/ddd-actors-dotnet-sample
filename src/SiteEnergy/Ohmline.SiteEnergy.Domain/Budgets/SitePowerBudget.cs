using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.SiteEnergy.Domain.Sites;

namespace Ohmline.SiteEnergy.Domain.Budgets;

/// <summary>
/// A site's power budget as a decider. Its invariant: after every decision, the allocations add
/// up to no more than the effective capacity, and each stays within its connector's minimum and
/// maximum. Every change (a request, a release, a curtailment) runs the
/// <see cref="FairShareAllocator"/> over all open requests and records what changed.
/// </summary>
public sealed class SitePowerBudget
    : IDecider<SitePowerBudgetState, SitePowerBudgetCommand, SitePowerBudgetEvent,
        SitePowerBudgetError>
{
    private SitePowerBudget()
    {
    }

    public static SitePowerBudgetState Initial => SitePowerBudgetState.Initial;

    /// <summary>
    /// Decides, then stamps every event of the decision with the command that caused it and
    /// the context's time.
    /// </summary>
    public static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> Decide(
        SitePowerBudgetState state, SitePowerBudgetCommand command, DecisionContext context) =>
        DecisionTable(state, command).Map(events => Stamp(events, command, context));

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError>
        DecisionTable(SitePowerBudgetState state, SitePowerBudgetCommand command) =>
        (state, command) switch
        {
            (Unconfigured, ConfigureSite configure) => Validate(configure).Match(
                valid => Accept(new SiteConfigured(configure.SiteId, valid)),
                invalid => Reject(invalid)),
            (Unconfigured,
                RequestAllocation or ReleaseAllocation or ApplyCurtailment or EndCurtailment) =>
                Reject(new SitePowerBudgetError.NotConfigured(command.SiteId)),
            (Configured, ConfigureSite) =>
                Reject(new SitePowerBudgetError.AlreadyConfigured(command.SiteId)),

            (Configured site, RequestAllocation request) => Request(site, request),
            (Configured site, ReleaseAllocation release) => Release(site, release),

            (Configured site, ApplyCurtailment same) when site.Curtailment == same.Reduction =>
                NoChange,
            (Configured site, ApplyCurtailment curtail) => Accept(
                [
                    new CurtailmentApplied(curtail.SiteId, curtail.Reduction),
                    .. Rebalance(site with { Curtailment = curtail.Reduction }),
                ]),
            (Configured site, EndCurtailment) when site.Curtailment == Power.Zero => NoChange,
            (Configured site, EndCurtailment end) => Accept(
                [
                    new CurtailmentEnded(end.SiteId),
                    .. Rebalance(site with { Curtailment = Power.Zero }),
                ]),

            (null, _) => throw DefaultState(),
        };

    /// <summary>
    /// Applies a recorded decision. Allocations come from the events, never from running the
    /// allocator again, so replay gives exactly what the chargers were told.
    /// </summary>
    public static SitePowerBudgetState Evolve(
        SitePowerBudgetState state, SitePowerBudgetEvent @event) =>
        (state, @event) switch
        {
            (Unconfigured, SiteConfigured configured) => Configured.Of(configured.Configuration),
            (Configured site, AllocationGranted granted) => site.Open(
                new OpenRequest(granted.ConnectorId, granted.SessionId, granted.Requested),
                granted.Granted),
            (Configured site, AllocationQueued queued) => site.Open(
                new OpenRequest(queued.ConnectorId, queued.SessionId, queued.Requested), null),
            (Configured site, AllocationsRebalanced rebalanced) =>
                site with { Allocations = rebalanced.Allocations },
            (Configured site, AllocationReleased released) => site.Close(released.ConnectorId),
            (Configured site, CurtailmentApplied applied) =>
                site with { Curtailment = applied.Reduction },
            (Configured site, CurtailmentEnded) => site with { Curtailment = Power.Zero },

            // An event that meets a state Decide would not have produced it in changes nothing.
            (Unconfigured or Configured, SitePowerBudgetEvent) => state,
            (null, _) => throw DefaultState(),
        };

    public static SitePowerBudgetState Fold(IEnumerable<SitePowerBudgetEvent> events) =>
        Decider.Fold<SitePowerBudget, SitePowerBudgetState, SitePowerBudgetCommand,
            SitePowerBudgetEvent, SitePowerBudgetError>(events);

    /// <summary>The factory checks the whole configuration with the smart constructors.</summary>
    private static Result<SiteConfiguration, ValidationError> Validate(ConfigureSite configure)
    {
        Result<ImmutableArray<ConnectorConfiguration>, ValidationError> connectors =
            Result.Ok(ImmutableArray<ConnectorConfiguration>.Empty);
        foreach (var limits in configure.Connectors)
        {
            connectors = connectors.Bind(valid => ConnectorConfiguration
                .TryCreate(limits.ConnectorId, limits.MinPower, limits.MaxPower, limits.Priority)
                .Map(valid.Add));
        }

        return connectors.Bind(valid => SiteConfiguration.TryCreate(
            configure.SiteId, configure.GridLimit, configure.SafetyMargin, configure.TimeZoneId,
            valid));
    }

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> Request(
        Configured site, RequestAllocation request)
    {
        if (site.Site.Connectors.All(connector => connector.ConnectorId != request.ConnectorId))
        {
            return Reject(new SitePowerBudgetError.UnknownConnector(request.ConnectorId));
        }

        if (site.RequestFor(request.ConnectorId) is { } open)
        {
            return open.SessionId == request.SessionId
                ? NoChange
                : Reject(new SitePowerBudgetError.ConnectorHasOpenRequest(
                    open.ConnectorId, open.SessionId));
        }

        var opened = new OpenRequest(request.ConnectorId, request.SessionId, request.Requested);
        var withRequest = site with { Requests = site.Requests.Add(opened) };
        var allocations = Allocate(withRequest);
        var claim = withRequest.AllocationRequests[^1];

        SitePowerBudgetEvent answer = AllocationDecision.Of(claim, allocations) switch
        {
            Granted granted => Granted(request, granted.Allocation.Power),
            Reduced reduced => Granted(request, reduced.Allocation.Power),
            Queued => new AllocationQueued(
                request.SiteId, request.ConnectorId, request.SessionId, request.Requested),
            null => throw new InvalidOperationException("The allocation decision is default."),
        };
        var answered = answer is AllocationGranted { Granted: var power }
            ? site.Open(opened, power)
            : site.Open(opened, null);

        return Accept([answer, .. Rebalanced(answered, allocations)]);
    }

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> Release(
        Configured site, ReleaseAllocation release)
    {
        if (site.RequestFor(release.ConnectorId) is not { } open
            || open.SessionId != release.SessionId)
        {
            return NoChange;
        }

        return Accept(
            [
                new AllocationReleased(release.SiteId, release.ConnectorId, release.SessionId),
                .. Rebalance(site.Close(release.ConnectorId)),
            ]);
    }

    private static ImmutableArray<SitePowerBudgetEvent> Stamp(
        ImmutableArray<SitePowerBudgetEvent> events,
        SitePowerBudgetCommand command,
        DecisionContext context) =>
        [.. events.Select(@event =>
            @event with { CommandId = command.CommandId, OccurredAt = context.Now })];

    private static AllocationGranted Granted(RequestAllocation request, Power power) =>
        new(request.SiteId, request.ConnectorId, request.SessionId, request.Requested, power);

    private static ImmutableArray<PowerAllocation> Allocate(Configured site) =>
        FairShareAllocator.Allocate(site.EffectiveCapacity, site.AllocationRequests);

    /// <summary>Runs the allocator after a change; records the table if anything moved.</summary>
    private static ImmutableArray<SitePowerBudgetEvent> Rebalance(Configured site) =>
        Rebalanced(site, Allocate(site));

    private static ImmutableArray<SitePowerBudgetEvent> Rebalanced(
        Configured site, ImmutableArray<PowerAllocation> allocations) =>
        allocations.SequenceEqual(site.Allocations)
            ? []
            : [new AllocationsRebalanced(site.Site.SiteId, allocations)];

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> NoChange
    { get; } = Accept();

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> Accept(
        params ImmutableArray<SitePowerBudgetEvent> events) => Result.Ok(events);

    private static Result<ImmutableArray<SitePowerBudgetEvent>, SitePowerBudgetError> Reject(
        SitePowerBudgetError error) => Result.Fail(error);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from SitePowerBudgetState.Initial.");
}
