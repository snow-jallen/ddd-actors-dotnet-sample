namespace Ohmline.Charging.Client.Contracts;

/// <summary>
/// Why the server refused an app's command: the <c>code</c> of a 409 or 422 problem. Stable
/// strings, meant for programs; the app turns each into a sentence in its own words. Published
/// vocabulary: values are added, never changed or reused, and an app must expect a code that
/// is not in its copy of this list.
/// </summary>
public static class RejectionCodes
{
    /// <summary>Some fields of the request are not valid (422); the problem names them.</summary>
    public const string InvalidRequest = "invalid-request";

    /// <summary>The charge point has no such connector, or is not registered at all.</summary>
    public const string UnknownConnector = "unknown-connector";

    /// <summary>The charge point is not registered.</summary>
    public const string NotRegistered = "not-registered";

    /// <summary>The connector is out of service, or charging someone else.</summary>
    public const string ConnectorUnavailable = "connector-unavailable";

    /// <summary>Another driver's token holds the connector's reservation.</summary>
    public const string ReservedForAnotherToken = "reserved-for-another-token";

    /// <summary>The connector reported a fault and is waiting for repair.</summary>
    public const string ConnectorFaulted = "connector-faulted";

    /// <summary>The session to stop never started.</summary>
    public const string NotStarted = "not-started";

    /// <summary>The session to stop has already ended.</summary>
    public const string AlreadyEnded = "already-ended";

    /// <summary>
    /// Another command already started the journey the request names (section 19.3): the app
    /// made a new command but reused a journey id.
    /// </summary>
    public const string JourneyExists = "journey-exists";

    /// <summary>
    /// The request's <c>Idempotency-Key</c> header names another command than its body (400).
    /// </summary>
    public const string IdempotencyKeyMismatch = "idempotency-key-mismatch";

    /// <summary>Every code this version of the published language knows, oldest first.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        InvalidRequest,
        UnknownConnector,
        NotRegistered,
        ConnectorUnavailable,
        ReservedForAnotherToken,
        ConnectorFaulted,
        NotStarted,
        AlreadyEnded,
        JourneyExists,
        IdempotencyKeyMismatch,
    ];
}
