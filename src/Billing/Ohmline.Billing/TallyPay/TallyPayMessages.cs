using System.Text.Json.Serialization;

namespace Ohmline.Billing.TallyPay;

// TallyPay's API in TallyPay's words (section 16.4). These shapes never leave this folder:
// TallyPayTranslator turns every answer into a PaymentOutcome before anything else sees it.

internal sealed record TallyPayPreauth(
    [property: JsonPropertyName("MERCHANT_REF")] string MerchantReference,
    [property: JsonPropertyName("CARD_TOKEN")] string CardToken,
    [property: JsonPropertyName("AMT_MINOR")] long AmountMinor,
    [property: JsonPropertyName("CCY")] string Currency);

internal sealed record TallyPayCapture(
    [property: JsonPropertyName("AUTH_ID")] string AuthorizationId,
    [property: JsonPropertyName("AMT_MINOR")] long AmountMinor,
    [property: JsonPropertyName("CCY")] string Currency);

internal sealed record TallyPayVoid(
    [property: JsonPropertyName("AUTH_ID")] string AuthorizationId);

/// <summary>
/// Every answer TallyPay gives: a two-digit <c>STATUS_CD</c>, the authorization, the business
/// date as <c>yyyyMMdd</c> and a message in capitals.
/// </summary>
public sealed record TallyPayReply(
    [property: JsonPropertyName("STATUS_CD")] string? StatusCode,
    [property: JsonPropertyName("AUTH_ID")] string? AuthorizationId,
    [property: JsonPropertyName("TXN_DT")] string? TransactionDate,
    [property: JsonPropertyName("MSG")] string? Message);
