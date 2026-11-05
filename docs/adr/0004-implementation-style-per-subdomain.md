# 0004. An implementation style per subdomain

- **Status:** Accepted
- **Date:** 2026-11-04
- **Book section:** ch04-s08

## Context

`docs/subdomains.md` classifies Ohmline's subdomains as core, supporting or generic. ADR 0002
says that every aggregate is a pure decider in an actor shell, and ADR 0001 that every aggregate
has a single writer. Neither says which parts of Ohmline need aggregates at all.

Vlad Khononov names four patterns for business logic, in order of cost and power:

| Pattern | Shape | Fits when |
|---|---|---|
| Transaction script | One procedure per operation, run in a database transaction | The logic is simple: mostly recording and reading data |
| Active record | Transaction scripts over objects that load and save themselves | Simple logic over richer data structures |
| Domain model | Aggregates and value objects that protect invariants | The rules are complex and must hold under every sequence of commands |
| Event-sourced domain model | A domain model whose state is the fold of its stored events | As above, and the history itself matters: audit, money, time-based analysis, or rebuilding new read models later |

In this sample an event-sourced aggregate also means a sharded persistent Akka.NET actor, with a
journal, a serializer, manifests, snapshots and projections. That machinery buys a single writer
per aggregate, location transparency and a complete history. It also costs code, durable names
that never change (`docs/naming-rules.md`) and a read side for every query. Spending it on a
subdomain that is mostly bookkeeping makes that subdomain harder to change, not safer.

## Decision

We will choose the business-logic pattern per bounded context, from its subdomain type and the
nature of its rules:

| Context | Subdomain type | Pattern | Akka.NET |
|---|---|---|---|
| Charging | Core | Event-sourced domain model: `ChargePoint`, `ChargingSession` and the `ChargingJourney` process manager as deciders | Sharded persistent actors, timers, projections |
| Site Energy | Core | Event-sourced domain model: `SitePowerBudget`, with the fair-share allocator as a pure Cohesive Mechanism | A sharded persistent actor per site |
| Tariffs | Supporting | Domain model with a Knowledge Level: tariffs are versioned data, and pricing is a pure domain service | One persistent cluster singleton holds the tariff book; no aggregate per tariff |
| Driver Accounts | Supporting | A small event-sourced domain model: `IdToken`, a reservation aggregate keyed by token UID to keep "one active session per token" | A sharded persistent actor, the simplest in the sample |
| Billing | Supporting | Transaction scripts over state stored in Postgres with Npgsql: holds, debts, invoices and receipts as rows, an inbox for Charging's feed and a transactional outbox | **None** |

For the other parts of Ohmline:

- **The Device Gateway** has no business rules of its own. It is a pure protocol translator plus
  one plain (not persistent) actor per charge point connection.
- **Generic subdomains** are bought or integrated (TallyPay, Northgate Grid, AssetTrack) behind
  anticorruption layers, which are pure translators.

We will not make a context event-sourced, or put it in an actor, because the rest of the sample
is built that way.

## Consequences

- The core subdomains get the full treatment: deciders with exhaustive decision tables, property
  tests of their invariants, a complete history in the journal, and projections for every
  screen. That is where Ohmline competes, and where a lost or reordered fact costs the most.
- Driver Accounts uses the same machinery for a different reason: uniqueness across aggregates
  is awkward with actors, and keying a small aggregate on the token UID turns the rule into a
  local check (section 12.7). The aggregate stays small on purpose.
- Tariffs is modeled carefully but stored simply. Its volatility lives in the data that the
  pricing analyst edits, so new prices need no new code, and a session's price depends only on
  the tariff snapshot it carries.
- Billing is the deliberate counter-example. It has no actors, no journal and no decider, and its
  tests need no actor system (section 16.3). Its audit trail is its tables and its outbox, which
  is what accountants expect anyway.
- The sample runs two styles of service side by side: an Akka.NET cluster (`Ohmline.Node`, the
  gateway) and an ordinary web service (`Ohmline.Billing`). They integrate through published
  contracts and HTTP, not through actor references (Chapter 16).
- **The pitfall this ADR exists to prevent is "everything is an event-sourced sharded actor".**
  A new context starts as the simplest pattern its rules allow. It moves up the table when a
  rule, a concurrency problem or a need for history demands it, and the move is recorded as a
  new ADR that supersedes the row above.
- Within a context, an aggregate whose history nobody needs may store only its latest state on
  the same journal; section 11.7 shows this for the charge point listing.

## References

- Vlad Khononov, *Learning Domain-Driven Design*, O'Reilly, 2021, chapters 5–7 (business-logic
  patterns) and chapter 10 (design heuristics).
- Eric Evans, *Domain-Driven Design Reference*, 2015, "Core Domain", "Generic Subdomains" and
  "Cohesive Mechanisms",
  https://domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf
- Martin Fowler, *Patterns of Enterprise Application Architecture*, Addison-Wesley, 2002:
  Transaction Script, Active Record, Domain Model.
