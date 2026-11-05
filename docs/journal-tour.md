# A guided tour of the journal

What Akka.Persistence.Sql 1.5.73 writes to SQLite when charge point `OHM-SLC-0042` accepts two
commands and rejects a third (section 10.8). Everything below was captured from
`JournalTourTests` (`tests/Ohmline.Charging.Actors.Tests/JournalTour/`), which runs Charging as
`Ohmline.Node` runs it in Development: `WithSqlPersistence` with `ProviderName.SQLiteMS`,
`autoInitialize: true`, `TagMode.TagTable` and the real `ChargingSerializer`. The SELECT
statements are in `JournalTourQueries.cs`.

Reading these tables is for learning and for incident work. Application code never reads them:
it asks the aggregate's actor, and from Chapter 13 it reads the read journal.

## The scenario

| # | Command | Reply | Rows written |
|---|---|---|---|
| 1 | `RegisterChargePoint` (two 22 kW connectors at `slc-depot`) | `CommandAccepted { Version = 1 }` | sequence number 1 |
| 2 | `ReserveConnector` (connector 1, card `04A2B3C4D5E6F7`) | `CommandAccepted { Version = 2 }` | sequence number 2 |
| 3 | `OccupyConnector` (connector 1, card `04FFEEDDCC`) | `CommandRejected { Code = reserved-for-another-token }` | none |

## The tables

`autoInitialize: true` creates three tables the first time the journal is used (not when the
node starts: an idle node creates no database file). The journal table, as SQLite reports it:

```sql
CREATE TABLE [journal]
(
	[ordering]        INTEGER        NOT NULL PRIMARY KEY AUTOINCREMENT,
	[created]         INTEGER        NOT NULL,
	[deleted]         Bit            NOT NULL,
	[persistence_id]  NVarChar(255)  NOT NULL,
	[sequence_number] INTEGER        NOT NULL,
	[message]         VarBinary      NOT NULL,
	[manifest]        NVarChar(500)      NULL,
	[identifier]      INTEGER            NULL,
	[writer_uuid]     NVarChar(128)      NULL
)
CREATE UNIQUE INDEX journal_uq ON journal (persistence_id, sequence_number)
CREATE INDEX journal_created_idx ON journal (created)
CREATE INDEX journal_sequence_number_idx ON journal (sequence_number)
```

and beside it:

```sql
CREATE TABLE [tags]
(
	[ordering_id]    INTEGER       NOT NULL,
	[tag]            NVarChar(64)  NOT NULL,
	[sequence_nr]    INTEGER       NOT NULL,
	[persistence_id] NVarChar(255) NOT NULL,

	CONSTRAINT [PK_tags] PRIMARY KEY ([ordering_id], [tag])
)
CREATE TABLE [snapshot]
(
	[persistence_id]  NVarChar(255) NOT NULL,
	[sequence_number] INTEGER       NOT NULL,
	[created]         INTEGER       NOT NULL,
	[snapshot]        VarBinary         NULL,
	[manifest]        NVarChar(500)     NULL,
	[serializer_id]   INTEGER           NULL,

	CONSTRAINT [PK_snapshot] PRIMARY KEY ([persistence_id], [sequence_number])
)
```

In tag-table mode the journal has **no `tags` column at all**: tags live only in the `tags`
table.

## The rows

```text
ordering=1 | persistence_id=charge-point-OHM-SLC-0042 | sequence_number=1
  | created=639294642278539950 (2026-11-05T08:30:27.8539950+00:00) | deleted=False
  | manifest=charging.charge-point-registered.v1 | identifier=9101
  | writer_uuid=2620fa9aa3da4417baa4f57738f9d859
  | message={"siteId":"slc-depot","connectorMaxPowers":[22,22],"chargePointId":"OHM-SLC-0042","commandId":"01a1a700-a600-73d7-9e75-42534c1ac52b","occurredAt":"2026-11-04T14:40:00+00:00"}
ordering=2 | persistence_id=charge-point-OHM-SLC-0042 | sequence_number=2
  | created=639294642278638408 (2026-11-05T08:30:27.8638408+00:00) | deleted=False
  | manifest=charging.connector-reserved.v1 | identifier=9101
  | writer_uuid=2620fa9aa3da4417baa4f57738f9d859
  | message={"tokenUid":"04A2B3C4D5E6F7","expiresAt":"2026-11-04T14:55:00+00:00","connectorNumber":1,"chargePointId":"OHM-SLC-0042","commandId":"01a1a700-a600-73d7-9e75-42534c1ac52b","occurredAt":"2026-11-04T14:40:00+00:00"}
tags: 0 rows
snapshot: 0 rows
```

(One row per line in the test output; wrapped here. `writer_uuid`, `created` and `commandId`
differ on every run.)

## Column by column

