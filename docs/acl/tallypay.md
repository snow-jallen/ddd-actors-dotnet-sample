# Anticorruption layer: TallyPay

TallyPay is the (fictional) payment provider behind Billing (section 16.4). Its API is its
own: upper-case field names, amounts in minor units, business dates as `yyyyMMdd`, and every
outcome as a two-digit `STATUS_CD`. None of that may reach Billing's model, let alone Charging's.

| Part | Where | What it does |
|---|---|---|
| `TallyPayClient` | `src/Billing/Ohmline.Billing/TallyPay/` | A typed `HttpClient`. Sends every call with an `X-Idempotency-Key`; returns a `PaymentOutcome`, never an exception or a TallyPay type |
| `TallyPayTranslator` | same | Pure: `STATUS_CD` to `PaymentOutcome`, decimal amounts to minor units, `yyyyMMdd` to `DateOnly` |
| Resilience | `TallyPayExtensions.AddTallyPay()` | Its own standard handler in place of the host's: 3 s per attempt, 3 retries from 250 ms, 15 s in all |
| Stand-in | `tools/Ohmline.FakePsp` | TallyPay's API with an in-memory ledger; `FLAKY_RATE` (0 to 1) answers that share of requests with 503 |

## The translation

| `STATUS_CD` | TallyPay says | `PaymentOutcome` | Billing's code |
|---|---|---|---|
| `00` with `AUTH_ID` and a date | Approved | `Authorized(reference, on)` | — |
| `00` without either | Approved, but unreadable | `ProviderUnavailable` | `provider-unavailable` |
| `05` | Do not honour | `Declined` | `do-not-honour` |
| `51` | Insufficient funds | `Declined` | `insufficient-funds` |
| `54` | Expired card | `Declined` | `card-expired` |
| `12` | Invalid transaction (no open authorization) | `Declined` | `unknown-authorization` |
| `13` | Invalid amount | `Declined` | `invalid-amount` |
| `91`, `96` | Issuer unavailable, system malfunction | `ProviderUnavailable` | `provider-unavailable` |
| anything else | ? | `Declined` | `declined-by-provider` |
| HTTP error, timeout, unreadable body | — | `ProviderUnavailable` | `provider-unavailable` |

`ProviderUnavailable` is not a refusal: nobody knows whether TallyPay acted. Billing answers
503, and the caller sends the same request again.

## Idempotency keys

| Call | Key |
|---|---|
| `POST /v1/preauth` | the hold id, which Charging sets to its own key `{journeyId}-hold` |
| `POST /v1/capture` | `{holdId}-capture` |
| `POST /v1/void` | `{holdId}-void` |

Because TallyPay answers a key it has seen with the answer it gave, the resilience handler may
retry a POST, Billing may be asked twice, and the journey may ask again after a restart: the
money moves once. A key without that promise would make every retry here a double charge.

## Amounts

TallyPay takes whole cents. Charging's prices can carry fractions of a cent (energy times a
rate per kWh); the translator rounds half away from zero, at the border, and nowhere else.
