using Akka.Actor;
using Akka.Hosting;
using Akka.Serialization;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Ohmline.DriverAccounts.Actors.Serialization;
using Ohmline.DriverAccounts.Api.Requests;
using Ohmline.DriverAccounts.Api.Responses;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;
using Ohmline.Testing;

namespace Ohmline.DriverAccounts.Api.Tests;

/// <summary>A probe plays the id token region; every message is serialized on its way.</summary>
public sealed class DriverAccountsEndpointsTests(ITestOutputHelper output)
    : TestKit(ActorTestConfig.With(DriverAccounts), output: output)
{
    private static readonly TokenUid Card = new("04A2B3C4D5E6F7");

    private static readonly IOptions<DriverAccountsApiOptions> Options =
        Microsoft.Extensions.Options.Options.Create(
            new DriverAccountsApiOptions { AskTimeout = TimeSpan.FromSeconds(3) });

    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 11, 4, 14, 40, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Given_a_card_and_an_account_then_the_token_is_registered_to_it()
    {
        var region = CreateTestProbe();

        var answer = DriverAccountsEndpoints.RegisterAsync(
            new RegisterTokenRequest("04a2b3c4d5e6f7", "drv-7f3a91c2"),
            null,
            Tokens(region),
            _clock,
            Options,
            Ct);
        var register = await region.ExpectMsgAsync<RegisterToken>(cancellationToken: Ct);
        region.Reply(new TokenCommandAccepted(1));

        register.ShouldBe(
            new RegisterToken(register.CommandId, Card, new DriverRef("drv-7f3a91c2")));
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Location.ShouldBe("/tokens/04A2B3C4D5E6F7");
    }

    [Fact]
    public async Task Given_a_driver_named_rather_than_referenced_then_it_is_not_valid()
    {
        var answer = await DriverAccountsEndpoints.RegisterAsync(
            new RegisterTokenRequest("04A2B3C4D5E6F7", "jane.doe@example.com"),
            null,
            Tokens(CreateTestProbe()),
            _clock,
            Options,
            Ct);

        var problem = answer.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors.Keys
            .ShouldBe(["driver"]);
    }

    [Fact]
    public async Task Given_a_card_reported_lost_then_the_token_is_blocked()
    {
        var region = CreateTestProbe();

        var answer = DriverAccountsEndpoints.BlockAsync(
            "04A2B3C4D5E6F7", null, Tokens(region), _clock, Options, Ct);
        var block = await region.ExpectMsgAsync<BlockToken>(cancellationToken: Ct);
        region.Reply(new TokenCommandAccepted(3));

        block.TokenUid.ShouldBe(Card);
        (await answer).Result.ShouldBeOfType<Accepted<CommandAcceptedResponse>>()
            .Value.ShouldBe(new CommandAcceptedResponse(3));
    }

    [Fact]
    public async Task Given_an_unknown_card_when_blocked_then_409_says_it_is_not_registered()
    {
        var region = CreateTestProbe();

        var answer = DriverAccountsEndpoints.BlockAsync(
            "04A2B3C4D5E6F7", null, Tokens(region), _clock, Options, Ct);
        await region.ExpectMsgAsync<BlockToken>(cancellationToken: Ct);
        region.Reply(new TokenCommandRejected("not-registered", "Not registered."));

        var problem = (await answer).Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Extensions["code"].ShouldBe("not-registered");
    }

    private static IRequiredActor<IdTokenRegion> Tokens(TestProbe probe) => new Region(probe.Ref);

    private static SerializerDetails DriverAccounts(ExtendedActorSystem system) =>
        SerializerDetails.Create(
            DriverAccountsSerializer.Name,
            new DriverAccountsSerializer(system),
            [.. DriverAccountsSerializer.BoundTypes]);

    private sealed class Region(IActorRef actor) : IRequiredActor<IdTokenRegion>
    {
        public IActorRef ActorRef => actor;

        public Task<IActorRef> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(actor);
    }
}
