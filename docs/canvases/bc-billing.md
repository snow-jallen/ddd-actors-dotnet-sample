# Bounded Context Canvas: Billing

*Bounded Context Canvas V5 (ddd-crew), filled in during section 5.1.*

## Name

**Billing**

## Purpose

Move the money for charging through TallyPay and keep the records that prove it: payment holds
before charging, captures and voids afterwards, debts when a capture fails, and a receipt for
every settled session.

## Strategic classification

| Domain | Business model | Evolution |
|---|---|---|
| Supporting (billing and receipts) | Compliance enforcer | Product |

## Domain roles

- **Audit model:** every amount that moved can be traced to a hold, a session and a receipt.
- **Gateway** to TallyPay: Billing is the only context that speaks to the payment provider.

## Inbound communication

Every message in these tables is a public contract: Billing's own messages live in
`Ohmline.Billing.Contracts` and travel as JSON over HTTP; the session it bills arrives in
Charging's Published Language.

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (journey) | Place hold (`POST /holds`) | Command | `Ohmline.Billing.Contracts` |
| Charging (journey) | Capture payment (`POST /holds/{id}/capture`), void hold (`POST /holds/{id}/void`) | Command | `Ohmline.Billing.Contracts` |
| Charging (feed) | `SessionCompleted` (`ohmline.charging.session-completed.v1`) | Event | `Ohmline.Charging.Contracts` (Published Language) |
| TallyPay | Pre-authorization, capture and void results | Event (reply) | TallyPay's API, behind Billing's ACL |
| Drivers, support staff (HTTP) | Receipts, open debts | Query | `Ohmline.Billing.Contracts` |

## Outbound communication

| Collaborator | Message | Kind | Contract |
|---|---|---|---|
| Charging (journey) | Hold placed, hold declined, payment captured, capture failed | Event (reply) | `Ohmline.Billing.Contracts` |
| TallyPay | Pre-authorize, capture, void | Command | TallyPay's API, behind Billing's ACL |
| Drivers | Receipt issued (through Billing's outbox) | Event | Billing's outbox |
| Accounting package | Daily export of captures and debts | Document | Accounting's import format |

## Ubiquitous language

| Term | Meaning here |
|---|---|
| Payment | A money movement at TallyPay: a hold, a capture or a void. Never a "transaction" |
| Hold (pre-authorization) | Money reserved on the driver's card before charging. Never a connector reservation |
| Capture | Taking the final amount from a hold; the rest is released |
| Void | Releasing a hold without taking any money |
| Debt | An amount owed after a capture failed for good or the price exceeded the hold |
| Receipt | The driver-facing record of a settled session |
| Session | Only a reference: the `SessionId` an invoice and a receipt line point at, with its price breakdown. Billing knows nothing about meter readings, connectors or suspensions |

## Business decisions

- No capture without a hold; a capture never exceeds the hold.
- If the price exceeds the hold, capture the whole hold and record the rest as a debt.
- A failed capture is retried after 1, 5 and 30 minutes with the same idempotency key, then
  recorded as a debt.
- A session with no energy delivered voids its hold.
- Each `SessionCompleted` is billed exactly once, however often the feed delivers it.

## Assumptions

- TallyPay's idempotency keys make retries safe.
- The charging feed may deliver an event more than once and may be read late, but never out of
  order.
- Accountants want rows and an audit trail, not an event stream (ADR 0004).

## Verification metrics

- For every session, the captured amount plus any debt equals the price breakdown total to the
  cent.
- Debts stay below 0.5% of settled sessions.
- A receipt is issued within 5 minutes of `SessionCompleted`.

## Open questions

- Who decides to write off a small debt, and after how long?
- Should Billing hold a little more than the spend cap to cover idle fees (story 03)?
