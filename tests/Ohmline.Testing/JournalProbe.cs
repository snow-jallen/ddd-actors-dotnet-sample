using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence;

namespace Ohmline.Testing;

/// <summary>
/// Reads one persistence id's events back the way recovery reads them: a fresh persistent actor
/// with that id replays the journal and hands over every event it was given. It skips snapshots,
/// so it always replays from the first event still in the journal, and it never writes. It works
/// with any journal: the test kit's, and SQLite's.
/// </summary>
public static class JournalProbe
{
    public static async Task<ImmutableArray<TEvent>> EventsAsync<TEvent>(
        ActorSystem system, string persistenceId, CancellationToken cancellationToken)
    {
        var probe = system.ActorOf(Props.Create(() => new Replayer(persistenceId)));
        var timeout = TimeSpan.FromSeconds(3 * ActorTestConfig.TimeFactor);
        var replayed = await probe.Ask<Replayed>(Read.Instance, timeout, cancellationToken);
        return [.. replayed.Events.Cast<TEvent>()];
    }

    private sealed class Replayer : ReceivePersistentActor
    {
        private readonly List<object> _events = [];

        public Replayer(string persistenceId)
        {
            PersistenceId = persistenceId;
            Recover<RecoveryCompleted>(_ => { });
            RecoverAny(_events.Add);
            Command<Read>(_ =>
            {
                Sender.Tell(new Replayed([.. _events]));
                Context.Stop(Self);
            });
        }

        public override string PersistenceId { get; }

        public override Recovery Recovery => new(SnapshotSelectionCriteria.None);
    }

    /// <summary>Asked of the probe; waits in its stash until the replay is over.</summary>
    private sealed record Read : INoSerializationVerificationNeeded
    {
        public static Read Instance { get; } = new();
    }

    /// <summary>Never leaves the process, so it is not serialized.</summary>
    private sealed record Replayed(ImmutableArray<object> Events)
        : INoSerializationVerificationNeeded;
}
