using Ohmline.Charging.Domain.Metering;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Actors.Sessions;

/// <summary>
/// Where a session is in its life, in its snapshot, written as a permanent <c>"$kind"</c>. Each
/// case carries only what the state's case of the same name carries.
/// </summary>
public closed record SessionPhaseSnapshot
{
    public sealed record Active(MeterReading Start, MeterReading Latest) : SessionPhaseSnapshot;

    public sealed record SuspendedByEv(
        MeterReading Start, MeterReading Latest, DateTimeOffset Since)
        : SessionPhaseSnapshot;

    public sealed record SuspendedByCharger(
        MeterReading Start, MeterReading Latest, DateTimeOffset Since)
        : SessionPhaseSnapshot;

    public sealed record Ended(Energy EnergyDelivered, DateTimeOffset EndedAt)
        : SessionPhaseSnapshot;
}