| Column | What it holds | Who uses it |
|---|---|---|
| `ordering` | A global position across all persistence ids, assigned by the database in insert order | Persistence Query's offset (Chapter 13) |
| `persistence_id` | `charge-point-OHM-SLC-0042`: the aggregate's durable name (`ChargePointActor.PersistenceIdOf`) | Recovery, sharding (Chapter 12) |
| `sequence_number` | 1, 2, ... per persistence id, without gaps: the aggregate's version, the `Version` in `CommandAccepted` | Recovery; the unique index `journal_uq` (section 10.9) |
| `created` | When the journal wrote the row: .NET ticks (an `INTEGER`), UTC, from the machine's clock. Not the decision time: the event's own `occurredAt` comes from the node's `TimeProvider`, here a fake clock at 2026-11-04 14:40 UTC, while `created` is the real time of the test run | Retention scripts, incident timelines |
| `deleted` | A soft-delete flag (`Bit`), false for every row here | `DeleteMessages` (section 11.7) |
| `manifest` | `charging.charge-point-registered.v1`: chosen by our serializer, never a CLR type name | The serializer, on replay (ADR 0007) |
| `identifier` | `9101`: `ChargingSerializer.Identifier`, which serializer wrote the bytes | Akka.NET, to pick the serializer on replay |
| `message` | The event as UTF-8 JSON (`VarBinary`): System.Text.Json, camel case, value objects as raw values | The serializer only |
| `writer_uuid` | One id per actor incarnation; both rows came from the same one | Replay filtering when two writers ever wrote (section 10.9) |

What is **not** there:

- **No row for the rejected `OccupyConnector`.** A rejection is a reply, not an event: nothing
  was decided, so nothing was recorded, and `sequence_number` stays at 2.
- **No tags.** Nothing tags Charging's events yet; the tagging write adapter of Chapter 13 fills
  the `tags` table, keyed by `ordering_id`, which is the journal row's `ordering`.
- **No snapshot.** Snapshots arrive in section 11.2.
- **No command.** The journal stores what happened, never what was asked.

## Two writers

One live actor per persistence id is what makes the sequence number a version and the actor a
lock (section 10.9). `TwoIncarnationsTests` breaks that rule on purpose: it starts two
`ChargePointActor`s for `OHM-SLC-0042` under the names `first` and `second`, in the same node,
on the same SQLite file. Both recover to sequence number 2 (the charge point is registered and
connector 1 is reserved for card `04A2B3C4D5E6F7` until 14:55).

1. `first` accepts a reservation of connector 2 and writes it as sequence number 3.
2. `second` has a clock 16 minutes later. From what it recovered, the reservation of connector 1
   has lapsed, so it accepts card `04FFEEDDCC`'s reservation: `ReservationExpired` and
   `ConnectorReserved`, one `PersistAll`, sequence numbers 3 and 4.

The journal refuses the second write. Akka.Persistence.Sql writes a batch in one transaction, and
the unique index `journal_uq` on `(persistence_id, sequence_number)` rejects sequence number 3.
`second` logs at ERROR, from `OnPersistFailure`, and stops:

```text
akka://ohmline/user/second: Failed to persist event type [Ohmline.Charging.Domain.ChargePoints.ReservationExpired] with sequence number [3] for persistenceId [charge-point-OHM-SLC-0042].
Cause: SQLite Error 19: 'UNIQUE constraint failed: journal.persistence_id, journal.sequence_number'.
Dead letter: GetChargePointStatus { ChargePointId = OHM-SLC-0042 }
```

- **The sender of the failed command gets no reply** at all; it would time out. A watching
  probe sees `Terminated`.
- **Messages stashed behind the failed write become dead letters:** the status query sent to
  `second` right after the reservation is the dead letter above.
- **The batch failed whole:** there is no row 4, although only number 3 collided.
- **The journal holds the first writer's history only,** one row per sequence number, all with
  the same `writer_uuid`:

```text
ordering=1 | sequence_number=1 | manifest=charging.charge-point-registered.v1 | writer_uuid=6312b8dd923d44b5a4dc67ef88e7e1e0
ordering=2 | sequence_number=2 | manifest=charging.connector-reserved.v1 | writer_uuid=6312b8dd923d44b5a4dc67ef88e7e1e0
ordering=3 | sequence_number=3 | manifest=charging.connector-reserved.v1 | writer_uuid=6312b8dd923d44b5a4dc67ef88e7e1e0
```

(Trimmed to four columns; the test output has every column.)

- **A fresh incarnation recovers the first writer's events:** version 3, both connectors
  reserved for `04A2B3C4D5E6F7`.

What protects reads if two writers ever got rows in, for example on a journal without the unique
index: every row carries its writer's `writer_uuid`, and Akka.Persistence's replay filter
(`akka.persistence.journal-plugin-fallback.replay-filter.mode = repair-by-discard-old`, the
default in 1.5.73) discards events of an older writer that overlap a newer one's sequence
numbers, and logs a warning. That path is not exercised here: SQLite's unique index stops the
second writer first.

None of this is a concurrency strategy. Sharding and the split-brain resolver keep one writer
per persistence id (Chapter 12); this is the net under them.
