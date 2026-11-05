using Ohmline.Charging.Api.Queries;
using Ohmline.Charging.Contracts.Live;

namespace Ohmline.Charging.Api.Live;

/// <summary>
/// A session's summary as live clients receive it: the data of each Server-Sent Event, and from
/// section 14.5 the argument of SignalR's <c>sessionSummaryChanged</c>. Plain values: a browser
/// or a phone reads it, not an actor.
/// </summary>
public sealed record SessionSummaryDto(
    Guid SessionId, long Version, string Status, decimal EnergyKwh)
{
    public static SessionSummaryDto From(SessionSummaryChanged changed) =>
        new(changed.SessionId.Value, changed.Version, changed.Status, changed.EnergyKwh);

    public static SessionSummaryDto From(SessionResponse row) =>
        new(row.SessionId, row.Version, row.Status, row.EnergyKwh);
}
