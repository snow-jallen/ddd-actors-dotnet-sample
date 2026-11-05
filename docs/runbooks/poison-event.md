# Runbook: a poison event

An aggregate whose journal holds an event it cannot read or apply never finishes recovery. Its
actor stops, the next message starts it again, and it fails the same way: every command for that
one charge point, session or site times out, while every other aggregate is fine. This runbook
says how to recognise it and how to fix it without losing history (section 11.3).

## Symptoms

- One aggregate's callers time out on every request; its neighbours answer.
- The node logs this ERROR line from `DeciderActor.OnRecoveryFailure`, again on every message
  (and, under a backoff supervisor or sharding, again on every restart):

  ```text
  Recovery of charge-point-OHM-SLC-0042 failed after sequence number 0, on an event with the
  unknown manifest 'charging.connector-teleported.v1'. The actor stops; see
  docs/runbooks/poison-event.md
  ```

  (One line in the log; wrapped here. `PoisonEventTests` produces exactly this line.)
- Each failure is followed by dead letters: the messages that waited for the replay.

The log line has up to three facts:

| Part | Meaning |
|---|---|
| `charge-point-OHM-SLC-0042` | The persistence id: which aggregate |
| `after sequence number 0` | The last event that was applied. The bad one comes **after** it, not necessarily right after it: Akka.Persistence.Sql reads up to `replay-batch-size` (1,000) rows at a time, and one unreadable row fails its whole batch |
| `unknown manifest '...'` | Present when the serializer did not know the manifest. Without it, the exception logged with the line says what went wrong instead |
| `while applying ...` | Present when the event was read but `Evolve` threw. That is a bug in `Evolve`, which must be total (section 7.5) |

## Confirm

Find the row. On SQLite (Development) or Postgres:

```sql
SELECT sequence_number, manifest, identifier, created
FROM journal
WHERE persistence_id = 'charge-point-OHM-SLC-0042'
ORDER BY sequence_number;
```

Compare each `manifest` with the serializer's registry for its `identifier` (9101 is
`ChargingSerializer.Manifests`, 9102 `SiteEnergySerializer.Manifests`; ADR 0007). The row whose
manifest is not in the registry, or whose payload no longer fits its type, is the poison.

## Causes

| Cause | Typical story |
|---|---|
| A manifest this version does not know | A newer version wrote it and was rolled back; or a type and its manifest were removed while old events still use them |
| A payload the type no longer accepts | A required field was added to an event without a default, or a value object's rules were tightened (`JsonException: Not a valid ...`) |
| `Evolve` throws | A new event meets a state that `Evolve` does not handle |
| The journal itself is down | Not a poison: **every** aggregate fails, and the log shows a connection error. Fix the database; the actors recover on their next message |

## Fix

Fix the code, deploy it, and let the next message recover the aggregate. In order of preference:

1. **Roll forward** to a version whose serializer knows the manifest (the one that wrote it).
2. **Teach the serializer the old shape:** add the manifest back to the registry, mapped to a
   type that can read it, or add an upcaster that turns the old payload into the current event
   (Chapter 20). Add a golden file for the manifest, so it can never be lost again.
3. **Make `Evolve` total** if it threw: every event must meet every state and give a state.

Then send the aggregate a harmless query (its status) and check the log for its INFO recovery
line.

## Never

- **Never edit, re-serialize or delete the row.** The journal is the system of record: other
  readers (projections, Chapter 13) have already read it, its `ordering` is someone's offset, and
  a deleted row leaves a gap in the sequence numbers that nothing explains. An edit by hand is a
  change to history that no event records.
- **Never insert a "fixing" event by hand.** If the domain needs a correction, a command makes
  it, through the decider, after the aggregate recovers.
- **`snapshot-is-optional` does not help with an unreadable event.** It is on from section 20.6,
  and it only skips an unreadable snapshot.

An unreadable **snapshot** needs no one at all: a snapshot is a cache, and since section 20.6 the
store skips it (`Snapshot load error for persistenceId [...]. Replaying all events since
snapshot-is-optional=true`, at INFO), the aggregate replays its events from the first, and saves
a snapshot it can read once it has replayed `SnapshotEvery` events or more. That is not true for
an aggregate whose older events were deleted after its snapshot was taken (state sourcing,
section 11.7): there the snapshot is the only copy, the shell stops the aggregate with
`... cannot recover: no snapshot was read, and its events start at sequence number N, not 1`,
and the serializer must be fixed to read the snapshot.
