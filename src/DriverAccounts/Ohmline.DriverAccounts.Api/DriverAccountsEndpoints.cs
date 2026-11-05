using Akka.Actor;
using Akka.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ohmline.ActorShell.Tracing;
using Ohmline.DriverAccounts.Api.Requests;
using Ohmline.DriverAccounts.Api.Responses;
using Ohmline.DriverAccounts.Contracts.Tokens;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DriverAccounts.Api;

/// <summary>Driver Accounts' HTTP edge: register a token, block it, and read it.</summary>
public static class DriverAccountsEndpoints
{
    extension(IServiceCollection services)
    {
        /// <summary>Settings from <c>Ohmline:DriverAccounts</c>, checked at the start.</summary>
        public IServiceCollection AddDriverAccountsApi()
        {
            services.AddOptions<DriverAccountsApiOptions>()
                .BindConfiguration(DriverAccountsApiOptions.Section)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapDriverAccountsEndpoints()
        {
            var tokens = endpoints.MapGroup("/tokens").WithTags("Id tokens");
            tokens.MapPost("", RegisterAsync);
            tokens.MapPost("/{uid}/block", BlockAsync);
            tokens.MapGet("/{uid}", GetAsync);
            return endpoints;
        }
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        RegisterAsync(
            RegisterTokenRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<IdTokenRegion> tokens,
            TimeProvider clock,
            IOptions<DriverAccountsApiOptions> options,
            CancellationToken cancellationToken)
    {
        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        var tokenUid = fields.Read("tokenUid", TokenUid.TryCreate(request.TokenUid));
        var driver = fields.Read("driver", DriverRef.TryCreate(request.Driver));
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await SendAsync(
            tokens.ActorRef,
            new RegisterToken(commandId, tokenUid, driver),
            $"/tokens/{tokenUid}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        BlockAsync(
            string uid,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IRequiredActor<IdTokenRegion> tokens,
            TimeProvider clock,
            IOptions<DriverAccountsApiOptions> options,
            CancellationToken cancellationToken)
    {
        if (!TokenUid.TryCreate(uid).TryGet(out var tokenUid))
        {
            return Problems.NotFound($"'{uid}' is not a token UID.");
        }

        var fields = new RequestFields();
        var commandId = fields.CommandIdFrom(idempotencyKey, clock);
        if (!fields.AreValid)
        {
            return fields.Problem();
        }

        return await SendAsync(
            tokens.ActorRef,
            new BlockToken(commandId, tokenUid),
            $"/tokens/{tokenUid}",
            options.Value.AskTimeout,
            cancellationToken);
    }

    public static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> GetAsync(
        string uid,
        IRequiredActor<IdTokenRegion> tokens,
        IOptions<DriverAccountsApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!TokenUid.TryCreate(uid).TryGet(out var tokenUid))
        {
            return Problems.NotFound($"'{uid}' is not a token UID.");
        }

        var timeout = options.Value.AskTimeout;
        try
        {
            var view = await tokens.ActorRef.AskTraced<IdTokenView>(
                new GetIdToken(tokenUid), timeout, cancellationToken);
            return view.Driver is { } driver
                ? TypedResults.Ok(TokenResponse.From(view, driver))
                : Problems.NotFound($"Token {tokenUid} is not registered.");
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }

    private static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        SendAsync(
            IActorRef tokens,
            IdTokenCommand command,
            string location,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        try
        {
            var reply = await tokens.AskTraced<TokenCommandReply>(
                command, timeout, cancellationToken);
            return reply switch
            {
                TokenCommandAccepted accepted =>
                    TypedResults.Accepted(location, new CommandAcceptedResponse(accepted.Version)),
                TokenCommandRejected rejected => Problems.Rejected(rejected),
                TokenLocked or TokenLockRefused => throw new InvalidOperationException(
                    $"{command.GetType().Name} is not answered with {reply.GetType().Name}."),
            };
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }
}
