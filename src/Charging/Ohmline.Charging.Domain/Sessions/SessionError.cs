using Ohmline.SharedKernel.Identity;
using Ohmline.SharedKernel.Monetary;

namespace Ohmline.Charging.Domain.Sessions;

/// <summary>
/// Why a session refused a command. One case is not the session's own: a spend cap in another
/// currency than the tariff is the shared kernel's <see cref="CurrencyMismatch"/>, used as it is.
/// </summary>
public union SessionError(
    SessionError.NotStarted,
    SessionError.AlreadyStarted,
    SessionError.AlreadyEnded,
    SessionError.SpendCapNotPositive,
    CurrencyMismatch)
{
    /// <summary>A sentence for logs and people. Code decides by the case, never by this.</summary>
    public string Message => this switch
    {
        NotStarted error => $"Session {error.SessionId} has not started.",
        AlreadyStarted error => $"Session {error.SessionId} has already started.",
        AlreadyEnded error => $"Session {error.SessionId} has already ended.",
        SpendCapNotPositive error => $"A spend cap must be more than zero, not {error.SpendCap}.",
        CurrencyMismatch error => error.Message,
        null => "This SessionError is default: it holds no case.",
    };

    public sealed record NotStarted(SessionId SessionId);

    public sealed record AlreadyStarted(SessionId SessionId);

    public sealed record AlreadyEnded(SessionId SessionId);

    public sealed record SpendCapNotPositive(Money SpendCap);
}
