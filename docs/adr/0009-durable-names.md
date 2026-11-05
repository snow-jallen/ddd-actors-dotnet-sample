# 0009. Shard type names, persistence ids and the shard count are durable names

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch12-s02

## Context

From section 12.2, Ohmline's aggregates live in shard regions: a region per aggregate type, on
every node with the `charging` role, which starts the one entity actor for an id on the first
message addressed to it, wherever in the cluster its shard is. The region is the repository: code
that wants charge point `OHM-SLC-0042` sends a command that names it, and never holds an actor
reference of its own.

Four strings and one number make that work, and each of them ends up somewhere that outlives a
deployment:

- **The shard type name** (`charge-point`) names the region. Every node, including the gateway's
  proxies from Chapter 16, must use the same one, or its messages go to a region nobody hosts.
- **The persistence id** (`charge-point-OHM-SLC-0042`) is the key of every journal row and
  snapshot the entity writes. The entity recovers what is stored under it, and nothing else.
- **The entity id** (`OHM-SLC-0042`) is what the message extractor reads from a command. Its
  written form is part of the persistence id.
- **The number of shards** decides which shard an id belongs to: the shard is a hash of the
  entity id modulo this number. During a rolling update, two nodes that disagree on it can put
  the same entity in two shards, on two nodes, at once.
- **The coordinator's state store mode** cannot change without stopping the whole cluster.

Section 10.3 made the same argument for serializer ids and manifests (ADR 0007).

## Decision

We will treat these as permanent, never to be changed or reused once released:

| Name | Value |
|---|---|
| ActorSystem name | `ohmline`, on every node and in every test that joins a cluster |
| Cluster roles | `charging` (the node that hosts aggregates), `gateway` (Chapter 16) |
| Shard type names | `charge-point`, `charging-session`, `charge-point-listing`, `site-power-budget`; from section 12.7 `id-token`, from section 15.3 `charging-journey` |
| Singleton names | `tariff-book` (section 12.8) |
| Persistence id | `{shard type name}-{entity id}`, such as `charge-point-OHM-SLC-0042`, `charging-session-019a4f6e-...`, `site-power-budget-slc-depot` |
| Entity ids | the identifier's canonical written form: `ChargePointId` (`OHM-SLC-0042`), `SiteId` (`slc-depot`), `SessionId` (a lower-case Guid with hyphens) |
| Number of shards | `100` for every region (`EntityMessageExtractor.NumberOfShards`), about ten times the most nodes Ohmline will run |
| Shard of an entity | `HashCodeMessageExtractor`'s hash of the entity id, modulo 100 |
| Coordinator state | `StateStoreMode.DData`, set explicitly in `EntityShardOptions` |
| Event tags (section 13.2) | `charging`, `charging-session`, `site-energy` (`ChargingTags`, `SiteEnergyTags`); a tag is stored with the event and never rewritten |
| Sharded daemon process (section 13.6) | `projections`, one instance per projection stream; instance `i` runs `ChargingReadModelsExtensions.Streams[i]`, so the list is append only |
| Projection ids (sections 13.5, 13.8) | `session-history-v1`, `site-live-board-v1`; a new read-model shape is a new id, never a reused one |
| DistributedPubSub topics (section 14.4) | `session-summaries` (`LiveTopics`); a subscriber to a renamed topic hears nothing, and nothing says so |
| SignalR (section 14.5) | hub path `/hubs/charging`; groups `session-{sessionId}` and `token-{tokenUid}`; hub methods `JoinSession`, `JoinToken`; client method `sessionSummaryChanged` (`ChargingHub`). Released apps call them by name |

We will keep each name in one place: the type name and the persistence id format in the
entity's actor (`ChargePointActor.TypeName`, `PersistenceIdOf`), the shard count in
`EntityMessageExtractor`, the defaults in `EntityShardOptions`, and the roles in `ClusterRoles`.
Each context routes its own message families with an `EntityMessageExtractor` whose function
reads the id a command already carries; the contracts stay plain records, with no interface for
routing.

## Consequences

- Renaming a shard type name or a persistence id format is a data migration, not a refactoring:
  every existing entity would start empty under its new name. If it must ever happen, it is an
  explicit journal migration (Chapter 20), never a code change alone.
- Ohmline can grow to about a thousand nodes before 100 shards spread unevenly; beyond that,
  changing the count means stopping the whole cluster, and it is a decision of its own.
- A region on a node without the `charging` role hosts nothing; such a node reaches the entities
  through a proxy with the same type name and extractor.
- `EntityMessageExtractorTests` pins the shard of a few ids (`OHM-SLC-0042` is in shard 67), so a
  change of hash fails a test before it moves entities. `ChargingMessageExtractorsTests` checks
  that every command and query reaches exactly one region. `ShardRegionTests` checks the
  persistence ids in a real SQLite journal, and each actor's tests check `PersistenceIdOf`.
