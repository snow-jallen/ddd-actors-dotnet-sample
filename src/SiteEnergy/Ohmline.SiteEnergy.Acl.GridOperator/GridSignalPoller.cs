using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.SharedKernel.Identity;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.SiteEnergy.Acl.GridOperator;

/// <summary>
/// Asks Northgate Grid for its demand-response events on a timer, and tells each site's power
/// budget what they mean now (section 16.8): <c>ApplyCurtailment</c> while an event is in
/// force, <c>EndCurtailment</c> once none is. <see cref="GridSignalTranslator"/> translates;
/// this actor only keeps the events it has read, asks, and remembers what each site was told.
/// </summary>
/// <remarks>
/// It runs as a cluster singleton, so one poller asks Northgate. What it remembers is in
/// memory: one that takes over reads Northgate's feed from the start and tells every site what
/// it should be doing, once. The commands' ids come from Northgate's event revisions, so a site
/// told again is told the same command, and its budget ignores what it already knows.
/// </remarks>
public sealed class GridSignalPoller : ReceiveActor, IWithTimers
{
    private readonly INorthgateFeed _northgate;
    private readonly IRequiredActor<SitePowerBudgetRegion> _budgets;
    private readonly GridOperatorOptions _options;
    private readonly IReadOnlyDictionary<string, SiteId> _sites;
    private readonly TimeProvider _clock;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly Dictionary<string, DemandResponseEvent> _events = [];
    private readonly Dictionary<SiteId, GridInstruction> _told = [];
    private readonly HashSet<SiteId> _telling = [];
    private long _seen;

    public GridSignalPoller(
        INorthgateFeed northgate,
        IRequiredActor<SitePowerBudgetRegion> budgets,
        IOptions<GridOperatorOptions> options,
        TimeProvider clock)
    {
        _northgate = northgate;
        _budgets = budgets;
        _options = options.Value;
        _sites = _options.Sites.ToImmutableDictionary(
            site => site.Key, site => new SiteId(site.Value));
        _clock = clock;

        Receive<Poll>(_ => _northgate.SinceAsync(_seen, CancellationToken.None).PipeTo(
            Self,
            success: signals => new Polled(signals),
            failure: cause => new PollFailed(cause)));
        Receive<Polled>(polled =>
        {
            foreach (var revision in polled.Signals.Events)
            {
                _events[revision.EventId] = revision;
            }

            _seen = Math.Max(_seen, polled.Signals.Latest);
            TellWhatChanged();
        });
        Receive<PollFailed>(failed =>
        {
            _log.Warning(failed.Cause, "Northgate did not answer; asking again later");
            TellWhatChanged(); // An event may have begun or ended since: time moves anyway.
        });
        Receive<Told>(told =>
        {
            _telling.Remove(told.Instruction.SiteId);
            _told[told.Instruction.SiteId] = told.Instruction;
            if (told.Reply is BudgetCommandRejected rejected)
            {
                _log.Warning(
                    "{0} refused {1}: {2}",
                    told.Instruction.SiteId,
                    told.Instruction.Value,
                    rejected);
            }
        });
        Receive<NotTold>(notTold =>
        {
            _telling.Remove(notTold.Site);
            _log.Warning(
                notTold.Cause, "{0} did not answer; it is told again later", notTold.Site);
        });
    }

    public ITimerScheduler Timers { get; set; } = null!;

    protected override void PreStart() => Timers.StartPeriodicTimer(
        nameof(Poll), Poll.Instance, TimeSpan.Zero, _options.PollInterval);

    /// <summary>Each site whose instruction is not what it was last told is told now.</summary>
    private void TellWhatChanged()
    {
        var desired = GridSignalTranslator.Desired(_events.Values, _sites, _clock.GetUtcNow());
        foreach (var (site, instruction) in desired)
        {
            if (_telling.Contains(site)
                || (_told.TryGetValue(site, out var told) && told.Equals(instruction)))
            {
                continue;
            }

            _telling.Add(site);
            _log.Info("{0}: {1}", site, instruction.Value);
            var command = GridSignalTranslator.Command(instruction);
            _budgets.ActorRef
                .AskTraced<BudgetCommandReply>(command, _options.AskTimeout)
                .PipeTo(
                    Self,
                    success: reply => new Told(instruction, reply),
                    failure: cause => new NotTold(site, cause));
        }
    }

    private sealed record Poll : INoSerializationVerificationNeeded
    {
        public static Poll Instance { get; } = new();
    }

    private sealed record Polled(NorthgateSignals Signals) : INoSerializationVerificationNeeded;

    private sealed record PollFailed(Exception Cause) : INoSerializationVerificationNeeded;

    private sealed record Told(GridInstruction Instruction, BudgetCommandReply Reply)
        : INoSerializationVerificationNeeded;

    private sealed record NotTold(SiteId Site, Exception Cause)
        : INoSerializationVerificationNeeded;
}
