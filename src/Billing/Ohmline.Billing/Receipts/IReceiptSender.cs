namespace Ohmline.Billing.Receipts;

/// <summary>Whatever takes a receipt to the driver: e-mail, a push to the app.</summary>
public interface IReceiptSender
{
    Task SendAsync(ReceiptIssued receipt, CancellationToken cancellationToken);
}

/// <summary>The sample's sender: it writes the receipt to the log.</summary>
public sealed class LoggingReceiptSender(ILogger<LoggingReceiptSender> log) : IReceiptSender
{
    public Task SendAsync(ReceiptIssued receipt, CancellationToken cancellationToken)
    {
        log.LogInformation(
            "Receipt for session {SessionId} sent for token {TokenUid}: {Total} {Currency}",
            receipt.SessionId,
            receipt.TokenUid,
            receipt.Total,
            receipt.Currency);
        return Task.CompletedTask;
    }
}
