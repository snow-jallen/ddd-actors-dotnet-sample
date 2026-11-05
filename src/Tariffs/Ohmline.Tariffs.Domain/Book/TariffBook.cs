using System.Collections.Immutable;
using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Deciders;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Tariffs.Domain.Book;

/// <summary>
/// The tariff book as a decider: the one list of published tariffs, the Knowledge Level of
/// Ohmline's pricing (section 8.2). Publishing makes the next version of a tariff, checked by
/// <see cref="Tariff"/>'s own rules; retiring stops new sessions from using it. Sessions copy a
/// version when they start, so nothing decided here ever changes a running session.
/// </summary>
public sealed class TariffBook
    : IDecider<TariffBookState, TariffBookCommand, TariffBookEvent, TariffBookError>
{
    private TariffBook()
    {
    }

    public static TariffBookState Initial => TariffBookState.Empty;

    public static Result<ImmutableArray<TariffBookEvent>, TariffBookError> Decide(
        TariffBookState state, TariffBookCommand command, DecisionContext context) =>
        DecisionTable(state, command).Map(events => Stamp(events, command, context));

    /// <summary>Total: a retirement of a tariff the book does not hold changes nothing.</summary>
    public static TariffBookState Evolve(TariffBookState state, TariffBookEvent @event) =>
        @event switch
        {
            TariffPublished published => state with
            {
                Tariffs = state.Tariffs.SetItem(
                    published.Tariff.TariffId, new Published(published.Tariff)),
            },
            TariffRetired retired when state.Tariffs.TryGetValue(retired.TariffId, out var entry)
                => state with
                {
                    Tariffs = state.Tariffs.SetItem(retired.TariffId, new Retired(entry.Latest)),
                },
            TariffRetired => state,
        };

    private static Result<ImmutableArray<TariffBookEvent>, TariffBookError> DecisionTable(
        TariffBookState state, TariffBookCommand command) =>
        command switch
        {
            PublishTariff publish => Tariff.TryCreate(
                    publish.TariffId,
                    NextVersion(state, publish.TariffId),
                    publish.Currency,
                    publish.Components,
                    publish.PriceCap,
                    publish.Rounding,
                    publish.TimeZoneId)
                .Match(
                    tariff => Accept(new TariffPublished(tariff.Snapshot())),
                    invalid => Reject(invalid)),
            RetireTariff retire => state.Tariffs.GetValueOrDefault(retire.TariffId).Value switch
            {
                Published published =>
                    Accept(new TariffRetired(retire.TariffId, published.Current.Version)),
                Retired => NoChange,
                _ => Reject(new TariffBookError.NotPublished(retire.TariffId)),
            },
        };

    /// <summary>1 for a new tariff, or one more than its latest version, even if retired.</summary>
    private static int NextVersion(TariffBookState state, TariffId tariffId) =>
        state.Tariffs.TryGetValue(tariffId, out var entry) ? entry.Latest.Version + 1 : 1;

    private static Result<ImmutableArray<TariffBookEvent>, TariffBookError> Accept(
        TariffBookEvent @event) => Result.Ok<ImmutableArray<TariffBookEvent>>([@event]);

    private static Result<ImmutableArray<TariffBookEvent>, TariffBookError> NoChange { get; } =
        Result.Ok(ImmutableArray<TariffBookEvent>.Empty);

    private static Result<ImmutableArray<TariffBookEvent>, TariffBookError> Reject(
        TariffBookError error) => Result.Fail(error);

    private static ImmutableArray<TariffBookEvent> Stamp(
        ImmutableArray<TariffBookEvent> events,
        TariffBookCommand command,
        DecisionContext context) =>
        [.. events.Select(@event =>
            @event with { CommandId = command.CommandId, OccurredAt = context.Now })];
}
