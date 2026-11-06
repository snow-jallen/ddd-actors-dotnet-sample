using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Api.Tariffs;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;
using Ohmline.Tariffs.Contracts.Book;

namespace Ohmline.Charging.Api.Tests;

/// <summary>A probe plays the tariff book's singleton proxy.</summary>
public sealed class TariffEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly TariffId Standard = new("slc-standard");

    private static readonly TariffSnapshot Version2 = new(
        Standard,
        2,
        Currency.Usd,
        [
            new EnergyRate(new Money(0.39m, Currency.Usd)),
            new EnergyRate(
                new Money(0.29m, Currency.Usd),
                new TimeOfUseBand("night", new TimeOnly(22, 0), new TimeOnly(6, 0))),
            new IdleFee(
                new Money(0.40m, Currency.Usd),
                TimeSpan.FromMinutes(15),
                Cap: new Money(12.00m, Currency.Usd)),
        ],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    private static readonly PublishTariffRequest Request = new(
        "slc-standard",
        "USD",
        "America/Denver",
        [
            new ComponentDto("energy-rate", 0.39m),
            new ComponentDto(
                "energy-rate", 0.29m, Band: new BandDto("night", new(22, 0), new(6, 0))),
            new ComponentDto("idle-fee", 0.40m, GraceMinutes: 15, Cap: 12.00m),
        ]);

    [Fact]
    public async Task Given_a_tariff_then_the_book_is_told_to_publish_it()
    {
        var book = CreateTestProbe();

        var answer = TariffEndpoints.PublishAsync(
            Request, null, RegionOf<TariffBookProxy>(book), Clock, Options, Ct);
        var publish = await AnswerAsync<PublishTariff>(book, new TariffBookAccepted(2));

        publish.Components.ShouldBe(Version2.Components);
        publish.Currency.ShouldBe(Currency.Usd);
        publish.Rounding.ShouldBe(RoundingPolicy.Cents);
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Location.ShouldBe("/tariffs/slc-standard");
    }

    [Fact]
    public async Task Given_a_component_of_no_known_kind_then_the_tariff_is_not_valid()
    {
        var answer = await TariffEndpoints.PublishAsync(
            Request with { Components = [new ComponentDto("parking-fee", 2m)] },
            null,
            RegionOf<TariffBookProxy>(CreateTestProbe()),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["components[0].kind"]);
    }

    [Fact]
    public async Task Given_a_published_tariff_when_read_then_200_with_its_current_version()
    {
        var book = CreateTestProbe();

        var answer = TariffEndpoints.GetAsync(
            "slc-standard", RegionOf<TariffBookProxy>(book), Options, Ct);
        await AnswerAsync<GetTariffSnapshot>(book, new CurrentTariff(Standard, Version2));

        var tariff = (await answer).Result.ShouldBeOfType<Ok<TariffResponse>>().Value;
        tariff.ShouldNotBeNull().Version.ShouldBe(2);
        tariff.Components.ShouldBe(Request.Components);
    }

    [Fact]
    public async Task Given_a_tariff_that_is_not_published_when_read_then_404()
    {
        var book = CreateTestProbe();

        var answer = TariffEndpoints.GetAsync(
            "slc-standard", RegionOf<TariffBookProxy>(book), Options, Ct);
        await AnswerAsync<GetTariffSnapshot>(book, new CurrentTariff(Standard, Snapshot: null));

        Problem((await answer).Result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Given_the_book_then_a_starting_session_copies_its_current_version()
    {
        var book = CreateTestProbe();
        var catalog = new TariffBookCatalog(RegionOf<TariffBookProxy>(book), Options);

        var current = catalog.CurrentAsync(Standard, Ct);
        await AnswerAsync<GetTariffSnapshot>(book, new CurrentTariff(Standard, Version2));

        (await current).ShouldBe(Version2);
    }
}
