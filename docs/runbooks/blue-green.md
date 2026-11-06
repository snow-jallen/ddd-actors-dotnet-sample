# Runbook: a release that cannot roll (blue/green)

Most releases roll (`docs/runbooks/rolling-update.md`). A release whose nodes cannot talk to the
running ones is deployed blue/green instead: a MAJOR version of the services (ADR 0016), such as
a change of Akka.NET's remoting transport or an upgrade with a wire format the old nodes cannot
read (section 21.10). This runbook is the only way to do it, because the obvious way, running
both clusters and moving traffic, puts two writers on one journal (ADR 0017, section 21.8).

Blue is the cluster running now; green is the new release.

## Before the window

- Announce a write outage of a few minutes. Sessions in progress keep charging; a charger that
  keeps its transaction messages while offline, as OCPP chargers are expected to, sends them
  when it reconnects. Drivers cannot start sessions in the app meanwhile.
- The migrator of green's release has run, and its migrations are additive: blue still runs on
  the same schema (section 20.8).
- Green's overlay uses its own discovery label, `akka-cluster: ohmline-green`, and its own
  `Ohmline__Kubernetes__ServiceName=ohmline-green`, so the two clusters never find each other.
- Read models that green changes are already built side by side (section 20.9).
- Rollback is decided in advance: if green is not healthy within 15 minutes of step 4, go back
  to blue (below).

## The cut-over

1. **Apply green at zero.** Apply green's overlay with its node and gateway deployments at
   `replicas: 0`, and pull its images onto the nodes ahead of time if the cluster allows it, so
   that step 4 is quick. Nothing of green runs yet.
2. **Stop new work on blue.** Point the ingress for `/app/v1`, the operator API and the chargers'
   WebSocket endpoint at a maintenance page or a 503 with `Retry-After`.
3. **Drain and stop blue.** Scale blue's gateway, then its nodes, to zero. Coordinated shutdown
   hands off every shard and leaves the cluster (section 21.7). Wait until no blue pod is left:

   ```bash
   kubectl -n ohmline get pods -l akka-cluster=ohmline
   ```

   Nothing must remain. A blue node still running is a second writer the moment green starts.
4. **Start green.** Scale green's nodes, then its gateway, to their size. They form a new
   cluster through bootstrap (section 21.6); aggregates recover from the journal on their first
   command.
5. **Move the traffic** to green's services. Chargers reconnect to green's gateway with the
   back-off they already use.
6. **Watch** (section 18.7): command latency, recovery times, dead letters, and the journal
   errors below. Keep blue's manifests, scaled to zero, until the next release.

## What two writers look like

If, despite step 3, both clusters ever hosted the same aggregate, the journal refuses the
second writer's events and that actor stops, logging at ERROR:

```text
Failed to persist event type [...] with sequence number [N] for persistenceId [charge-point-...]
Cause: ... duplicate key value violates unique constraint "journal_uq"
```

(`TwoIncarnationsTests`, section 10.9, shows the same error on SQLite.) Stop the cluster that
should not be running at once; then check the affected aggregates' histories with
`tools/Ohmline.JournalInspect` (section 20.1), and treat any command accepted by the stopped
cluster after the cut-over as lost: its sender was answered, but its event was never stored.

## Roll back

Before step 4, rolling back is starting blue again: scale blue up, move traffic back, delete
green. After step 4, green has written events blue may not be able to read (that is often why
the release could not roll). Rolling back then needs the same cut-over in reverse and a blue
that reads green's events; if it cannot, roll forward with a fix.
