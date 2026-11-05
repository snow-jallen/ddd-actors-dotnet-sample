using Akka.Hosting;

namespace Ohmline.ActorShell.Deciders;

/// <summary>
/// Snapshots are a cache (section 20.6). A snapshot the store cannot read, such as one in a
/// version this code does not know, is skipped, and the aggregate replays its events from the
/// first instead. Akka.NET's <c>snapshot-is-optional</c> does that; it is off by default because
/// it is unsafe when events are deleted, which <see cref="DeciderActor{TDecider, TState,
/// TCommand, TEvent, TError}"/> checks for at the end of every recovery.
/// </summary>
public static class OptionalSnapshots
{
    /// <summary>On for every snapshot store, through the plugins' common fallback.</summary>
    public const string Hocon =
        "akka.persistence.snapshot-store-plugin-fallback.snapshot-is-optional = on";

    extension(AkkaConfigurationBuilder akka)
    {
        public AkkaConfigurationBuilder WithOptionalSnapshots() =>
            akka.AddHocon(Hocon, HoconAddMode.Prepend);
    }
}
