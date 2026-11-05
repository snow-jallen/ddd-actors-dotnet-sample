using Ohmline.Charging.Domain.Metering;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// The two readings a session needs: the one at the start and the latest accepted one. Every
/// reading in between is in the journal, not in memory.
/// </summary>
public sealed record SessionMeter(MeterReading Start, MeterReading Latest)
{
    public static SessionMeter StartingAt(MeterReading start) => new(start, start);

    public Energy Delivered => Latest.Register - Start.Register;

    public SessionMeter Advance(MeterReading reading) => this with { Latest = reading };
}
