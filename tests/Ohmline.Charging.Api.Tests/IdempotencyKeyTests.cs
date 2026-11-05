using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Requests;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;
using Ohmline.Charging.Contracts.ChargePoints;

namespace Ohmline.Charging.Api.Tests;

/// <summary>
/// The client's Idempotency-Key is the command's id: a request sent again after a timeout is
/// the same command, and the aggregate answers it as it did the first time (section 12.6).
/// </summary>
public sealed class IdempotencyKeyTests(ITestOutputHelper output) : EdgeTestKit(output)
{
    private const string Key = "019a4f6e-1c00-7d2a-9b3e-5c0a00000042";

    private static readonly RegisterChargePointRequest Registration =
        new("OHM-SLC-0042", "slc-depot", [22m, 22m]);

    [Fact]
    public async Task Given_an_idempotency_key_then_it_is_the_command_id()
    {
        var region = CreateTestProbe();

        var answer = ChargePointEndpoints.RegisterAsync(
            Registration, Key, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);
        var command = await AnswerAsync<RegisterChargePoint>(region, new CommandAccepted(1));

        command.CommandId.Value.ShouldBe(Guid.Parse(Key));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>();
    }

    [Fact]
    public async Task Given_the_same_request_twice_without_a_key_then_they_are_two_commands()
    {
        var region = CreateTestProbe();

        var first = ChargePointEndpoints.RegisterAsync(
            Registration, null, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);
        var once = await AnswerAsync<RegisterChargePoint>(region, new CommandAccepted(1));
        await first;
        var second = ChargePointEndpoints.RegisterAsync(
            Registration, null, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);
        var again = await AnswerAsync<RegisterChargePoint>(region, new CommandAccepted(1));
        await second;

        again.CommandId.ShouldNotBe(once.CommandId);
    }

    [Theory]
    [InlineData("retry-1")]
    [InlineData("4f1c2a3e-5b6d-4e7f-8a9b-0c1d2e3f4a5b")] // A version 4 Guid: not ordered by time.
    public async Task Given_a_key_that_is_not_a_version_7_guid_then_422_and_nothing_is_sent(
        string key)
    {
        var region = CreateTestProbe();

        var answer = await ChargePointEndpoints.RegisterAsync(
            Registration, key, RegionOf<ChargePointRegion>(region), Clock, Options, Ct);

        Problem(answer.Result, StatusCodes.Status422UnprocessableEntity)
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["Idempotency-Key"]);
        await region.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), Ct);
    }
}
