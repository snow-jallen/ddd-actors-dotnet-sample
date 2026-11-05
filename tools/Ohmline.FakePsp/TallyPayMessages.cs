using System.Text.Json.Serialization;

namespace Ohmline.FakePsp;

/// <summary>A pre-authorization: reserve an amount on a card.</summary>
public sealed record PreauthRequest(
    [property: JsonPropertyName("MERCHANT_REF")] string MerchantReference,
    [property: JsonPropertyName("CARD_TOKEN")] string CardToken,
    [property: JsonPropertyName("AMT_MINOR")] long AmountMinor,
    [property: JsonPropertyName("CCY")] string Currency);

/// <summary>Take an amount from an authorization.</summary>
public sealed record CaptureRequest(
    [property: JsonPropertyName("AUTH_ID")] string AuthorizationId,
    [property: JsonPropertyName("AMT_MINOR")] long AmountMinor,
    [property: JsonPropertyName("CCY")] string Currency);

/// <summary>Release an authorization.</summary>
public sealed record VoidRequest([property: JsonPropertyName("AUTH_ID")] string AuthorizationId);

/// <summary>
/// Every answer TallyPay gives: a two-digit status code, the authorization it is about, the
/// business date as <c>yyyyMMdd</c>, and a message in capitals.
/// </summary>
public sealed record TallyPayReply(
    [property: JsonPropertyName("STATUS_CD")] string StatusCode,
    [property: JsonPropertyName("AUTH_ID")] string? AuthorizationId,
    [property: JsonPropertyName("TXN_DT")] string TransactionDate,
    [property: JsonPropertyName("MSG")] string Message);
