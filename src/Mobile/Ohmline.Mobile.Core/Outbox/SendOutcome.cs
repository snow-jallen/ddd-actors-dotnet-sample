namespace Ohmline.Mobile.Core.Outbox;

/// <summary>
/// What became of one attempt to send a command (section 19.6). Accepted and rejected are the
/// server's answers and final; a transient failure means nobody knows, so the command is sent
/// again later, with the same id.
/// </summary>
public union SendOutcome(SendAccepted, SendRejected, SendTransientFailure);

/// <summary>202: the aggregate accepted the command and is at this version.</summary>
public sealed record SendAccepted(long Version);

/// <summary>400, 409 or 422: refused, with a code; never sent again.</summary>
public sealed record SendRejected(string Code, string Reason);

/// <summary>No answer, a 5xx, 408 or 429: maybe carried out, maybe not; send it again.</summary>
public sealed record SendTransientFailure(string Why);
