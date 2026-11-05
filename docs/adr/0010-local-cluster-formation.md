# 0010. A local cluster forms through a static seed

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch12-s10

## Context

From section 12.10, `aspire run` (or `dotnet run --project src/Ohmline.AppHost`) starts
PostgreSQL 17 and three Ohmline nodes on one laptop, and the three must form one cluster. A node
joins a cluster by contacting a node that is already in it; the very first node has to know that
it is the first and join itself. Something has to tell every node where to start.

Three ways were tried or considered:

- **Aspire replicas** (`WithReplicas(3)` on one `node` resource). Aspire gives each replica its
  own port for an endpoint only when the replicas start: three replicas of a test run got
  `Akka__Remote__Port` 25725, 25727 and 25729. Nothing tells a replica that it is the first, and
  no address is known before they start, so no static seed can name one of them. A TCP proxy in
  front of the replicas does not help either: Akka.Remote drops messages addressed to an address
  that is not the receiving node's own.
- **Akka.Aspire and Akka.Aspire.Hosting 1.5.73** (Cluster Bootstrap from the AppHost). Their
  README lists Redis, Azure Table Storage and Kubernetes as discovery backends. Each needs a
  package that is not in Ohmline's allowed set (`Aspire.Hosting.Redis` and
  `Akka.Discovery.Redis`, or their Azure equivalents) and a container that does nothing for the
  domain.
- **A static seed.** One node listens on a port fixed in the AppHost, and every node names that
  address as its seed. This is how a StatefulSet's `pod-0` works in Kubernetes.

## Decision

We will form the local cluster through a static seed:

- The AppHost adds the node three times, as the resources `node`, `node-2` and `node-3`, with
  HTTP on ports 5180, 5181 and 5182 and Akka.Remote on 4053, 4054 and 4055.
- Every node gets `Akka__Cluster__SeedNodes__0 = akka.tcp://ohmline@localhost:4053`, the address
  of `node`. Akka.NET makes the first seed node in the list join itself when no other seed
  answers; the others keep trying the seed until it answers.
- `node-2` and `node-3` wait for `node` to be healthy (`/health`: an Up member of the cluster),
  so that only one node creates the journal's tables at a time while `autoInitialize` is on.
- Outside Development, a node without seed nodes refuses to start (section 12.1).

## Consequences

- One `aspire run` gives a real three-node cluster: shards spread over three nodes, and a request
  to any node reaches any aggregate (section 12.10 shows the journal rows in PostgreSQL).
- If `node` is down, a fresh cluster cannot form, though running nodes keep working: the seed is
  needed only to join. That is acceptable on a laptop, and wrong in production.
- The resource names `node-2` and `node-3` exist only locally; Aspire's durable name for the
  service stays `node`.
- Production does not use static seeds: Chapter 21 replaces them with Akka.Management, Cluster
  Bootstrap and Kubernetes API discovery (`OHMLINE_KUBERNETES=true`). Revisit Akka.Aspire if a
  discovery backend that needs no extra package becomes available.
