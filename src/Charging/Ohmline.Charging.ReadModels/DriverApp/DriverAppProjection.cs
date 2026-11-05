using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.ReadModels.DriverApp;

/// <summary>
/// The driver app's read model (section 14.6), from the same events as the session history:
/// each token's session cards, with the charge point's display name in them, and a per-token
/// change feed that an app catches up from after it has been away. Each new card is announced
/// as a <see cref="DriverCardChanged"/> once it is committed.
/// </summary>
/// <remarks>
/// The display name is denormalised from the listing's current view (section 11.7), asked once
/// when a card is made, and again on the session's next events only while the card has none.
/// A card keeps the name the charge point had when the session started, like a receipt; a
/// rebuild asks again, and gets the names of the day it runs.
/// </remarks>
public sealed class DriverAppProjection(IChargePointNames? names = null) : IProjectionHandler
{
    /// <summary>Durable: its offsets are kept under it. A new table shape gets a new id.</summary>
    public const string Id = "driver-app-v1";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.ChargingSession];

    /// <summary>
    /// Cards that stop moving for every driver are worse than one card that misses an event:
    /// the session's next event corrects it. The receipt reads the session history, which
    /// stops instead.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new RecordAndSkip(MaxRetries: 5);

    /// <summary>In a node, names come from the listings' shard region.</summary>
    public IProjectionHandler RunningIn(ActorSystem system) =>
        new DriverAppProjection(new ListingDisplayNames(system));

    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        if (envelope.Event is not SessionEvent @event)
        {
            return [];
        }

        var card = await DriverAppTables.FindCardAsync(
            transaction, @event.SessionId.Value, cancellationToken);
        if (card?.Version >= envelope.SequenceNr)
        {
            return []; // Seen it: a redelivery, or a replay from an older offset.
        }

        var displayName = card?.ChargePointDisplayName
            ?? await NameAsync(ChargePointOf(card, @event), cancellationToken);
        if (DriverAppCard.Apply(card, @event, envelope.SequenceNr, displayName) is not { } next)
        {
            return [];
        }

        await DriverAppTables.UpsertCardAsync(transaction, next, cancellationToken);
        var position =
            await DriverAppTables.AppendToFeedAsync(transaction, next, cancellationToken);
        return [Changed(next, position)];
    }

    /// <summary>The charge point of the card's connector, or of the session's start.</summary>
    private static ChargePointId? ChargePointOf(DriverAppCard? card, SessionEvent @event) =>
        (card, @event) switch
        {
            ({ } existing, _) => ConnectorId.TryCreate(existing.ConnectorId) switch
            {
                Ok<ConnectorId> connector => connector.Value.ChargePointId,
                Err<ValidationError> or null => null,
            },
            (null, SessionStarted started) => started.ConnectorId.ChargePointId,
            (null, _) => null,
        };

    private async Task<string?> NameAsync(
        ChargePointId? chargePointId, CancellationToken cancellationToken) =>
        names is not null && chargePointId is { } id
            ? await names.DisplayNameAsync(id, cancellationToken)
            : null;

    private static DriverCardChanged Changed(DriverAppCard card, long position) =>
        new(
            new TokenUid(card.TokenUid),
            new SessionId(card.SessionId),
            card.Version,
            position,
            card.Status,
            card.ConnectorId,
            card.ChargePointDisplayName,
            card.EnergyKwh,
            card.StartedAt,
            card.EndedAt);
}
