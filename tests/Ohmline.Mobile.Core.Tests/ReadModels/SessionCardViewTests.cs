using Ohmline.Charging.Client.Contracts;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Mobile.Core.Outbox;
using Ohmline.Mobile.Core.ReadModels;
using static Ohmline.Mobile.Core.Tests.ReadModels.Cards;

namespace Ohmline.Mobile.Core.Tests.ReadModels;

/// <summary>The card and its overlay, as a pure function of the cache and the outbox.</summary>
public sealed class SessionCardViewTests
{
    [Fact]
    public void Given_a_queued_stop_then_the_card_says_stopping()
    {
        var view = SessionCardView.Merge([At(2)], [Stop(OutboxStatus.Queued)]);

        view.ShouldBe(new SessionCardView(At(2), SessionCardView.Stopping, null));
    }

    [Fact]
    public void Given_a_stop_accepted_at_a_version_the_card_has_not_reached_then_still_stopping()
    {
        var view = SessionCardView.Merge([At(2)], [Stop(OutboxStatus.Accepted, version: 3)]);

        view.Pending.ShouldBe(SessionCardView.Stopping);
    }

    [Fact]
    public void Given_the_card_at_the_stops_version_then_no_overlay()
    {
        var ended = At(3, 12m, ended: true);

        var view = SessionCardView.Merge([ended], [Stop(OutboxStatus.Accepted, version: 3)]);

        view.ShouldBe(new SessionCardView(ended, null, null));
    }

    [Fact]
    public void Given_a_start_whose_session_has_no_card_yet_then_starting()
    {
        var view = SessionCardView.Merge([], [Start(OutboxStatus.Accepted, version: 1)]);

        view.ShouldBe(new SessionCardView(null, SessionCardView.Starting, null));
    }

    [Fact]
    public void Given_a_start_whose_session_has_its_card_then_no_overlay()
    {
        SessionCardView.Merge([At(1)], [Start(OutboxStatus.Accepted, version: 1)])
            .Pending.ShouldBeNull();
    }

    [Fact]
    public void Given_a_rejected_start_then_the_driver_reads_why()
    {
        var view = SessionCardView.Merge(
            [], [Start(OutboxStatus.Rejected) with { Code = RejectionCodes.ConnectorFaulted }]);

        view.ShouldBe(new SessionCardView(
            null, null, "This connector has a fault and is waiting for repair."));
    }

    [Fact]
    public void Given_a_start_that_failed_to_sync_then_the_driver_is_told_to_try_again()
    {
        SessionCardView.Merge([], [Start(OutboxStatus.FailedToSync)])
            .Problem.ShouldBe(SessionCardView.NotSent);
    }

    [Fact]
    public void Given_an_ended_card_and_a_charging_one_then_the_charging_one_is_shown()
    {
        var earlier = At(3, 12m, ended: true) with { SessionId = Guid.CreateVersion7() };

        SessionCardView.Merge([earlier, At(1)], []).Card.ShouldBe(At(1));
    }

    private static OutboxRow Start(string status, long? version = null) =>
        Row(OutboxKinds.StartCharging, status, version);

    private static OutboxRow Stop(string status, long? version = null) =>
        Row(OutboxKinds.StopCharging, status, version);

    private static OutboxRow Row(string kind, string status, long? version) => new(
        Guid.CreateVersion7(Morning),
        kind,
        Session.ToString(),
        Payload: "{}",
        Morning,
        Attempts: 1,
        status,
        version,
        Code: null,
        Reason: null);
}
