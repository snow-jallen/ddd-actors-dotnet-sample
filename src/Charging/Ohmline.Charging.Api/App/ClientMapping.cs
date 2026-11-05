using Ohmline.Charging.Api.Queries;
using Ohmline.Charging.Client.Contracts.Notifications;
using Ohmline.Charging.Client.Contracts.ReadModels;
using Ohmline.Charging.Contracts.Listings;
using Internal = Ohmline.Charging.Contracts.Live;

namespace Ohmline.Charging.Api.App;

/// <summary>
/// From Charging's own messages to the published language for client apps (section 19.2):
/// plain values out, nothing of the shared kernel, so the DTOs need no reference to it.
/// </summary>
internal static class ClientMapping
{
    extension(Internal.SessionSummaryChanged changed)
    {
        public SessionSummaryChanged ToClient() =>
            new(changed.SessionId.Value, changed.Version, changed.Status, changed.EnergyKwh);
    }

    extension(SessionResponse row)
    {
        public SessionSummaryChanged ToSummary() =>
            new(row.SessionId, row.Version, row.Status, row.EnergyKwh);
    }

    extension(Internal.DriverCardChanged changed)
    {
        public DriverCardChanged ToClient() => new(
            changed.Position,
            new DriverSessionCard(
                changed.SessionId.Value,
                changed.TokenUid.Value,
                changed.Status,
                changed.ConnectorId,
                changed.ChargePointDisplayName,
                changed.EnergyKwh,
                changed.StartedAt,
                changed.EndedAt,
                changed.Version),
            changed.TraceParent);
    }

    extension(ChargePointListingView view)
    {
        public ChargePointListingDto ToClient() => new(
            view.ChargePointId.Value,
            view.Revision,
            view.DisplayName ?? "",
            view.AccessInstructions ?? "",
            view.OpeningHours ?? "");
    }
}
