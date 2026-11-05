using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.Charging.Domain.Listings;
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

    public ChargePointListingActor(ChargePointId chargePointId, TimeProvider clock)
        : base(TypeName, chargePointId.Value, clock) =>
        Command<GetChargePointListing>(_ => Sender.Tell(State.ToView(chargePointId)));

    /// <summary>Durable vocabulary: <c>charge-point-listing-OHM-SLC-0042</c>, for ever.</summary>
    public static string PersistenceIdOf(ChargePointId chargePointId) =>
        $"{TypeName}-{chargePointId}";

    /// <summary>
    /// For a test that starts the actor itself, with a clock of its choosing. In a node, the
    /// shard region has the container make it (section 12.3).
    /// </summary>
    public static Props Props(ChargePointId chargePointId, TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new ChargePointListingActor(chargePointId, clock));

    protected override object Accepted(long version) => new CommandAccepted(version);

    protected override object Rejected(ListingError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new CommandRejected(code, message);

    protected override object? ToSnapshot(ListingState state) => state.ToSnapshot();

    protected override ListingState FromSnapshot(object snapshot) =>
        ((ChargePointListingSnapshot)snapshot).ToState();
}
