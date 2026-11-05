using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Ohmline.Charging.Api.Responses;
using Ohmline.Charging.Contracts;

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
    public static async Task<Results<Accepted<CommandAcceptedResponse>, ProblemHttpResult>>
        SendAsync(
            IActorRef region,
            object command,
            string location,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        try
        {
            var reply = await region.Ask<CommandReply>(command, timeout, cancellationToken);
            return reply switch
            {
                CommandAccepted accepted =>
                    TypedResults.Accepted(location, new CommandAcceptedResponse(accepted.Version)),
                CommandRejected rejected => Problems.Rejected(rejected),
            };
        }
        catch (AskTimeoutException)
        {
            return Problems.Timeout(timeout);
        }
    }

    /// <summary>The aggregate's answer to a question, or null when none came in time.</summary>
    public static async Task<TReply?> AskAsync<TReply>(
        IActorRef region, object question, TimeSpan timeout, CancellationToken cancellationToken)
        where TReply : class
    {
        try
        {
            return await region.Ask<TReply>(question, timeout, cancellationToken);
        }
        catch (AskTimeoutException)
        {
            return null;
        }
    }
}
