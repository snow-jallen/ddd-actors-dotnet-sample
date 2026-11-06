# Upgrading Akka.NET from 1.5.73 to 1.6.0-beta5

*Section 21.10. This document lives on the branch `upgrade/akka-1.6`, at tag `ch21-s10`. The
book's `main` stays on 1.5.73 until 1.6 is released.*

The upgrade is one line, and that line is not the work. The work is making sure the line changes
nothing else: not what is in the journal, not what goes over the wire between a 1.5 node and a
1.6 node during the rolling upgrade, and not how the cluster places shards.

## The line

```xml
<AkkaVersion>1.6.0-beta5</AkkaVersion>
```

Every `Akka.*` package in `Directory.Packages.props` takes its version from this property, so the
bump moves all of them together, including Akka.Hosting, which ships from the main repository in
lock-step since 1.5.72. 1.6 targets `net10.0` only; a `net11.0` project loads its `net10.0`
assets. The lock files of every project that depends on Akka.NET change with it, and the diff of
`packages.lock.json` is where the reviewer sees exactly which packages moved.

With nothing else changed, the whole solution builds without a warning and every test passes on
1.6.0-beta5. That is necessary, and not enough: tests run one version at a time, and a rolling
upgrade runs two.

## What 1.6 changes by itself, and what Ohmline does about it

| Change in 1.6 | Risk during a rolling upgrade or a rollback | What Ohmline does |
|---|---|---|
| Akka.NET's own serializers are registered in code, not in HOCON (#8702) | Bindings that a 1.5 process does not have: rows or messages a 1.5 node cannot read after a rollback | `UpgradePins` binds strings, integers, actor references and status replies to the serializers 1.5 used (`primitive`, 17; `akka-misc`, 16). `UpgradePinTests` passes on both versions |
| `rebalance-absolute-limit` defaults to 20 instead of 0 (#8445) | The coordinator runs on the oldest node, so the rebalance algorithm would change mid-upgrade | `UpgradePins` keeps 0 (and the relative limit 0.1). Moving to 20 is a later release of its own |
| System UIDs widen to 64 bits (#8317) | None while `akka.remote.use-64bit-system-uids` is off; on, 1.5 nodes cannot read the handshake | Left off. Turn it on only after every node runs 1.6 |
| Artery TCP remoting, `akka://` on port 25520 (experimental) | Not wire-compatible with classic remoting: a 1.6 node on Artery cannot talk to a 1.5 node | Not used. Switching transports is a blue/green release (ADR 0017), never a rolling one |
| `TestKit` implements `IAsyncLifetime` with virtual methods (#8545) | Test classes that declare `InitializeAsync`/`DisposeAsync` must mark them `override` | Nothing to change: backported to 1.5.72, and no TestKit subclass in the sample declares them |
| Disposing the shared materializer no longer stops it (#8886) | Code that relied on the 1.5 behaviour | Nothing to change: `ProjectionDaemon` never disposes `Materializer()` |
| `Cluster.Get()` no longer waits for the cluster to start (#8359) | Code that reads cluster state right after `Get` | Nothing to change: the one call, in `WithOhmlineCluster`, only joins the node's own address |

Third-party plugins built against 1.5 may hit a binary break on 1.6. Petabridge.Cmd 1.5.2 runs in
the sample's tests on 1.6.0-beta5 (`OperatorCommandsTests`), but production waits for its 1.6
build.

## The serializers Ohmline owns are not part of the upgrade

Every event, snapshot, command and reply goes through Ohmline's own serializers (9100 to 9104),
with manifests that do not change (ADR 0007). The upgrade changes none of them, and the golden
corpus (section 20.5) proves it on every build: the same bytes are read by both versions.

## Akka.Serialization.V2 and union fields

1.6 adds a source-generated MessagePack serializer, `Akka.Serialization.V2`. It is the first
Akka.NET serializer that round-trips a C# 15 union as a field: it writes the case the union
holds, with that case's manifest, and reads it back into the union. The learning tests in
`learn/Ch21.SerializationV2.Tests` show it on `ChargingAuthorized(string SessionId,
Authorization By)`, where `Authorization` is `union(RfidCard, AppToken)`.

Ohmline does not move its journal to it. Changing a persisted type's serializer is a new
serializer id for new rows and the old one kept for reading old rows forever (ADR 0007), and its
API was still changing between betas. Its place is new message types, once 1.6 is released.

## The rollout

1. **Release N (this branch, merged when 1.6 is released):** the version bump with
   `UpgradePins`. It rolls like any MINOR release (`docs/runbooks/rolling-update.md`): the wire
   format and the journal are the same on both versions, so 1.5 and 1.6 nodes run side by side
   for the length of the update. Rolling back is rolling forward to the previous image.
2. **Release N+1, once no 1.5 node is left anywhere, including a rollback target:** decide on
   the 1.6 defaults one at a time, each in a release of its own: `rebalance-absolute-limit`, then
   64-bit system UIDs.
3. **A transport change (Artery) is never part of this.** It is a MAJOR release, deployed
   blue/green (`docs/runbooks/blue-green.md`).
