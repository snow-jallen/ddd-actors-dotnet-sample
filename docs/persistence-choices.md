# Persistence choices: does anyone need what happened?

Not every aggregate needs its history. This page compares the four ways Ohmline keeps state and
says which one each part of the system uses (section 11.7).

## The rule

**Choose by whether anyone will ever ask what happened, not by convenience.** If a driver, an
operator, an auditor, a projection or a future feature may need to know how the state came to
be, keep the events. If only the latest state will ever matter, keep only that.

A charging session's events are its invoice, its dispute record and every read model's input:
event sourced. A charge point's listing (display name, access instructions such as
`Level -2, bays 14-19`, opening hours) is read by the driver app and corrected by operators; no
one will ask how it read last March: state sourced.

## Durable State and Akka.NET

JVM Akka has *Durable State*: a `DurableStateBehavior` keeps only the latest state, with a
revision number, in a `DurableStateStore`, and offers a `DurableStateStoreQuery` for changes.
**Akka.NET has no Durable State.** Neither `Akka.Persistence`, `Akka.Persistence.Query` nor
`Akka.Persistence.Sql` contains such a type at 1.5.73 or at 1.6.0-beta5 (the editor checked the
assemblies and NuGet on 2026-11-04; the 1.5.73 and 1.6.0-beta5 assemblies were searched again
for `DurableState` on 2026-11-05, with no match). Ohmline therefore gets the same effect the Akka.NET way, on the
journal and snapshot store it already has, and adds no state-store plugin or table.

## The options

| | Event sourcing | State sourcing on the journal | State-stored repository behind an actor | No actor at all |
|---|---|---|---|---|
| **What is stored** | Every event, forever; snapshots as a cache | One event per change carrying the whole state; a snapshot after each; everything older deleted | One row per aggregate, overwritten | Rows in the service's own database |
| **History** | Complete | None: the latest state only | None | Whatever the service records |
| **Queries through projections** | Yes: tags and Persistence Query (Chapter 13) | Unreliable: a projection that falls behind finds the events it missed deleted | Only by polling the table or an outbox | Through the service's own outbox (Chapter 16) |
| **Storage growth** | Grows with activity; snapshots bound recovery, not storage | Bounded: one snapshot and one row marked deleted per aggregate | Bounded: one row | Up to the service |
| **Schema evolution** | Every event version is read forever (upcasters, Chapter 20) | Only the snapshot DTO, but it is the only copy: it must be read forever too | Migrate the table | Migrate the table |
| **Concurrency** | One writer per persistence id; sequence numbers (section 10.9) | The same: it is still a persistent actor | Optimistic: a revision column checked on every update | Database transactions |
| **Used in Ohmline for** | `ChargePoint`, `ChargingSession`, `SitePowerBudget` and every core aggregate | `ChargePointListing` | Nothing yet | Billing (section 16.3) |

A state-stored repository behind an actor is the closest thing to JVM Durable State: the actor
reads the row on start, writes it with `UPDATE ... SET revision = revision + 1 WHERE revision =
@expected`, and treats a missed update as a conflict. Ohmline does not need it: state sourcing on
the journal gives the same result with the shell, serializer, tests and operations it already
has.

## How state sourcing works in Ohmline

- **The decider** (`ChargePointListing` in `Ohmline.Charging.Domain/Listings`) still decides: it
  checks the text and records nothing when nothing changed. Each revision records one
  `ChargePointListingRevised` event carrying the whole listing and a `Revision`; `Evolve`
  replaces the state with it.
- **The shell** (`StateSourcedDeciderActor` in `Ohmline.ActorShell/Deciders`) saves a snapshot
  after every event and, once it is stored, deletes the older snapshots and the events up to it
  (`DeleteSnapshots`, `DeleteMessages`). If the actor stops between the event and the snapshot,
  the event is still there and recovery replays it.
- **Recovery** reads the one snapshot and replays nothing: the log line says `snapshot at 3, 0
  events replayed`.

## What the journal keeps (observed)

`StateSourcingTourTests` revises charge point `OHM-SLC-0042`'s listing three times on SQLite
(Akka.Persistence.Sql 1.5.73, `delete-compatibility-mode = false`, the default). Once the
deletes have run, the tables hold:

```text
journal:
ordering=3 | persistence_id=charge-point-listing-OHM-SLC-0042 | sequence_number=3
  | created=639294670519030755 (2026-11-05T09:17:31.9030755+00:00) | deleted=True
  | manifest=charging.charge-point-listing-revised.v1 | identifier=9101
  | writer_uuid=4d7d96460d2f47fe9b566cd17579f3b0
  | message={"displayName":"Salt Lake bus depot, public bays","accessInstructions":"Level -2, bays 14-19","openingHours":"24/7","revision":3,"chargePointId":"OHM-SLC-0042","commandId":"01a1a700-a600-7f3e-a204-b106bdb1feaa","occurredAt":"2026-11-04T14:40:00+00:00"}
snapshot:
persistence_id=charge-point-listing-OHM-SLC-0042 | sequence_number=3
  | created=639294670519040366 (2026-11-05T09:17:31.9040366+00:00)
  | manifest=charging.charge-point-listing-snapshot.v1 | serializer_id=9101
  | snapshot={"chargePointId":"OHM-SLC-0042","displayName":"Salt Lake bus depot, public bays","accessInstructions":"Level -2, bays 14-19","openingHours":"24/7","revision":3}
```

(One row per line in the test output; wrapped here.)

- **Events 1 and 2 are physically deleted.** Their rows are gone.
- **The last event stays, marked `deleted=True`.** It is never replayed, but it remembers the
  highest sequence number: there is no `journal_metadata` table in this schema, and after a
  restart the next revision is accepted as version 4. Its payload is still readable in the row,
  so "deleted" does not mean erased (Chapter 20 deals with data that must really go).
- **One snapshot**, at 3, with its own manifest. It is now the only copy of the listing, which
  is why its DTO (`ChargePointListingSnapshot`) has a golden file like any event. From section
  20.6 `snapshot-is-optional` is on for every aggregate; if the listing's snapshot cannot be
  read, `DeciderActor` sees that its events no longer start at 1 and stops it, with an ERROR,
  rather than let it start empty.

## Consequences for the listing

- No projection may depend on the listing's events: they disappear. The driver app asks the
  listing actor for the listing, never its event stream.
- The listing's snapshot is a permanent contract: changing its shape needs a new manifest version
  and a converter, exactly as an event would (Chapter 20).
- If anyone ever asks "who changed the access instructions, and when?", the answer is that we
  chose not to know. That question is the signal to switch the listing to event sourcing; the
  decider would not change, only the shell it runs in.

## Billing: the fourth column (section 16.3)

Billing keeps no history of its own and runs no actor. Its state is rows in its own database
(`billing.holds`, `billing.invoices`), written by transaction scripts with Npgsql:

- **A hold is a row** whose status moves from `placed` to `captured` or `voided`. Each request
  reads the row, decides with `HoldRules` (plain functions over the row) and writes the new row
  with `WHERE status = @expected`: a request that lost a race with another for the same hold
  changes nothing, reads again and decides again. The database is the single writer.
- **What happened is still recorded where someone asks for it:** an invoice per session, and a
  receipt message in Billing's outbox, written in the same database transaction.
- **What Billing reads from Charging** goes through an inbox: the position of the last charging
  feed entry it handled, stored with whatever the entry changed (an idempotent receiver).

Accountants want rows and an audit trail, not an event stream (ADR 0004); Billing is not where
Ohmline competes. If disputes ever need "how did this hold get here", that is the signal to
record payment events, and the rows would become a projection of them.
