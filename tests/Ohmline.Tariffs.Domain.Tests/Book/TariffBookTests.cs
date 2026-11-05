using Ohmline.SharedKernel;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;
using Ohmline.Tariffs.Domain.Book;
using static Ohmline.Tariffs.Domain.Tests.TheDepotTariff;

namespace Ohmline.Tariffs.Domain.Tests.Book;

/// <summary>
/// The tariff book publishes numbered versions, checked by the tariff's own rules, and retires
/// tariffs; a version, once published, never changes.
/// </summary>
public sealed class TariffBookTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 4, 14, 40, 0, TimeSpan.Zero);

    private static readonly CommandId Command =
        new(Guid.Parse("019a4f6e-1c00-7d2a-9b3e-5c0a00000042"));

    [Fact]
    public void Given_an_empty_book_when_a_tariff_is_published_then_it_is_version_1() =>
        TariffBookSpec.Given().At(Morning)
            .When(Publish(Components))
            .Then(Published(Version1().Snapshot()));

    [Fact]
    public void Given_a_published_tariff_when_published_again_then_it_is_the_next_version()
    {
        var cheaper = new EnergyRate(Usd(0.39m));

        TariffBookSpec.Given(Published(Version1().Snapshot())).At(Morning)
            .When(Publish([cheaper, Fee, Idle]))
            .Then(Published(Version1().Snapshot() with
            {
                Version = 2,
                Components = [cheaper, Fee, Idle],
            }));
    }

    [Fact]
    public void Given_a_tariff_that_breaks_its_rules_when_published_then_it_is_refused() =>
        TariffBookSpec.Given().At(Morning)
            .When(Publish([BaseRate, Fee, Fee]))
            .ThenRejects<Malformed>() // The tariff's own rule: at most one session fee.
            .Expected.ShouldBe("at most one session fee");

    [Fact]
    public void Given_a_published_tariff_when_retired_then_its_last_version_is_recorded() =>
        TariffBookSpec.Given(Published(Version1().Snapshot())).At(Morning)
            .When(new RetireTariff(Command, Id))
            .Then(new TariffRetired(Id, 1) { CommandId = Command, OccurredAt = Morning });

    [Fact]
    public void Given_a_retired_tariff_when_retired_again_then_nothing_changes() =>
        TariffBookSpec.Given(Published(Version1().Snapshot()), Retired()).At(Morning)
            .When(new RetireTariff(Command, Id))
            .ThenNothingChanges();

    [Fact]
    public void Given_a_tariff_never_published_when_retired_then_it_is_refused() =>
        TariffBookSpec.Given().At(Morning)
            .When(new RetireTariff(Command, Id))
            .ThenRejects(new TariffBookError.NotPublished(Id));

    [Fact]
    public void Given_a_retired_tariff_when_published_again_then_it_continues_its_numbering() =>
        TariffBookSpec.Given(Published(Version1().Snapshot()), Retired()).At(Morning)
            .When(Publish(Components))
            .Then(Published(Version1().Snapshot() with { Version = 2 }));

    [Fact]
    public void Given_a_published_then_retired_tariff_then_sessions_can_no_longer_copy_it()
    {
        var published = TariffBook.Evolve(TariffBook.Initial, Published(Version1().Snapshot()));
        var retired = TariffBook.Evolve(published, Retired());

        published.Current(Id).ShouldBe(Version1().Snapshot());
        retired.Current(Id).ShouldBeNull();
        retired.Tariffs[Id].Latest.ShouldBe(Version1().Snapshot());
    }

    private static PublishTariff Publish(TariffComponent[] components) => new(
        Command, Id, Currency.Usd, [.. components], null, RoundingPolicy.Cents, "America/Denver");

    private static TariffBookEvent Published(TariffSnapshot tariff) =>
        new TariffPublished(tariff) { CommandId = Command, OccurredAt = Morning };

    private static TariffBookEvent Retired() =>
        new TariffRetired(Id, 1) { CommandId = Command, OccurredAt = Morning };
}
