using System.Collections.Immutable;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Persistence;
using Akka.Persistence.TestKit;
using Akka.TestKit;
using Xunit;

namespace Ohmline.Testing;

/// <summary>
/// Given-When-Then for a decider in its shell: the twin, one layer out, of <see
/// cref="DeciderSpec{TDecider, TState, TCommand, TEvent, TError}"/>. Given the events already
/// in the journal, when the actor gets a message, then it replies this and persists exactly
/// these events. The rules are the decider's tests; this checks what only the shell does:
/// recovery, replies, what reaches the journal, and what happens when the journal misbehaves.
/// </summary>
/// <remarks>
/// <para>
/// Built on Akka.Persistence.TestKit's scriptable journal and snapshot store, with <see
/// cref="ActorTestConfig"/>, so every message is serialized on its way. The history is written
/// by a seeding actor that persists the given events under the persistence id and stops, so the
/// actor under test recovers them as it would in production. What was persisted is read back by
/// recovering a fresh probe incarnation (<see cref="JournalProbe"/>): the test kit 1.5.73 has no
/// query of its own for the journal's contents.
/// </para>
/// <para>
/// The journal and snapshot behaviours chosen with <see cref="Act.WithJournalWrite"/> and
/// <see cref="Act.WithSnapshotSave"/> apply only while the message is handled, never to the
/// seeding or the reading back.
/// </para>
/// </remarks>
public abstract class PersistenceShellSpec(
    ActorSystemSetup setup, string actorSystemName, ITestOutputHelper output)
    : PersistenceTestKit(setup, actorSystemName, output)
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The shell under test: how to start it, and the persistence id it writes.</summary>
    protected Scenario Shell(Props actor, string persistenceId) =>
        new(this, actor, persistenceId, []);

    /// <summary>An actor and the history in its journal.</summary>
    public sealed class Scenario
    {
        private readonly PersistenceShellSpec _kit;
        private readonly Props _actor;
        private readonly string _persistenceId;
        private readonly ImmutableArray<object> _history;

        internal Scenario(
            PersistenceShellSpec kit,
            Props actor,
            string persistenceId,
            ImmutableArray<object> history)
        {
            _kit = kit;
            _actor = actor;
            _persistenceId = persistenceId;
            _history = history;
        }

        /// <summary>The events already in the journal, oldest first; none for a new one.</summary>
        public Scenario Given(params IEnumerable<object> history) =>
            new(_kit, _actor, _persistenceId, [.. history]);

        /// <summary>The message the actor gets once it has recovered the history.</summary>
        public Act When(object message) => new(this, message, null, null);

        /// <summary>
        /// Writes the history and starts the actor, for a test that drives it step by step.
        /// </summary>
        public async Task<IActorRef> StartAsync()
        {
            await SeedAsync();
            return _kit.Sys.ActorOf(_actor);
        }

        /// <summary>
        /// Writes the history and starts the actor as a child of <paramref name="shard"/>, which
        /// plays its shard: for a test that expects the actor to passivate itself.
        /// </summary>
        public async Task<IActorRef> StartUnderAsync(TestProbe shard)
        {
            await SeedAsync();
            return shard.ChildActorOf(_actor, cancellationToken: Ct);
        }

        internal async Task<IActorRef> StartWatchedAsync()
        {
            var actor = await StartAsync();
            await _kit.WatchAsync(actor);
            return actor;
        }

        internal async Task<ImmutableArray<object>> PersistedSinceGivenAsync()
        {
            var all = await JournalProbe.EventsAsync<object>(_kit.Sys, _persistenceId, Ct);
            return [.. all.Skip(_history.Length)];
        }

        internal PersistenceShellSpec Kit => _kit;

        private async Task SeedAsync()
        {
            // Seeds even an empty history: the seeder's recovery starts the journal and the
            // snapshot store, so the actor under test does not pay for that within its reply.
            var seeder = _kit.Sys.ActorOf(Props.Create(() => new Seeder(_persistenceId)));
            await seeder.Ask<Seeded>(new Seed(_history), Timeout, Ct);
            await _kit.WatchAsync(seeder);
            _kit.Sys.Stop(seeder);
            await _kit.ExpectTerminatedAsync(seeder, Timeout, cancellationToken: Ct);
        }
    }

    /// <summary>The message, and how the journal and snapshot store behave meanwhile.</summary>
    public sealed class Act
    {
        private readonly Scenario _scenario;
        private readonly object _message;
        private readonly Func<JournalWriteBehavior, Task>? _journalWrite;
        private readonly Func<SnapshotStoreSaveBehavior, Task>? _snapshotSave;

        internal Act(
            Scenario scenario,
            object message,
            Func<JournalWriteBehavior, Task>? journalWrite,
            Func<SnapshotStoreSaveBehavior, Task>? snapshotSave)
        {
            _scenario = scenario;
            _message = message;
            _journalWrite = journalWrite;
            _snapshotSave = snapshotSave;
        }

        /// <summary>The journal writes this way while the message is handled.</summary>
        public Act WithJournalWrite(Func<JournalWriteBehavior, Task> behavior) =>
            new(_scenario, _message, behavior, _snapshotSave);

        /// <summary>The snapshot store saves this way while the message is handled.</summary>
        public Act WithSnapshotSave(Func<SnapshotStoreSaveBehavior, Task> behavior) =>
            new(_scenario, _message, _journalWrite, behavior);

        /// <summary>
        /// It replies exactly this, and persists exactly these events. Returns the actor, which
        /// is still running, for anything more the test wants to ask it.
        /// </summary>
        public Task<IActorRef> ThenAsync(object reply, params IEnumerable<object> persisted) =>
            ThenAsync<object>(actual => actual.ShouldBe(reply), persisted);

        /// <summary>
        /// It replies with a <typeparamref name="TReply"/> that passes the check, and persists
        /// exactly these events.
        /// </summary>
        public async Task<IActorRef> ThenAsync<TReply>(
            Action<TReply> check, params IEnumerable<object> persisted)
        {
            var kit = _scenario.Kit;
            var actor = await _scenario.StartAsync();
            await DuringAsync(async () =>
            {
                actor.Tell(_message, kit.TestActor);
                check(await kit.ExpectMsgAsync<TReply>(cancellationToken: Ct));
            });

            (await _scenario.PersistedSinceGivenAsync()).ShouldBe(persisted);
            return actor;
        }

        /// <summary>
        /// The actor stops without replying, and nothing new is in the journal: what a failed
        /// write or recovery does.
        /// </summary>
        public async Task ThenStopsAsync()
        {
            var kit = _scenario.Kit;
            var actor = await _scenario.StartWatchedAsync();
            await DuringAsync(async () =>
            {
                actor.Tell(_message, kit.TestActor);
                await kit.ExpectTerminatedAsync(actor, cancellationToken: Ct);
            });

            await kit.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
            (await _scenario.PersistedSinceGivenAsync()).ShouldBeEmpty();
        }

        private Task DuringAsync(Func<Task> act)
        {
            var kit = _scenario.Kit;
            Func<Task> withSnapshots = _snapshotSave is null
                ? act
                : () => kit.WithSnapshotSave(_snapshotSave, act);
            return _journalWrite is null
                ? withSnapshots()
                : kit.WithJournalWrite(_journalWrite, withSnapshots);
        }
    }

    /// <summary>
    /// For the seeding only: the first write of a fresh actor system starts its journal, which
    /// takes seconds on a machine running the whole suite.
    /// </summary>
    private static TimeSpan Timeout => TimeSpan.FromSeconds(20 * ActorTestConfig.TimeFactor);

    /// <summary>Persists the history under the persistence id, all at once, and says so.</summary>
    private sealed class Seeder : ReceivePersistentActor
    {
        public Seeder(string persistenceId)
        {
            PersistenceId = persistenceId;
            Command<Seed>(seed =>
            {
                var replyTo = Sender;
                if (seed.History.IsEmpty)
                {
                    replyTo.Tell(Seeded.Instance); // Recovered: the journal is up.
                    return;
                }

                PersistAll(seed.History, _ => { }, () => replyTo.Tell(Seeded.Instance));
            });
        }

        public override string PersistenceId { get; }
    }

    /// <summary>
    /// Test plumbing that never leaves the process; the events in it are serialized on their way
    /// to the journal like any others.
    /// </summary>
    private sealed record Seed(ImmutableArray<object> History)
        : INoSerializationVerificationNeeded;

    private sealed record Seeded : INoSerializationVerificationNeeded
    {
        public static Seeded Instance { get; } = new();
    }
}
