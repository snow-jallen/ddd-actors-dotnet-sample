# Runbook: a rolling update of the cluster

A rolling update replaces the node and gateway pods one at a time while the cluster keeps
serving. For a few minutes two releases run side by side and talk to each other. This runbook
says what happens in those minutes, what to watch, and when to stop (section 21.7). It assumes
the topology of `deploy/k8s` (section 21.6) and a release that can roll: a MINOR or PATCH in the
sense of ADR 0016. A release that cannot roll is deployed blue/green instead (section 21.8).

## Before you start

- The release's images exist with its exact version tag, and the overlay names that tag, never
  `latest` (section 21.5).
- Every new event, message or field is readable by the release now running: the release that
  starts *writing* a shape comes after the one that can *read* it (ADR 0015). If this release
  both reads and writes something new, stop: split it into two releases.
- The migrator job of the release has completed (`kubectl -n ohmline get job migrator`).
- The dashboards of section 18.7 are open: cluster members, shards per node, command latency,
  dead letters, projection lag, gateway connections.

## What happens, pod by pod

`maxSurge: 1` and `maxUnavailable: 0`: Kubernetes first starts one pod of the new release, waits
until it is ready, and only then stops one old pod. Then the next.

1. **The new pod joins.** Cluster bootstrap finds the running cluster through the Kubernetes API
   and joins it; the pod becomes ready once it is an Up member and reaches the journal
   (`/health/ready`). Its app version is the new release's `MAJOR.MINOR.PATCH`, from MinVer
   (section 21.2).
2. **New shards go to the new version.** Sharding's allocation strategy prefers, among the nodes
   that are not leaving, those with the *highest* app version. Every shard that starts from now
   on, an aggregate that was asleep and is woken by a command, starts on a new node.
3. **Rebalancing pauses.** While the regions report different app versions, the coordinator does
   not rebalance at all: moving entities to a node that is about to be replaced would only move
   them twice. The cluster looks lopsided during the update; that is expected.
4. **An old pod stops.** Kubernetes sends SIGTERM and removes the pod from its services. The
   host's shutdown (30 s, `ShutdownBudget.HostShutdownTimeout`) runs coordinated shutdown inside
   the pod's 35 s grace period:

   | Phase | Timeout | What happens |
   |---|---|---|
   | `service-unbind` | 10 s | The gateway closes its chargers' connections in batches over 8 s, each with a reconnect hint |
   | `cluster-sharding-shutdown-region` | 6 s | The node hands its shards off; their entities stop, and messages for them are buffered by the coordinator |
   | `cluster-leave`, `cluster-exiting` | 2 s each | The node leaves; the others see it as Removed, not Unreachable, so no one is downed |
   | the other nine phases | 14 s in all | Usually instant |

   The handed-off shards start again, on the node with the highest app version, on the next
   message for them. Their entities recover from the journal (and snapshot) there.
5. **The last old pod stops.** All regions report the same app version, rebalancing resumes,
   and shards spread out evenly again.

## Messages between releases

During the update an old node may receive a message that only the new release knows: a new
command type or a new manifest version, sent by a new node through a shard region.

- Its serializer does not know the manifest and throws `SerializationException`
  (`UnknownManifestException`). Akka.Remote logs it, **drops that message**, and keeps the
  connection; any other exception would close the association between the two nodes.
- The sender's `Ask` times out. The edge answers 504; a client retries with the **same command
  id**, and the aggregate, if it already took the command, answers it as a duplicate
  (section 12.6). Nothing is decided twice.
- Seeing a few of these during an update is normal when a release adds a message. Seeing them
  for a message that already existed in the old release is not: stop.

Events are not affected the same way: a node reads the journal only for the shards it hosts,
and new shards start on new nodes. A handed-off shard can move back to an old node only if an
old node is the newest left, which the rollout order prevents.

## Draining the gateway without a storm

A gateway pod holds the WebSockets of hundreds of chargers. Cut them all at once and they all
reconnect to the remaining gateways within the same second, each with a boot notification, a
status notification per connector and, for those charging, a session lookup: a reconnection
storm that a healthy cluster can turn into timeouts.

The gateway prevents it twice:

- **The gateway spreads the closes.** `ConnectionDrain` closes the connections in shuffled
  batches of 25 (`Ohmline:Gateway:DrainBatchSize`), each batch at a jittered moment of its own
  slot of the 8 s window (`DrainWindow`). It starts as soon as the host begins to stop, before
  the web server waits for open requests; a new charger that connects meanwhile gets 503.
- **The charger spreads its return.** Each close frame has the status 1012 (Service Restart)
  and the reason `retry-after=N`, a wait of its own between 5 and 30 s (`ReconnectAfterMin`,
  `ReconnectAfterMax`). A charger that ignores it still sees a clean close.

To see the difference, run the simulator's storm scenario against a local cluster and restart a
gateway:

```bash
dotnet run --project tools/Ohmline.Simulator -- \
  --chargers 50 --scenario reconnect-storm --duration 120
```

## Watch

| Signal | Healthy during the update | Stop when |
|---|---|---|
| Cluster members | One more than usual, briefly; no Unreachable | A member stays Unreachable for more than 20 s |
| Pod readiness | The new pod ready within a minute | The new pod not ready after 3 minutes |
| Command latency (p99) | Up for a few seconds per stopped node, during the hand-off | Above 2 s for more than a minute |
| `SerializationException` in the logs | Only for messages this release adds | For any message the old release already had |
| Dead letters | A burst per stopped node | Growing after the last pod is replaced |
| Gateway connections | Dip and recover per gateway pod, spread over half a minute | Not back to the count before the update within 2 minutes |
| Projection lag | Unchanged | Growing |

## Roll back

A rollback is another rolling update, to the previous image tag:

```bash
kubectl -n ohmline rollout undo deployment/node
kubectl -n ohmline rollout undo deployment/gateway
```

It is safe as long as the new release has not written anything the old one cannot read. Under
ADR 0015 it has not: a release that writes a new shape only follows one that already reads it.
If that rule was broken, rolling back makes the old nodes fail to recover the affected
aggregates (`docs/runbooks/poison-event.md`); roll forward with a fix instead.
