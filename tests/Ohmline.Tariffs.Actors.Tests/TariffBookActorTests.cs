using Microsoft.Extensions.Time.Testing;
using Ohmline.Tariffs.Actors.Book;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;
using Ohmline.Testing;
using static Ohmline.Tariffs.Actors.Tests.TheBook;

namespace Ohmline.Tariffs.Actors.Tests;

/// <summary>
/// The tariff book's shell: one persistence id for the whole cluster, versions in the journal,
/// and the current version for any session that asks.
/// </summary>
public sealed class TariffBookActorTests(ITestOutputHelper output)
    : PersistenceShellSpec(
        ActorTestConfig.With(Serializers.Tariffs), nameof(TariffBookActorTests), output)
{
    private readonly FakeTimeProvider _clock = new(Morning);

    private PersistenceShellSpec.Scenario Book =>
        Shell(TariffBookActor.Props(_clock), TariffBookActor.Name);

    [Fact]
    public void The_persistence_id_is_the_singletons_name() =>
        TariffBookActor.Name.ShouldBe("tariff-book");

    [Fact]
    public async Task Given_an_empty_book_when_a_tariff_is_published_then_it_is_version_1()
    {
        var publish = Publish(Version1);

        await Book.Given()
            .When(publish)
            .ThenAsync(
                new TariffBookAccepted(1),
                Stamped(new TariffPublished(Version1), publish.CommandId));
    }

    [Fact]
    public async Task Given_a_published_tariff_when_a_session_asks_then_it_gets_that_version() =>
        await Book.Given(Stamped(new TariffPublished(Version1)))
            .When(new GetTariffSnapshot(Standard))
            .ThenAsync(new CurrentTariff(Standard, Version1));

    [Fact]
    public async Task Given_a_retired_tariff_when_a_session_asks_then_there_is_none() =>
        await Book.Given(
                Stamped(new TariffPublished(Version1)), Stamped(new TariffRetired(Standard, 1)))
            .When(new GetTariffSnapshot(Standard))
            .ThenAsync(new CurrentTariff(Standard, Snapshot: null));

    [Fact]
    public async Task Given_a_tariff_never_published_when_retired_then_it_is_refused() =>
        await Book.Given()
            .When(new RetireTariff(NewCommand, Standard))
            .ThenAsync<TariffBookRejected>(rejected => rejected.Code.ShouldBe("not-published"));
}
