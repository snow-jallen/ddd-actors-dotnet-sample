using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Ohmline.Billing.Contracts.Receipts;
using Ohmline.Billing.Invoicing;

namespace Ohmline.Billing.Receipts;

/// <summary>A settled session's receipt, as drivers and support staff see it.</summary>
public static class ReceiptsEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapReceiptsEndpoints()
        {
            endpoints.MapGet("/receipts/{sessionId:guid}", GetAsync).WithTags("Receipts");
            return endpoints;
        }
    }

    /// <summary>
    /// <c>GET /receipts/{sessionId}</c>: 404 until Billing has read the session from the
    /// charging feed, which is usually within a second or two of its end.
    /// </summary>
    public static async Task<Results<Ok<ReceiptResponse>, ProblemHttpResult>> GetAsync(
        Guid sessionId, NpgsqlDataSource database, CancellationToken cancellationToken) =>
        await InvoiceStore.FindAsync(database, sessionId, cancellationToken) is { } invoice
            ? TypedResults.Ok(invoice.ToReceipt())
            : Problems.NotFound("unknown-receipt", $"No receipt for session {sessionId} yet.");
}
