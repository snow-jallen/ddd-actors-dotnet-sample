using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.ActorShell.Tracing;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Client.Contracts.Replies;
using Ohmline.Charging.Contracts;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api;

/// <summary>
/// What every Charging endpoint does once it has a command or a question: ask the shard region,
/// wait no longer than the configured timeout, and turn the answer into an HTTP result.
/// </summary>
internal static class Edge
{
    /// <summary>
    /// 202 Accepted with the aggregate's new version and where to read it, or 409 with the
    /// rejection's code, or 504 when no answer came in time.
    /// </summary>
    public static Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>> SendAsync(
        IActorRef region,
        object command,
        string location,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        SendAsync(region, command, _ => location, timeout, cancellationToken);

    /// <summary>
    /// The same, for a resource read from a read model: where to read it depends on the new
    /// version, such as <c>/sessions/{id}?minVersion=7</c> (section 14.1).
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        SendAsync(
            IActorRef region,
            object command,
            Func<long, string> locationAt,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
        await AskCommandAsync(region, command, timeout, cancellationToken) switch
        {
            CommandAccepted accepted => TypedResults.Accepted(
                locationAt(accepted.Version), new CommandAcceptedResponse(accepted.Version)),
            CommandRejected rejected => Problems.Rejected(rejected),
            null => Problems.Timeout(timeout),
        };

    /// <summary>
    /// The same for the driver app (section 19.2), in its published language: the 202's body
    /// names the command by the id the app gave it, beside the aggregate's new version.
    /// </summary>
    public static async Task<Results<Accepted<CommandAcceptedDto>, ProblemHttpResult>>
        SendForAppAsync(
            IActorRef region,
            object command,
            CommandId commandId,
            Func<long, string> locationAt,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
        await AskCommandAsync(region, command, timeout, cancellationToken) switch
        {
            CommandAccepted accepted => TypedResults.Accepted(
                locationAt(accepted.Version),
                new CommandAcceptedDto(commandId.Value, accepted.Version)),
            CommandRejected rejected => Problems.Rejected(rejected),
            null => Problems.Timeout(timeout),
        };

    /// <summary>The aggregate's answer to a question, or null when none came in time.</summary>
    public static async Task<TReply?> AskAsync<TReply>(
        IActorRef region, object question, TimeSpan timeout, CancellationToken cancellationToken)
        where TReply : class
    {
        try
        {
            return await region.AskTraced<TReply>(question, timeout, cancellationToken);
        }
        catch (AskTimeoutException)
        {
            return null;
        }
    }

    /// <summary>The aggregate's reply to a command, or null when none came in time.</summary>
    private static Task<CommandReply?> AskCommandAsync(
        IActorRef region, object command, TimeSpan timeout, CancellationToken cancellationToken) =>
        AskAsync<CommandReply>(region, command, timeout, cancellationToken);
}
