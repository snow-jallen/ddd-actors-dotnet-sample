# Runbook: operator commands with pbm

Every node and the Device Gateway host Petabridge.Cmd 1.5.2 with the cluster and
cluster-sharding palettes (section 18.8). The host listens on **127.0.0.1 only**, never on
Petabridge.Cmd's default `0.0.0.0:9110`: its commands can make a node leave or down the
cluster, and they carry no authentication. Reach it from the machine itself, or forward a port.

| Host | Port (`PetabridgeCmd:Port`) |
|---|---|
| A node, by default and in Development | 9110 |
| Under the AppHost: `node`, `node-2`, `node-3` | 9110, 9111, 9112 |
| Under the AppHost: `gateway` | 9120 |

Install the client once: `dotnet tool install --global pbm`. On a machine with only the .NET 11
SDK, run it with `DOTNET_ROLL_FORWARD=Major`.

In Kubernetes (Chapter 21), forward the pod's port and connect to your end of it:

```bash
kubectl port-forward pod/<node-pod> 9110:9110
pbm 127.0.0.1:9110 cluster show
```

## Commands, with what a Development node answered at `ch18-s08`

```text
$ pbm 127.0.0.1:9110 cluster show
akka.tcp://ohmline@localhost:38967 | [charging] | up |  | 1.0.0
Count: 1 nodes

$ pbm 127.0.0.1:9110 cluster-sharding regions
charging-journey
id-token
charge-point-listing
site-power-budget
charging-session
charge-point

$ pbm 127.0.0.1:9110 cluster-sharding region-stats -r charge-point
akka.tcp://ohmline@localhost:38967 | [charging] | up |  | 1.0.0 [coordinator](charge-point) | shards: 1 | entities: 1

$ pbm 127.0.0.1:9110 actor alive
...
TariffBookActor | Ohmline.Tariffs.Actors.Book.TariffBookActor | 1
ChargePointActor | Ohmline.Charging.Actors.ChargePoints.ChargePointActor | 1
```

`actor alive` counts actors by type from Akka.NET's actor telemetry events, which
`WithShellMetrics()` switches on (section 18.2); a type whose count only grows is a leak.

`cluster leave <address>` asks a node to leave gracefully before it is stopped; coordinated
shutdown on SIGTERM normally does it already. `cluster down-unreachable` downs every unreachable
member: the split-brain resolver's job, done by hand only when it cannot decide.
