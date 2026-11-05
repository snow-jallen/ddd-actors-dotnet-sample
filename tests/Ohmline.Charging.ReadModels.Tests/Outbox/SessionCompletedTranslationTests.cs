using System.Text.Json;
using Ohmline.Charging.Contracts.Integration;
using Ohmline.Charging.Domain.Sessions;
using Ohmline.Charging.ReadModels.Outbox;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.SharedKernel.Units;
using Ohmline.Tariffs.Contracts;
using static Ohmline.Charging.ReadModels.Tests.Known;

namespace Ohmline.Charging.ReadModels.Tests.Outbox;

/// <summary>
/// The relay's translation from Charging's domain events into its Published Language
/// (section 16.1): pure, so a table of sessions says what Billing is told.
/// </summary>
public sealed class SessionCompletedTranslationTests
{
    private static readonly DateTimeOffset Ended = Morning.AddMinutes(45);

    [Fact]
    public void Given_a_session_that_ended_then_it_is_completed_with_its_price()
    {
        var completed = SessionCompletedTranslation.Translate(
            RelayedSession.From(Started(Tariff)), EndedWith(30m));

        completed.ShouldBe(new SessionCompleted(
            SessionCompletedTranslation.EventIdOf(Session),
            Ended,
            Session.Value,
            "04A2B3C4D5E6F7",
            "OHM-SLC-0042/1",
            30m,
            new PriceBreakdown("USD", 12.60m, [new("energy", 30m, "kWh", 0.42m, 12.60m)]),
            Morning,
            Ended));
    }

    [Fact]
    public void Given_a_tariff_with_a_session_fee_then_the_fee_is_a_line_of_its_own()
    {
        var withFee = Tariff with
        {
            Components = [.. Tariff.Components, new SessionFee(new Money(1m, Currency.Usd))],
        };

        var price = SessionCompletedTranslation.Translate(
            RelayedSession.From(Started(withFee)), EndedWith(10m)).Price;

        price.Lines.ShouldBe(
        [
            new PriceLine("energy", 10m, "kWh", 0.42m, 4.20m),
            new PriceLine("session-fee", 1m, "session", 1m, 1m),
        ]);
        price.Total.ShouldBe(5.20m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7.35)]
    [InlineData(30)]
    public void The_price_is_what_the_journey_captures(decimal kWh)
    {
        var started = Started(Tariff);
        var terms = new SessionTerms(
            Session, Depot, started.ConnectorId, DriversCard, Tariff, started.SpendCap);

        var price = SessionCompletedTranslation.Translate(
            RelayedSession.From(started), EndedWith(kWh)).Price;

        new Money(price.Total, new Currency(price.Currency))
            .ShouldBe(terms.CostOf(new Energy(kWh)));
    }

    [Fact]
    public void The_same_session_is_always_the_same_event()
    {
        var once = SessionCompletedTranslation.Translate(
            RelayedSession.From(Started(Tariff)), EndedWith(30m));
        var again = SessionCompletedTranslation.Translate(
            RelayedSession.From(Started(Tariff)), EndedWith(30m));

        again.EventId.ShouldBe(once.EventId);
        once.EventId.ShouldNotBe(Session.Value);
    }

    [Fact]
    public void A_completed_session_is_written_under_its_wire_name_and_read_back()
    {
        ChargingIntegrationEvent completed = SessionCompletedTranslation.Translate(
            RelayedSession.From(Started(Tariff)), EndedWith(30m));

        var json = JsonSerializer.Serialize(completed, PublishedLanguage.Json);

        JsonDocument.Parse(json).RootElement.GetProperty("type").GetString()
            .ShouldBe("ohmline.charging.session-completed.v1");
        JsonSerializer.Deserialize<ChargingIntegrationEvent>(json, PublishedLanguage.Json)
            .ShouldBe(completed);
    }

    /// <summary>
    /// The second version (section 20.9): the same session, the same price, its site, and the
    /// connector as a charge point and a number; under an event id of its own.
    /// </summary>
    [Fact]
    public void Given_a_session_that_ended_then_its_second_version_names_the_site_and_charger()
    {
        var session = RelayedSession.From(Started(Tariff));

        var v2 = SessionCompletedTranslation.TranslateV2(session, EndedWith(30m));

        v2.ShouldBe(new SessionCompletedV2(
            SessionCompletedTranslation.EventIdOf(Session, SessionCompletedV2.WireName),
            Ended,
            Session.Value,
            "slc-depot",
            "04A2B3C4D5E6F7",
            "OHM-SLC-0042",
            1,
            30m,
            SessionCompletedTranslation.Translate(session, EndedWith(30m)).Price,
            Morning,
            Ended));
        v2.EventId.ShouldNotBe(SessionCompletedTranslation.EventIdOf(Session));
    }

    private static SessionStarted Started(TariffSnapshot tariff) => new(
        Session,
        Depot,
        new ConnectorId(Charger, 1),
        DriversCard,
        tariff,
        new Money(40m, Currency.Usd),
        MeterStartWh: 1_200_000) { OccurredAt = Morning };

    private static SessionEnded EndedWith(decimal kWh) => new(
        Session, Depot, new ConnectorId(Charger, 1), DriversCard, new Energy(kWh))
    {
        OccurredAt = Ended,
    };
}
