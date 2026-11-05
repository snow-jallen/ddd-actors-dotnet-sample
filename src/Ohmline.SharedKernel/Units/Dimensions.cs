namespace Ohmline.SharedKernel.Units;

/// <summary>
/// How the units combine. Each unit stands alone and knows nothing of the others; the physics
/// that relates them lives here, in one place: E = P × t, P = E / t and, for one phase, P = V × I.
/// Anything else (power plus energy, energy times current) does not compile.
/// </summary>
public static class Dimensions
{
    extension(Power)
    {
        /// <summary>
        /// Energy delivered at a constant power. Counting in ticks keeps the arithmetic exact.
        /// </summary>
        public static Energy operator *(Power power, TimeSpan duration) =>
            new(power.Kilowatts * duration.Ticks / TimeSpan.TicksPerHour);

        public static Energy operator *(TimeSpan duration, Power power) => power * duration;
    }

    extension(Energy)
    {
        /// <summary>The average power over a duration. Over no time at all, it throws.</summary>
        public static Power operator /(Energy energy, TimeSpan duration) =>
            new(energy.KilowattHours * TimeSpan.TicksPerHour / duration.Ticks);
    }

    extension(Voltage)
    {
        /// <summary>Single-phase power, with a power factor of one.</summary>
        public static Power operator *(Voltage voltage, Current current) =>
            new(voltage.Volts * current.Amperes / 1000m);
    }
}
