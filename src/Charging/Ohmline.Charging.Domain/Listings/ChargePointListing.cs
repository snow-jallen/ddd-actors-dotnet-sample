using System.Collections.Immutable;
using Ohmline.Charging.Contracts.Listings;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;

namespace Ohmline.Charging.Domain.Listings;

/// <summary>
/// A charge point's listing as a decider. Nobody will ever ask how a listing read last March, so
/// it is state sourced: each revision records the whole new listing and a revision number, and
/// Evolve replaces the state with it. The decider still decides: it checks the text and
/// records nothing when nothing changed.
/// </summary>
public sealed class ChargePointListing
    : IDecider<ListingState, ChargePointListingCommand, ChargePointListingEvent, ListingError>
{
    public const int DisplayNameMaxLength = 60;

    public const int AccessInstructionsMaxLength = 500;

    public const int OpeningHoursMaxLength = 200;

    private ChargePointListing()
    {
    }

    public static ListingState Initial => ListingState.Initial;

    public static Result<ImmutableArray<ChargePointListingEvent>, ListingError> Decide(
        ListingState state, ChargePointListingCommand command, DecisionContext context) =>
        (state, command) switch
        {
            (_, ReviseChargePointListing revise) when Check(revise) is { } error => Reject(error),
            (Listed listed, ReviseChargePointListing revise) when Unchanged(listed, revise) =>
                NoChange,
            (Unlisted, ReviseChargePointListing revise) => Revision(revise, 1, context),
            (Listed listed, ReviseChargePointListing revise) =>
                Revision(revise, listed.Revision + 1, context),
            (null, _) => throw DefaultState(),
        };

    /// <summary>Total, and a replacement: the latest revision is the whole state.</summary>
    public static ListingState Evolve(ListingState state, ChargePointListingEvent @event) =>
        @event switch
        {
            ChargePointListingRevised revised => new Listed(
                revised.ChargePointId,
                revised.DisplayName,
                revised.AccessInstructions,
                revised.OpeningHours,
                revised.Revision),
        };

    private static ListingError? Check(ReviseChargePointListing revise) => revise switch
    {
        { DisplayName: var name } when string.IsNullOrWhiteSpace(name) =>
            new ListingError.DisplayNameMissing(),
        { DisplayName.Length: > DisplayNameMaxLength } => TooLong(
            "display name", revise.DisplayName, DisplayNameMaxLength),
        { AccessInstructions.Length: > AccessInstructionsMaxLength } => TooLong(
            "access instructions", revise.AccessInstructions, AccessInstructionsMaxLength),
        { OpeningHours.Length: > OpeningHoursMaxLength } => TooLong(
            "opening hours", revise.OpeningHours, OpeningHoursMaxLength),
        _ => default(ListingError?),
    };

    private static ListingError TooLong(string field, string text, int maxLength) =>
        new ListingError.TextTooLong(field, text.Length, maxLength);

    private static bool Unchanged(Listed listed, ReviseChargePointListing revise) =>
        listed.DisplayName == revise.DisplayName
        && listed.AccessInstructions == revise.AccessInstructions
        && listed.OpeningHours == revise.OpeningHours;

    private static Result<ImmutableArray<ChargePointListingEvent>, ListingError> Revision(
        ReviseChargePointListing revise, long revision, DecisionContext context) =>
        Result.Ok<ImmutableArray<ChargePointListingEvent>>(
        [
            new ChargePointListingRevised(
                revise.ChargePointId,
                revise.DisplayName,
                revise.AccessInstructions,
                revise.OpeningHours,
                revision)
            {
                CommandId = revise.CommandId,
                OccurredAt = context.Now,
            },
        ]);

    private static Result<ImmutableArray<ChargePointListingEvent>, ListingError> NoChange { get; } =
        Result.Ok(ImmutableArray<ChargePointListingEvent>.Empty);

    private static Result<ImmutableArray<ChargePointListingEvent>, ListingError> Reject(
        ListingError error) => Result.Fail(error);

    private static InvalidOperationException DefaultState() =>
        new("The state is default: it holds no case. Start from ListingState.Initial.");
}
