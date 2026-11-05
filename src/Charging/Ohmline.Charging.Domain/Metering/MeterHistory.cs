using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Units;

namespace Ohmline.Charging.Domain.Metering;

/// <summary>Rules over a session's meter readings, oldest first.</summary>
public static class MeterHistory
{
    /// <summary>
    /// Checks every step of the history and reports the first that breaks the rules. Comparing
    /// only the first and last readings would miss a register that went down and back up.
    /// </summary>
    public static Result<Unit, MeterError> Check(ReadOnlySpan<MeterReading> history)
    {
        while (history is [var previous, var current, ..])
        {
            if (current.EnergySince(previous) is Err<MeterError> error)
            {
                return error;
            }

            history = history[1..];
        }

        return Result.Ok();
    }

    /// <summary>The energy delivered over the whole history, if every step is sound.</summary>
    public static Result<Energy, MeterError> EnergyDelivered(ReadOnlySpan<MeterReading> history) =>
        history switch
        {
            [] or [_] => Result.Ok(Energy.Zero),
            [var first, .., var last] => Check(history).Bind(_ => last.EnergySince(first)),
        };
}
