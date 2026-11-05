using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Api.Tariffs;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.Sessions;
using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;
using Ohmline.Tariffs.Contracts;

namespace Ohmline.Charging.Api.Tests;

public sealed class SessionEndpointsTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private static readonly Guid Session = Guid.Parse("019a4f6e-1c40-7a51-8e2f-3b1d00000001");

    private static readonly TariffSnapshot Standard = new(
        new TariffId("slc-standard"),
        Version: 3,
        Currency.Usd,
        [new EnergyRate(new Money(0.42m, Currency.Usd))],
        PriceCap: null,
        RoundingPolicy.Cents,
        "America/Denver");

    private static readonly StartSessionRequest Start = new(
        Session, "slc-depot", "OHM-SLC-0042/1", "04A2B3C4D5E6F7", "slc-standard", 40m, 1_200_000);

    [Fact]
    public async Task Given_a_start_then_the_session_gets_the_tariffs_current_version()
    {
        var region = CreateTestProbe();

        var answer = SessionEndpoints.StartAsync(
            Start,
            idempotencyKey: null,
            RegionOf<ChargingSessionRegion>(region),
            new Tariffs(),
            Clock,
            Options,
            Ct);
        var start = await AnswerAsync<StartSession>(region, new CommandAccepted(1));

        start.SessionId.ShouldBe(new SessionId(Session));
        start.ConnectorId.ShouldBe(new ConnectorId(new ChargePointId("OHM-SLC-0042"), 1));
        start.Tariff.ShouldBe(Standard);
        start.SpendCap.ShouldBe(new Money(40m, Currency.Usd)); // In the tariff's currency.
        start.MeterStartWh.ShouldBe(1_200_000);
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Location.ShouldBe($"/sessions/{Session}");
    }

    [Fact]
    public async Task Given_a_tariff_that_is_not_published_then_the_start_is_not_valid()
    {
        var region = CreateTestProbe();

        var answer = await SessionEndpoints.StartAsync(
            Start with { TariffId = "slc-gold" },
            null,
            RegionOf<ChargingSessionRegion>(region),
            new Tariffs(),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>()
            .Errors["tariffId"].ShouldBe(["No tariff slc-gold is published."]);
        await region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }

    [Fact]
    public async Task Given_a_session_id_that_is_not_version_7_then_the_start_is_not_valid()
    {
        var answer = await SessionEndpoints.StartAsync(
            Start with { SessionId = Guid.NewGuid() },
            null,
            RegionOf<ChargingSessionRegion>(CreateTestProbe()),
            new Tariffs(),
            Clock,
            Options,
            Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Given_a_meter_value_then_the_session_records_the_register()
    {
        var region = CreateTestProbe();

        var answer = SessionEndpoints.RecordMeterValueAsync(
            Session,
            new MeterValueRequest(1_207_400, Morning.AddMinutes(10)),
            null,
            RegionOf<ChargingSessionRegion>(region),
            Clock,
            Options,
            Ct);
        var metered = await AnswerAsync<RecordMeterValue>(region, new CommandAccepted(2));

        metered.ShouldBe(new RecordMeterValue(
            metered.CommandId, new SessionId(Session), 1_207_400, Morning.AddMinutes(10)));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>();
    }

    [Fact]
    public async Task Given_a_stop_then_the_session_is_told_to_stop()
    {
        var region = CreateTestProbe();

        var answer = SessionEndpoints.StopAsync(
            Session, null, RegionOf<ChargingSessionRegion>(region), Clock, Options, Ct);
        var stop = await AnswerAsync<StopSession>(region, new CommandAccepted(3));

        stop.SessionId.ShouldBe(new SessionId(Session));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Value.ShouldBe(new CommandAcceptedResponse(3));
    }

    /// <summary>The tariff book as the edge sees it: one tariff, at version 3.</summary>
    private sealed class Tariffs : ITariffCatalog
    {
        public Task<TariffSnapshot?> CurrentAsync(
            TariffId tariffId, CancellationToken cancellationToken) =>
            Task.FromResult(tariffId == Standard.TariffId ? Standard : null);
    }
}
