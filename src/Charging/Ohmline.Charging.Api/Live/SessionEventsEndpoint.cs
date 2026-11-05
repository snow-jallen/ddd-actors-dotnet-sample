using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using Ohmline.Charging.Api.App;
using Ohmline.Charging.Api.Queries;
using Ohmline.Charging.Client.Contracts.Notifications;
using Internal = Ohmline.Charging.Contracts.Live;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.Charging.Api.Live;

/// <summary>
/// <c>GET /sessions/{id}/events</c> (section 14.4): a session's progress pushed to a browser as
/// Server-Sent Events. The first event is the session history's row, then one event per change
/// the projection commits, from whichever node runs it. Each event's id is the session's
/// version, so a browser that reconnects sends <c>Last-Event-ID</c> and gets only what is newer.
/// The stream ends with the session; a reconnect after that gets 204, which tells the browser
/// to stop.
/// </summary>
public static class SessionEventsEndpoint
{
    /// <summary><c>SessionHistoryStatus.Ended</c>, as the query API returns it.</summary>
    private const string Ended = "ended";

    /// <summary>Only the newest summary matters to a browser that is slow to read.</summary>
    private static readonly BoundedChannelOptions Newest = new(16)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = true,
    };

    public static async Task<
            Results<ServerSentEventsResult<SessionSummaryChanged>, NoContent, ProblemHttpResult>>
        GetAsync(
            Guid id,
            [FromHeader(Name = "Last-Event-ID")] string? lastEventId,
            ActorSystem system,
            IRequiredActor<DistributedPubSub> mediator,
            [FromServices] NpgsqlDataSource readModels,
            [FromServices] IOptions<ReadModelOptions> options,
            CancellationToken cancellationToken)
    {
        var history = SessionHistorySource.Of(options.Value.SessionHistoryVersion);
        if (!SessionId.TryCreate(id).TryGet(out var sessionId))
        {
            return Problems.NotFound($"'{id}' is not a session id.");
        }

        var seen = long.TryParse(lastEventId, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;
        if (await SessionEndpoint.FindAsync(readModels, history, sessionId, cancellationToken) is
            { Status: Ended } ended && ended.Version <= seen)
        {
            return TypedResults.NoContent(); // It has seen the end: nothing more will come.
        }

        return TypedResults.ServerSentEvents(SummariesAsync(
            sessionId, seen, system, mediator.ActorRef, readModels, history, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<SessionSummaryChanged>> SummariesAsync(
        SessionId sessionId,
        long seen,
        ActorSystem system,
        IActorRef mediator,
        NpgsqlDataSource readModels,
        SessionHistorySource history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var changes = Channel.CreateBounded<Internal.SessionSummaryChanged>(Newest);
        var subscribed =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = system.ActorOf(
            SessionSummarySubscriber.Props(mediator, sessionId, changes.Writer, subscribed));
        try
        {
            // Subscribed first, then read: a change committed in between arrives on the channel.
            await subscribed.Task.WaitAsync(cancellationToken);
            var current = await SessionEndpoint.FindAsync(
                readModels, history, sessionId, cancellationToken);
            var summaries = changes.Reader.ReadAllAsync(cancellationToken)
                .Select(changed => changed.ToClient());
            if (current is not null)
            {
                summaries = summaries.Prepend(current.ToSummary());
            }

            await foreach (var summary in summaries)
            {
                if (summary.Version <= seen)
                {
                    continue; // Sent already, or older than what was sent.
                }

                seen = summary.Version;
                yield return new SseItem<SessionSummaryChanged>(summary)
                {
                    EventId = summary.Version.ToString(CultureInfo.InvariantCulture),
                };
                if (summary.Status == Ended)
                {
                    yield break;
                }
            }
        }
        finally
        {
            system.Stop(subscriber);
        }
    }
}
