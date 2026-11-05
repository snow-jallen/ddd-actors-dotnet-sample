# Runbook: a stuck projection

A read model that stops moving looks healthy: its rows are all there, just old. Nothing fails
for the people reading it; they see yesterday. This runbook says how to see that a projection
is stuck, why, and how to get it moving again without losing an event (section 13.7).

## Symptoms

- Screens fed by one read model stop changing while commands still succeed.
- `GET /projections/status` shows a stream whose `eventsBehind` grows instead of returning to
  0, and whose `updatedAt` stays put.
- The node logs, for that stream, one of these ERROR lines (one line each in the log;
  wrapped here; `ProjectionFailureTests` produces lines of exactly this shape):

  ```text
  Projection session-history-v1/charging-session failed on charging-session-... #4 at offset
  1234 (2 of 5 retries); it restarts from its offset
  Projection session-history-v1/charging-session stopped at charging-session-... #4, offset
  1234, after 5 retries. Its read model stays as it is until the projection is fixed and the
  node restarted
  ```

## What each policy does

| Policy | The read model | What you see |
|---|---|---|
| `StopAfterRetries` (session history) | Correct up to the event before the bad one, then frozen | `eventsBehind` grows; the "stopped at" line names the event |
| `RecordAndSkip` (from section 13.8, the site live board) | Moves on without the bad event | `failures` above 0; a row in `read_models.projection_failures` with the error |

A database that cannot be reached is not a stuck projection: the stream backs off (up to 30 s
between attempts) and resumes by itself, and `eventsBehind` falls once it is back.

## Confirm

The event the stream stopped at is the first one after its stored offset:

```sql
SELECT t.ordering_id, t.persistence_id, t.sequence_nr
FROM tags t
JOIN read_models.projection_offsets o ON o.tag = t.tag
WHERE o.projection_id = 'session-history-v1' AND t.ordering_id > o.last_offset
ORDER BY t.ordering_id
LIMIT 1;
```

For a skipping projection, the skipped events and their errors:

```sql
SELECT persistence_id, sequence_nr, failed_at, error
FROM read_models.projection_failures
WHERE projection_id = 'site-live-board-v1'
ORDER BY failed_at;
```

## Fix

1. Read the error. A handler that throws on a legal event is a bug in the projection: fix it,
   with a test that delivers that event (the pure `Apply` tests are the quickest place).
2. Deploy the fix. A stopped stream starts again when its daemon does: with the node, or when
   its daemon moves to another node.
3. Watch `eventsBehind` fall to 0. Nothing was lost: the stream resumes after its stored
   offset, and the journal still holds every event.
4. For skipped events, the read model is missing their effect. Rebuild it under a new
   projection id from offset 0 (section 14.8), then delete the rows of
   `read_models.projection_failures` you have dealt with.

Never move a stored offset forward by hand to get past an event: that is `RecordAndSkip`
without the record.
