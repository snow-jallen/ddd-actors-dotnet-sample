namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// Where one of the driver's commands is, from the tap to the server's answer: the app's own
/// language for a command (section 19.1). Queued and sending are pending; accepted and
/// rejected are the server's answers, and final; failed to sync is the app giving up, after a
/// day without a connection.
/// </summary>
public union CommandStatus(Queued, Sending, Accepted, Rejected, FailedToSync);

/// <summary>Kept on the phone, waiting to be sent; tried this many times so far.</summary>
public sealed record Queued(int Attempts);

/// <summary>Being sent now: this attempt.</summary>
public sealed record Sending(int Attempt);

/// <summary>The server accepted it; the aggregate is at this version.</summary>
public sealed record Accepted(long Version);

/// <summary>
/// The server refused it, for the reason <paramref name="Code"/>, which
/// <paramref name="Text"/> says in the driver's words. Never sent again.
/// </summary>
public sealed record Rejected(string Code, string Text);

/// <summary>Not sent within its maximum age; never sent again.</summary>
public sealed record FailedToSync;
