using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Metering;

/// <summary>
/// A charge point's cumulative energy register, read at one moment. The register only counts up,
/// so the energy delivered between two readings is their difference, if they really are in order.
/// </summary>
public sealed record MeterReading(Energy Register, DateTimeOffset At)
{
    /// <summary>Charge points report the register in watt-hours.</summary>
    public static MeterReading FromWattHours(long wattHours, DateTimeOffset at) =>
        new(Energy.FromWattHours(wattHours), at);

    /// <summary>
    /// The energy delivered since an earlier reading. A reading taken before it, or a register
    /// lower than it, is not an exception: meters do this, and the caller decides what it means.
    /// </summary>
    public Result<Energy, MeterError> EnergySince(MeterReading previous)
    {
        if (At < previous.At)
        {
            return Result.Fail<MeterError>(new ReadingOutOfOrder(previous, this));
        }

        if (Register < previous.Register)
        {
            return Result.Fail<MeterError>(new RegisterWentBackwards(previous, this));
        }

        return Result.Ok(Register - previous.Register);
    }
}
