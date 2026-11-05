namespace Ohmline.Charging.Client.Contracts.Replies;

/// <summary>
/// An RFC 9457 problem as the app reads it from a 4xx or 5xx answer. <paramref name="Code"/> is
/// one of <see cref="RejectionCodes"/>, or one this build of the app does not know yet;
/// <paramref name="Detail"/> is for logs, never for the driver. <paramref name="Errors"/> names
/// each field that is not valid, on a 422.
/// </summary>
public sealed record ProblemDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Code,
    IReadOnlyDictionary<string, string[]>? Errors = null);
