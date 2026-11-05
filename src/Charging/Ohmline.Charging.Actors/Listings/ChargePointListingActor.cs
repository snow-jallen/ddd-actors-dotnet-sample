using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Actors.Diagnostics;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.Listings;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Actors.Listings;

/// <summary>
/// One charge point's listing in the shell, state sourced: after each revision the journal
/// keeps a snapshot of the listing and nothing before it. The rules are in
/// <see cref="ChargePointListing"/>; what is kept is <see cref="StateSourcedDeciderActor{
/// TDecider, TState, TCommand, TEvent, TError}"/>'s business.
/// </summary>
public sealed class ChargePointListingActor
    : StateSourcedDeciderActor<ChargePointListing, ListingState, ChargePointListingCommand,
        ChargePointListingEvent, ListingError>
{
    /// <summary>Durable vocabulary: the shard type name and the persistence id prefix.</summary>
    public const string TypeName = "charge-point-listing";

    private readonly ChargingMetrics _metrics;

    public ChargePointListingActor(
        ChargePointId chargePointId, TimeProvider clock, ChargingMetrics metrics)
        : base(TypeName, chargePointId.Value, clock)
    {
        _metrics = metrics;
        Command<GetChargePointListing>(_ => Sender.Tell(State.ToView(chargePointId)));
    }

    /// <summary>Durable vocabulary: <c>charge-point-listing-OHM-SLC-0042</c>, for ever.</summary>
    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"{TypeName}-{chargePointId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing, and instruments
    /// of its choosing or none a host collects. In a node, the shard region has the container
    /// make it (section 12.3).
    /// </summary>
    public static Props Props(
        ChargePointId chargePointId, TimeProvider clock, ChargingMetrics? metrics = null)
    {
        var instruments = metrics ?? ChargingMetrics.Unhosted;
        return Akka.Actor.Props.Create(() =>
            new ChargePointListingActor(chargePointId, clock, instruments));
    }

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(ListingError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override CommandId CommandIdOf(ChargePointListingCommand command) =>
        command.CommandId;

    protected override CommandId CausationOf(ChargePointListingEvent @event) => @event.CommandId;

    protected override void OnDecided(
        ChargePointListingCommand command,
        Result<ImmutableArray<ChargePointListingEvent>, ListingError> decision) =>
        _metrics.Decided(TypeName, PersistenceId, command, decision);

    protected override object? ToSnapshot(
        ListingState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (ListingState State, ImmutableArray<RecentCommand> Recent) FromSnapshot(
        object snapshot)
    {
        var stored = (ChargePointListingSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }
}
