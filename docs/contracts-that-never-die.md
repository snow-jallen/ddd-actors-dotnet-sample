# Contracts that never die

What Ohmline writes down that it can never take back (section 20.1). The journal is append-only:
an event stored today is replayed by every version of Ohmline that ever recovers its aggregate,
so whatever that replay depends on is a contract with the future. ADR 0007 (serializer ids and
manifests) and ADR 0009 (durable names) hold the decisions; this page is the map.

## Three schemas, not one

An event exists in three shapes, and only the first is ours to change at will.

| Schema | Where it lives | Who reads it | Free to change? |
|---|---|---|---|
| The **domain event** | a `sealed record` in `*.Domain`, such as `ConnectorReserved` | `Decide` and `Evolve`, in memory | Yes: rename it, move it, refactor it, as long as the serializer still maps its manifest to it |
| The **persisted representation** | a journal row: serializer id, manifest, payload bytes | every future recovery, projection and rebuild | Never in place. A new shape is a new manifest version, and the old one is read for ever (sections 20.2 to 20.4) |
| The **wire message** | the same bytes between two nodes | the other node, which may run the previous release during a rolling update | Only in two steps: first every node learns to read it, then a node may send it (section 20.2) |

The serializer is the anti-corruption layer between the first and the other two. It is why the
C# type name appears nowhere in the journal.

## What a row holds on PostgreSQL

The SQLite tour of section 10.8 (`docs/journal-tour.md`) has the same columns, in PostgreSQL's
types, as `autoInitialize` creates them (checked by `JournalInspectTests` in
`Ohmline.IntegrationTests`):

| Column | Type | What it is | Contract? |
|---|---|---|---|
| `ordering` | `bigint` (`bigserial`) | the journal's global position; projections store it as their offset | yes, for every offset store |
| `created` | `bigint` | .NET ticks (UTC) from the writer's clock, not the decision's time | no |
| `deleted` | `boolean` | set by `DeleteMessages`; the payload stays | no |
| `persistence_id` | `text` | `{shard type name}-{entity id}`, such as `charge-point-OHM-SLC-0042` | **yes** |
| `sequence_number` | `bigint` | the aggregate's version; unique with the persistence id | **yes** |
| `message` | `bytea` | the payload, as the serializer wrote it | **yes** |
| `manifest` | `text` | what the payload is, in the serializer's words | **yes** |
| `identifier` | `integer` | which serializer wrote it | **yes** |
| `writer_uuid` | `text` | the incarnation that wrote it | no |

In tag-table mode the tags are rows of their own in `tags`, written once with the event and never
rewritten.

Since section 17.7, most of the node's events are stored in a trace envelope: the row says
`9100` and `ohmline.trace-envelope.v1`, and the envelope keeps the event's own serializer id,
manifest and bytes beside the trace. Both layers are contracts.

## Looking at it: `tools/Ohmline.JournalInspect`

A console tool that reads the journal or snapshot table with plain ADO.NET and the tour's SELECT
statements, and opens trace envelopes. It knows no Ohmline type, so it shows what is stored, not
what today's code makes of it.

```bash
dotnet run --project tools/Ohmline.JournalInspect -- \
    --postgres "Host=localhost;Port=5432;Database=ohmline;Username=postgres;Password=..." \
    --persistence-id charge-point-OHM-SLC-0042
dotnet run --project tools/Ohmline.JournalInspect -- --sqlite src/Ohmline.Node/ohmline.db \
    --after 0 --limit 20
dotnet run --project tools/Ohmline.JournalInspect -- --postgres "..." \
    --persistence-id charge-point-OHM-SLC-0042 --snapshots
```

Two rows written by a node on PostgreSQL 17, from `JournalInspectTests` (one row per line in the
output; wrapped here):

