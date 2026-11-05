using System.Collections.Immutable;
using Akka.Persistence.Query;
using Npgsql;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Live;
using Ohmline.Charging.Domain.ChargePoints;
using Ohmline.Charging.ReadModels.Projections;
using Ohmline.SiteEnergy.Contracts;
using Ohmline.SiteEnergy.Contracts.Budgets;

namespace Ohmline.Charging.ReadModels.SiteLiveBoard;

/// <summary>
/// The operator's live board (section 13.8): every connector of a site with its status and the
/// power it may draw now. It reads two contexts' tags, <c>charging</c> for connector statuses
/// and <c>site-energy</c> for allocations, as two streams with offsets of their own.
/// </summary>
public sealed class SiteLiveBoardProjection : IProjectionHandler
{
    /// <summary>Durable: its offsets are kept under it. A new table shape gets a new id.</summary>
    public const string Id = "site-live-board-v1";

    public string ProjectionId => Id;

    public IReadOnlyList<string> Tags { get; } = [ChargingTags.Charging, SiteEnergyTags.SiteEnergy];

    /// <summary>
    /// A board that freezes is worse than one that misses an event: the next event for the same
    /// connector puts it right again, and the skipped one is on record.
    /// </summary>
    public ProjectionFailurePolicy FailurePolicy { get; } = new RecordAndSkip(MaxRetries: 5);

    /// <summary>The board announces nothing yet: the operator console polls it.</summary>
    public async Task<ImmutableArray<LiveUpdate>> HandleAsync(
        EventEnvelope envelope, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        switch (envelope.Event)
        {
            case ChargePointEvent chargePoint:
                foreach (var change in ConnectorStatusChange.Of(chargePoint))
                {
                    await SiteLiveBoardTable.ApplyAsync(
                        transaction, change, envelope.SequenceNr, cancellationToken);
                }

                break;
            case ISiteAllocationEvent allocations:
                await SiteLiveBoardTable.ApplyAsync(
                    transaction, allocations, envelope.SequenceNr, cancellationToken);
                break;
        }

        return [];
    }
}
