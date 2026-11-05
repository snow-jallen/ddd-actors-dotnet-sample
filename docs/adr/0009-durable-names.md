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
| Singleton names | `tariff-book` (section 12.8); `grid-signal-poller` (section 16.8) |
| Persistence id | `{shard type name}-{entity id}`, such as `charge-point-OHM-SLC-0042`, `charging-session-019a4f6e-...`, `site-power-budget-slc-depot` |
| Entity ids | the identifier's canonical written form: `ChargePointId` (`OHM-SLC-0042`), `SiteId` (`slc-depot`), `SessionId` (a lower-case Guid with hyphens) |
| Number of shards | `100` for every region (`EntityMessageExtractor.NumberOfShards`), about ten times the most nodes Ohmline will run |
| Shard of an entity | `HashCodeMessageExtractor`'s hash of the entity id, modulo 100 |
| Coordinator state | `StateStoreMode.DData`, set explicitly in `EntityShardOptions` |
| Event tags (section 13.2) | `charging`, `charging-session`, `site-energy` (`ChargingTags`, `SiteEnergyTags`); from section 15.3 `charging-journey`; a tag is stored with the event and never rewritten |
| Sharded daemon process (section 13.6) | `projections`, one instance per projection stream; instance `i` runs `ChargingReadModelsExtensions.Streams[i]`, so the list is append only |
| Projection ids (sections 13.5, 13.8, 14.6, 15.1, 15.4, 16.1, 16.8) | `session-history-v1`, `site-live-board-v1`, `driver-app-v1`; the subscribers `session-ended-subscriber-v1` and `journey-session-relay-v1`, and the outbox relay `integration-relay-v1` and the `charging-profile-dispatcher-v1` (section 16.8), which keep offsets like any projection; a new read-model shape is a new id, never a reused one |
| Published Language (section 16.1) | wire names `ohmline.{context}.{kebab-type-name}.v{N}`, such as `ohmline.charging.session-completed.v1`, in `[JsonDerivedType]`; the outbox table `read_models.integration_outbox` and its positions, which consumers store |
| Idempotency keys (section 15.6) | `{journeyId}-{step}`, with the steps `lock`, `hold`, `allocation`, `charger`, `release`, `capture`, `unlock` (`IdempotencyKey`); the command id derived from a key (`GuidV7.Derive`) is how Driver Accounts and Site Energy recognise a request sent again, so neither the format nor the derivation may change while a journey is in flight |
| DistributedPubSub topics (sections 14.4, 14.6, 16.7) | `session-summaries`, `driver-app-cards` (`LiveTopics`); one per charge point, `charger-commands-{chargePointId}` (`ChargerTopics`), which the gateway node holding the charger's WebSocket subscribes to; a subscriber to a renamed topic hears nothing, and nothing says so. The mediator runs on every node, whatever its role |
| SignalR (section 14.5) | hub path `/hubs/charging`; groups `session-{sessionId}` and `token-{tokenUid}`; hub methods `JoinSession`, `JoinToken`; client methods `sessionSummaryChanged`, `driverCardChanged` from section 14.6 (`ChargingHub`). Released apps call them by name |
| Driver app API (section 14.6) | route prefix `/app/v1` (`DriverAppEndpoints`); a breaking change is `/app/v2` beside it, never a change to `v1` |
| Driver app on the phone (sections 19.6 to 19.8) | the SQLite file `ohmline-driver.db3` and its tables `outbox`, `session_card` and `feed_position`, which an app upgrade must still read (the outbox's queued commands must be sent); the outbox's kinds (`start-charging`, `stop-charging`, `reserve-connector`); the ActivitySource `Ohmline.Mobile`, its span names (`tap {kind}`, `send {kind}`, `apply {method}`) and attributes (`ohmline.command.id`, `ohmline.command.kind`, `ohmline.command.attempt`) |
| Client contracts (section 19.2) | the JSON names of every DTO in `Ohmline.Charging.Client.Contracts`, and the `RejectionCodes` values, which are only ever added to (`src/Charging/Ohmline.Charging.Client.Contracts/README.md`) |
| Telemetry (Chapter 17) | ActivitySource and Meter names `Ohmline.{Context}` (`Ohmline.Charging`, `Ohmline.SiteEnergy`, `Ohmline.DriverAccounts`, `Ohmline.Tariffs`, `Ohmline.DeviceGateway`, `Ohmline.Billing`, `Ohmline.ActorShell`), which dashboards and the hosts' `Ohmline.*` find them by; the trace envelope's serializer `9100` and manifest `ohmline.trace-envelope.v1`, which stored events carry from section 17.7, and its journal adapter `trace-envelope-tagger`; span names such as `process {ActorType}` and attribute names such as `messaging.message.conversation_id`, which saved queries and alerts use |
| Metrics (Chapter 18) | Instrument names `ohmline.{context}.{name}` and their units (`ohmline.charging.commands`, `.commands.rejected`, `.sessions.started`, `.energy.delivered` in `kWh`, `.journey.time_to_energy_flow` in `s`; `ohmline.site_energy.allocation.utilization`; the shell's `ohmline.akka.*`), tag names (`aggregate.type`, `command.type`, `reason`, `outcome`, `actor.type`, `message.type`, `sharding.type`) and the `outcome` value `energy-flowing`: Prometheus series, the dashboard and the SLO rules in `deploy/observability` are written against them; renaming one silently empties a panel or an alert. Health tags `live` and `ready` and the paths `/health/live` and `/health/ready`, which orchestrators probe |

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
