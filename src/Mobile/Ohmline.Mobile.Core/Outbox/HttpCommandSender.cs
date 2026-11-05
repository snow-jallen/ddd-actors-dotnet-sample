using System.Net.Http.Json;
using System.Text;
using Ohmline.Charging.Client.Contracts;

namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// Sends an outbox row to its route under <c>/app/v1</c>, with the payload as it was stored,
/// and maps the answer (section 19.6): 202 is accepted; 400, 409 and 422 are rejected, with the
/// problem's code; 408, 429, a 5xx such as 503 or 504, a timeout or no network at all are
/// transient, because the command may or may not have been carried out.
/// </summary>
public sealed class HttpCommandSender(HttpClient http) : ICommandSender
{
    private static readonly ChargingClientJsonContext Json = ChargingClientJsonContext.Default;

    public async Task<SendOutcome> SendAsync(OutboxRow row, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent(row.Payload, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(RouteOf(row), content, cancellationToken);
            return (int)response.StatusCode switch
            {
                202 => new SendAccepted((await response.Content.ReadFromJsonAsync(
                    Json.CommandAcceptedDto, cancellationToken))!.Version),
                400 or 409 or 422 => await RejectedAsync(response, cancellationToken),
                var status => new SendTransientFailure($"HTTP {status}"),
            };
        }
        catch (HttpRequestException failure)
        {
            return new SendTransientFailure(failure.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SendTransientFailure("No answer before the client's timeout.");
        }
    }

    /// <summary>The route of the row's kind, for its aggregate.</summary>
    public static string RouteOf(OutboxRow row) => row.Kind switch
    {
        OutboxKinds.StartCharging => "/app/v1/journeys",
        OutboxKinds.StopCharging => $"/app/v1/sessions/{row.AggregateKey}/stop",
        OutboxKinds.ReserveConnector when row.AggregateKey.Split('/') is [var id, var number] =>
            $"/app/v1/charge-points/{id}/connectors/{number}/reservations",
        _ => throw new InvalidOperationException($"No route for a '{row.Kind}' command."),
    };

    private static async Task<SendOutcome> RejectedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync(Json.ProblemDto, cancellationToken);
        return new SendRejected(problem?.Code ?? "", problem?.Detail ?? problem?.Title ?? "");
    }
}
