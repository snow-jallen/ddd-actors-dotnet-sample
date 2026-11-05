using System.Collections.Concurrent;
using System.Globalization;

namespace Ohmline.FakePsp;

/// <summary>
/// TallyPay's books, in memory: authorizations and what was taken from them. Every request
/// with an idempotency key it has seen gets the answer it got the first time, and changes
/// nothing more.
/// </summary>
public sealed class TallyPayLedger(TimeProvider clock)
{
    private const long MostApproved = 50_000; // 500.00: more is "insufficient funds".

    private readonly ConcurrentDictionary<string, TallyPayReply> _answered = new();
    private readonly ConcurrentDictionary<string, Authorization> _authorizations = new();
    private readonly Lock _gate = new();

    public TallyPayReply Preauthorize(string? key, PreauthRequest request) =>
        Once(key, () => request switch
        {
            { CardToken: var card } when card.Contains("declined", StringComparison.Ordinal) =>
                Reply("05", null, "DO NOT HONOUR"),
            { CardToken: var card } when card.Contains("expired", StringComparison.Ordinal) =>
                Reply("54", null, "EXPIRED CARD"),
            { AmountMinor: <= 0 } => Reply("13", null, "INVALID AMOUNT"),
            { AmountMinor: > MostApproved } => Reply("51", null, "INSUFFICIENT FUNDS"),
            _ => Authorize(request),
        });

    public TallyPayReply Capture(string? key, CaptureRequest request) =>
        Once(key, () =>
        {
            if (!_authorizations.TryGetValue(request.AuthorizationId, out var authorization)
                || authorization.Status != "open")
            {
                return Reply("12", request.AuthorizationId, "INVALID TRANSACTION");
            }

            if (request.AmountMinor > authorization.AmountMinor
                || request.Currency != authorization.Currency)
            {
                return Reply("13", request.AuthorizationId, "INVALID AMOUNT");
            }

            _authorizations[request.AuthorizationId] = authorization with { Status = "captured" };
            return Reply("00", request.AuthorizationId, "CAPTURED");
        });

    public TallyPayReply Void(string? key, VoidRequest request) =>
        Once(key, () =>
        {
            if (!_authorizations.TryGetValue(request.AuthorizationId, out var authorization)
                || authorization.Status != "open")
            {
                return Reply("12", request.AuthorizationId, "INVALID TRANSACTION");
            }

            _authorizations[request.AuthorizationId] = authorization with { Status = "voided" };
            return Reply("00", request.AuthorizationId, "VOIDED");
        });

    private TallyPayReply Once(string? key, Func<TallyPayReply> answer)
    {
        if (key is null)
        {
            return answer();
        }

        lock (_gate)
        {
            return _answered.GetOrAdd(key, _ => answer());
        }
    }

    private TallyPayReply Authorize(PreauthRequest request)
    {
        var id = $"TP{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        _authorizations[id] = new Authorization(request.AmountMinor, request.Currency, "open");
        return Reply("00", id, "APPROVED");
    }

    private TallyPayReply Reply(string status, string? authorizationId, string message) => new(
        status,
        authorizationId,
        clock.GetUtcNow().ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        message);

    private sealed record Authorization(long AmountMinor, string Currency, string Status);
}
