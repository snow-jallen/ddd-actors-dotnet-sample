using System.Collections.Immutable;

namespace Ch03.LanguageLab.Tests;

public static class ChargingExtensions
{
    extension(decimal value)
    {
        /// <summary>Reads like a unit: <c>22m.kW</c>.</summary>
        public Kilowatts kW => new(value);
    }

    extension(Kilowatts)
    {
        public static Kilowatts FromWatts(decimal watts) => new(watts / 1000m);

        public static KilowattHours operator *(Kilowatts power, TimeSpan duration) =>
            new(power.Value * duration.Ticks / TimeSpan.TicksPerHour);
    }

    extension(ImmutableArray<MeterSample> history)
    {
        /// <summary>The last sample taken at or before a time (the history is in order).</summary>
        public MeterSample? this[DateTimeOffset at] =>
            history.LastOrDefault(sample => sample.TakenAt <= at);
    }

    // Preview of section 10.2: once Akka.NET is referenced, one block gives every IActorRef a
    // Send that accepts only the protocol union, and tells the case inside it, never the union.
    //
    // extension(IActorRef self)
    // {
    //     public void Send(ChargerProtocol msg) => self.Tell(msg.Value);
    // }
}
