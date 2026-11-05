using System.Collections.Immutable;
using Akka.Actor;
using Ohmline.ActorShell.Deciders;
using Ohmline.SharedKernel.Identity;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;

namespace Ohmline.Tariffs.Actors.Book;

/// <summary>
/// The tariff book in the shell: one persistent actor for the whole cluster, a cluster
/// singleton, because the Knowledge Level is one list that every session copies from. It is
/// written to a few times a month and read once per session start, by question, never by
/// sharing its state.
/// </summary>
public sealed class TariffBookActor
    : DeciderActor<TariffBook, TariffBookState, TariffBookCommand, TariffBookEvent,
        TariffBookError>
{
    /// <summary>Durable vocabulary: the singleton's name and its persistence id.</summary>
    public const string Name = "tariff-book";

    public TariffBookActor(TimeProvider clock)
        : base(Name, clock) =>
        Command<GetTariffSnapshot>(get =>
            Sender.Tell(new CurrentTariff(get.TariffId, State.Current(get.TariffId))));

    /// <summary>For a test that starts the actor itself, with a clock of its choosing.</summary>
    public static Props Props(TimeProvider clock) =>
        Akka.Actor.Props.Create(() => new TariffBookActor(clock));

    protected override object Accepted(long version) => new TariffBookAccepted(version);

    protected override object Rejected(TariffBookError error) => error.ToReply();

    protected override object Rejected(string code, string message) =>
        new TariffBookRejected(code, message);

    protected override CommandId CommandIdOf(TariffBookCommand command) => command.CommandId;

    protected override CommandId CausationOf(TariffBookEvent @event) => @event.CommandId;

    protected override object? ToSnapshot(
        TariffBookState state, ImmutableArray<RecentCommand> recent) =>
        state.ToSnapshot() is { } snapshot ? snapshot with { RecentCommands = recent } : null;

    protected override (TariffBookState State, ImmutableArray<RecentCommand> Recent)
        FromSnapshot(object snapshot)
    {
        var stored = (TariffBookSnapshot)snapshot;
        return (stored.ToState(), stored.RecentCommands);
    }
}
