# Runbook: backups, point-in-time recovery and restore drills

What to back up, how to restore it, and how to prove a restore works before it is needed
(section 20.10). The commands below are PostgreSQL's own, for the PostgreSQL 17 that the AppHost
and production run; none of them is run by the sample's tests, so treat them as the starting
point of a drill, not as a tested script.

## What there is to lose

| Database | What it holds | If it is lost |
|---|---|---|
| `ohmline`, schema `public` | The journal, snapshots and tags: **the system of record** | Everything Ohmline knows about charge points, sessions, journeys, budgets, tokens and tariffs |
| `ohmline`, schema `read_models` | Read models, projection offsets, the integration outbox | Rebuildable from the journal (section 20.9), except the outbox's **positions**, which consumers have stored |
| `billing`, schema `billing` | Holds, invoices, the inbox position, the outbox of receipts | Billing's own truth: state stored as rows, not events (section 16.3) |
| `schemaversions` in each | Which migrations ran | The migrator would run every script again; they are idempotent |

Snapshots are a cache (section 20.6) and read models can be rebuilt, but restoring them with the
journal saves hours of replay, so they are backed up together: one PostgreSQL cluster, one backup.

## Backups

- **Continuous archiving for point-in-time recovery**: a base backup (`pg_basebackup`) plus the
  archived write-ahead log (`archive_mode = on`, `archive_command` or `pg_receivewal`). An
  append-only journal suits it: the WAL is mostly inserts. Recovery can stop at any moment
  before an incident (`recovery_target_time`).
- **A logical dump** (`pg_dump --format=custom`) of each database, daily, kept apart: portable
  across versions, and the input of the restore drill.
- **Retention of backups** follows `retention.md`: a backup is a copy of every event, so purged
  events stay in backups until those backups expire.

## Restore

1. **Stop every node, Billing and the gateway.** A node running against a database being
   restored would write events that the restore then removes.
2. **Restore** the base backup and replay WAL to the chosen point, or restore the dumps.
3. **Run the migrator** (`src/Ohmline.Migrator`). It applies only what the restored
   `schemaversions` lacks, and grants the roles again.
4. **Check the journal** with `tools/Ohmline.JournalInspect` (section 20.1): the newest rows,
   by `--after`, are from before the restore point.
5. **Move the published positions past what consumers have already read** (see below).
6. **Start the cluster.** Projections resume from their restored offsets; aggregates recover
   from the restored journal on their next command.

## Side effects outside the system

A restore to an earlier point takes Ohmline back in time, and the world outside does not go
with it. Everything that happened after the restore point and left the system is still out there:

| What left the system | Where it is now | What to do |
|---|---|---|
| Payments: holds placed, captured or voided at TallyPay (section 16.4) | At TallyPay, under Ohmline's idempotency keys `{journeyId}-{step}` | A journey that runs again sends the same keys, and TallyPay answers with what it already did. Reconcile TallyPay's report for the lost window against Billing |
| Integration events Billing already read from the feed | In Billing's `inbox_position` and invoices | After restoring only `ohmline`, the outbox's sequence would number new events with positions Billing has passed. Before starting, move it past the highest position any consumer stored: `SELECT setval('read_models.integration_outbox_position_seq', <highest seen>)` |
| Driver app feed positions stored on phones (section 19.7) | In each app's `feed_position` | The same for `read_models.driver_app_feed_position_seq`, or the apps miss new cards until they reset |
| Commands chargers carried out: remote starts and stops, charging profiles (section 16.7) | In the chargers | Chargers report their state again when they reconnect, and the gateway turns that into commands, as after any outage |
| Receipts sent to drivers | In drivers' mailboxes | Billing's outbox marks a receipt sent; a receipt sent after the restore point may be sent again. Say so in the incident notice |

Restoring `ohmline` and `billing` to the same point keeps the feed and Billing's inbox consistent
with each other; it does not undo what TallyPay did.

## The restore drill

Run it on a schedule, and before any retention purge:

1. Restore last night's dumps into a scratch PostgreSQL (a container is enough).
2. Run the migrator against it.
3. Start one node against it with `ConnectionStrings:ohmline` pointing at the scratch database,
   on a port of its own, with no gateway and no Billing.
4. Ask a few known aggregates for their status (`GET /charge-points/{id}`, `GET /sessions/{id}`)
   and compare them with production's answers from before the dump.
5. Rebuild one read model beside the restored one, as section 20.9 does with a new projection
   id, and compare row counts and a checksum of the columns that matter.
6. Record how long each step took: that is the recovery time to plan for.
