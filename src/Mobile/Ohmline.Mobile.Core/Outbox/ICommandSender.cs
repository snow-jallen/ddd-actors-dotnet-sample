namespace Ohmline.Mobile.Core.Outbox;

/// <summary>Sends one command from the outbox to the server, once, and says how it went.</summary>
public interface ICommandSender
{
    Task<SendOutcome> SendAsync(OutboxRow row, CancellationToken cancellationToken);
}
