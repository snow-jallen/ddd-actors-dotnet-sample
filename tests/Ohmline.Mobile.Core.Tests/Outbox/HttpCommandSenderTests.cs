using System.Net;
using Ohmline.Mobile.Core.Outbox;
using static Ohmline.Mobile.Core.Tests.Outbox.Commands;

namespace Ohmline.Mobile.Core.Tests.Outbox;

public sealed class HttpCommandSenderTests
{
    private static readonly OutboxRow Row = RowOf(StartCharging());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<HttpStatusCode> Transient => new(
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests);

    [Fact]
    public async Task Given_202_then_accepted_at_the_servers_version()
    {
        var server = new FakeServer().Answers(
            HttpStatusCode.Accepted, $$"""{"commandId":"{{Row.CommandId}}","version":1}""");

        var outcome = await new HttpCommandSender(server.Client()).SendAsync(Row, Ct);

        outcome.Value.ShouldBe(new SendAccepted(1));
        server.Requests.ShouldBe([(HttpMethod.Post, "/app/v1/journeys", Row.Payload)]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "reserved-for-another-token")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "unknown-connector")]
    [InlineData(HttpStatusCode.BadRequest, "idempotency-key-mismatch")]
    public async Task Given_a_problem_with_a_code_then_rejected_with_it(
        HttpStatusCode status, string code)
    {
        var server = new FakeServer().Answers(
            status, $$"""{"status":{{(int)status}},"code":"{{code}}","detail":"No."}""");

        var outcome = await new HttpCommandSender(server.Client()).SendAsync(Row, Ct);

        outcome.Value.ShouldBe(new SendRejected(code, "No."));
    }

    [Theory]
    [MemberData(nameof(Transient))]
    public async Task Given_an_answer_that_may_change_then_transient(HttpStatusCode status)
    {
        var server = new FakeServer().Answers(status);

        var outcome = await new HttpCommandSender(server.Client()).SendAsync(Row, Ct);

        outcome.Value.ShouldBe(new SendTransientFailure($"HTTP {(int)status}"));
    }

    [Fact]
    public async Task Given_no_network_then_transient()
    {
        var server = new FakeServer().IsUnreachable();

        var outcome = await new HttpCommandSender(server.Client()).SendAsync(Row, Ct);

        outcome.Value.ShouldBe(new SendTransientFailure("Network is unreachable"));
    }

    [Fact]
    public void A_reservation_goes_to_its_connectors_route()
    {
        var row = Row with
        {
            Kind = OutboxKinds.ReserveConnector, AggregateKey = "OHM-SLC-0042/2",
        };

        HttpCommandSender.RouteOf(row)
            .ShouldBe("/app/v1/charge-points/OHM-SLC-0042/connectors/2/reservations");
    }

    private static OutboxRow RowOf(OutboxCommand command) => new(
        command.CommandId,
        command.Kind,
        command.AggregateKey,
        command.Payload,
        Morning,
        Attempts: 0,
        OutboxStatus.Queued,
        Version: null,
        Code: null,
        Reason: null);
}