```text
ordering=1 | persistence_id=charge-point-OHM-ITG-7273 | sequence_nr=1
  | created=2026-11-05T22:45:27.9912543+00:00 | serializer_id=9100>9101
  | manifest=ohmline.trace-envelope.v1>charging.charge-point-registered.v1
  | traceparent=00-527350d0b677dc71f927d5de39b4e379-7ea6fe651c8f7e18-03
  | payload={"siteId":"slc-depot","connectorMaxPowers":[22,22],"chargePointId":"OHM-ITG-7273","commandId":"01a1ade3-7583-7c8a-b309-1019c95ed174","occurredAt":"2026-11-05T22:45:27.9828046+00:00"}
ordering=2 | persistence_id=charge-point-OHM-ITG-7273 | sequence_nr=2
  | created=2026-11-05T22:45:28.1058167+00:00 | serializer_id=9100>9101
  | manifest=ohmline.trace-envelope.v1>charging.connector-reserved.v1
  | traceparent=00-918301407c65dd68d747773118addc20-cd79cfc52ab7906c-03
  | payload={"tokenUid":"04A2B3C4D5E6F7","expiresAt":"2026-11-05T23:00:28.1046442+00:00","connectorNumber":1,"chargePointId":"OHM-ITG-7273","commandId":"01a1ade3-77a7-76ef-8f30-520de89ff76d","occurredAt":"2026-11-05T22:45:28.1046442+00:00"}
```

## The permanent list

Each of these is written into stored data or into another node's or another team's code. Changing
one is a data migration, never a refactoring.

| What | Ohmline's values | Kept in | Breaks if changed |
|---|---|---|---|
| Serializer ids | 9100 envelope, 9101 Charging, 9102 Site Energy, 9103 Driver Accounts, 9104 Tariffs; 9105 to 9199 reserved | each serializer's `Identifier`; ADR 0007 | every row it wrote becomes unreadable |
| Manifests | `{context}.{kebab-type-name}.v{N}`; snapshots `{context}.{aggregate}-snapshot.v{N}`; `ohmline.trace-envelope.v1` | each serializer's registry | every row with that manifest becomes a poison event (section 11.3) |
| Persistence id format | `{shard type name}-{entity id}`; the singleton `tariff-book` | `PersistenceIdOf` on each actor | the aggregate starts empty, its history orphaned |
| Shard type names | `charge-point`, `charging-session`, `charge-point-listing`, `charging-journey`, `site-power-budget`, `id-token` | each actor's `TypeName` | messages go to a region nobody hosts, and the persistence ids change with it |
| Number of shards | `100` for every region | `EntityMessageExtractor.NumberOfShards` | during a rolling update one entity can live in two shards on two nodes |
| Event tags | `charging`, `charging-session`, `charging-journey`, `site-energy` | the taggers | a projection on the new tag misses every older event |
| Projection ids and the daemon's stream order | `session-history-v1`, ... (ADR 0009) | each projection's `Id`; `ChargingReadModelsExtensions.Streams` | a renamed projection starts again from offset 0; a reordered list runs one stream twice |
| Published Language | `ohmline.{context}.{kebab-type-name}.v{N}` and the outbox positions | `ChargingIntegrationEvent`'s `[JsonDerivedType]`; `read_models.integration_outbox` | Billing skips or misreads what it is billed by |
| Client contracts | `/app/v1`, the DTOs' JSON names, `RejectionCodes` | `Ohmline.Charging.Client.Contracts` | driver apps already installed on phones stop understanding the server |

The full list of durable names, with telemetry and pub-sub topics, is ADR 0009.

## What is free to change

- The names, namespaces and assemblies of C# types, as long as each serializer's registry keeps
  their manifests (section 20.7 renames a concept this way).
- The state unions and everything in memory: a snapshot is a DTO with a manifest of its own, so
  the state can change shape without a stored snapshot noticing (section 20.6).
- A read model's tables, since they can be rebuilt from the journal; a new shape gets a new
  projection id and a new table beside the old one (section 20.9).
- How a decision is made. The journal records what was decided, never the rule that decided it.
