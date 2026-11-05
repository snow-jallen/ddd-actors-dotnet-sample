using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Ohmline.SharedKernel.Units;
using Ohmline.SiteEnergy.Acl.GridOperator.Northgate;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;
using Ohmline.Testing;
using static Ohmline.SiteEnergy.Acl.GridOperator.Tests.Northgate;

namespace Ohmline.SiteEnergy.Acl.GridOperator.Tests;

/// <summary>
/// The poller between a stand-in for Northgate's feed and a probe as the site budgets: each
/// site is told when what it should do changes, and only then.
/// </summary>
public sealed class GridSignalPollerTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.Config, nameof(GridSignalPollerTests), output)
{
    private readonly FakeTimeProvider _clock = new(FivePm.AddMinutes(10));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_an_event_in_force_then_the_site_is_curtailed_once_and_restored_after()
    {
        var feed = new Feed(Event(-60m));
        var budgets = Budgets();
        Poller(feed, budgets);

        var curtail = await budgets.ExpectMsgAsync<ApplyCurtailment>(cancellationToken: Ct);
        curtail.Reduction.ShouldBe(new Power(60m));
        await budgets.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), Ct);
        _clock.Advance(TimeSpan.FromHours(2));

        (await budgets.ExpectMsgAsync<EndCurtailment>(cancellationToken: Ct)).SiteId
            .ShouldBe(Depot);
    }

    [Fact]
    public async Task Given_a_revision_that_cancels_the_event_then_the_site_is_restored()
    {
        var feed = new Feed(Event(-60m));
        var budgets = Budgets();
        Poller(feed, budgets);
        await budgets.ExpectMsgAsync<ApplyCurtailment>(cancellationToken: Ct);

        feed.Publish(Event(-60m, status: "cancelled", modification: 1, sequence: 2));

        await budgets.ExpectMsgAsync<EndCurtailment>(cancellationToken: Ct);
    }

    [Fact]
    public async Task Given_a_site_that_did_not_answer_then_it_is_told_again()
    {
        var feed = new Feed(Event(-60m));
        var budgets = CreateTestProbe();
        Poller(feed, budgets, askTimeout: TimeSpan.FromMilliseconds(500));

        var first = await budgets.ExpectMsgAsync<ApplyCurtailment>(cancellationToken: Ct);
        var again = await budgets.ExpectMsgAsync<ApplyCurtailment>(
            TimeSpan.FromSeconds(5), cancellationToken: Ct);

        again.ShouldBe(first);
    }

    private TestProbe Budgets()
    {
        var budgets = CreateTestProbe();
        budgets.SetAutoPilot(new DelegateAutoPilot((sender, _) =>
        {
            sender.Tell(new BudgetCommandAccepted(Version: 2));
            return AutoPilot.KeepRunning;
        }));
        return budgets;
    }

    /// <summary>
    /// The poller, asking every 100 ms. A budget that answers is given time to, however busy the
    /// machine: an answer that comes too late is no answer, and the site would be told again.
    /// </summary>
    private void Poller(Feed feed, TestProbe budgets, TimeSpan? askTimeout = null)
    {
        var options = Options.Create(new GridOperatorOptions
        {
            Enabled = true,
            PollInterval = TimeSpan.FromMilliseconds(100),
            AskTimeout = askTimeout ?? Dilated(TimeSpan.FromSeconds(3)),
            Sites = new() { ["NG-VEN-0042"] = "slc-depot" },
        });
        Sys.ActorOf(Props.Create(() => new GridSignalPoller(
            feed, new Region<SitePowerBudgetRegion>(budgets.Ref), options, _clock)));
    }

    /// <summary>Northgate's feed, with the revisions the test publishes.</summary>
    private sealed class Feed(params DemandResponseEvent[] revisions) : INorthgateFeed
    {
        private readonly ConcurrentQueue<DemandResponseEvent> _revisions = new(revisions);

        public void Publish(DemandResponseEvent revision) => _revisions.Enqueue(revision);

        public Task<NorthgateSignals> SinceAsync(long sequence, CancellationToken cancellationToken)
        {
            var all = _revisions.ToArray();
            return Task.FromResult(new NorthgateSignals(
                [.. all.Where(revision => revision.Sequence > sequence)],
                all.Max(revision => revision.Sequence)));
        }
    }

    private sealed class Region<TKey>(IActorRef actor) : IRequiredActor<TKey>
    {
        public IActorRef ActorRef => actor;

        public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(actor);
    }
}
