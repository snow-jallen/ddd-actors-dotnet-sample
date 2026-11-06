# 0016. A version number means something different at each layer

- **Status:** Accepted
- **Date:** 2026-11-05
- **Book section:** ch21-s01

## Context

Ohmline is about to ship continuously. "Version 1.4.2" will appear in four places, and each has
a different reader who asks a different question of it:

- **The services** (`ohmline/node`, `ohmline/gateway`, `ohmline/billing`, `ohmline/migrator`).
  Their reader is the operator and the cluster itself: can this image join a cluster that runs
  the previous one, and can it be rolled back? Akka.NET's sharding compares the nodes' app
  versions during a rolling update (section 21.7).
- **The contract packages** (`Ohmline.Charging.Contracts`, `Ohmline.Tariffs.Contracts`,
  `Ohmline.Charging.Client.Contracts` and the `Ohmline.SharedKernel` they depend on). Their
  reader is another team's compiler: will my code still build and bind against this package?
- **The event and message manifests** (`charging.meter-value-recorded.v2`,
  `ohmline.charging.session-completed.v1`). Their reader is code of every age: the journal is
  read by today's node, a message by a node one release older or newer (ADR 0007, ADR 0015).
- **The client contracts on the wire** (`/app/v1`). Their reader is every build of the driver
  app that is still installed on a phone. A store release reaches drivers over weeks, and some
  never update; the server cannot roll them back or force them forward (Chapter 19).

One number cannot answer all four questions. Using the service's version for everything would
make a new event version a "major" release of the node, and a new endpoint a breaking change of
the event schema, neither of which is true.

## Decision

We will version each layer on its own terms:

| Layer | Version | MAJOR | MINOR | PATCH |
|---|---|---|---|---|
| Service image | SemVer from the `vX.Y.Z` git tag (MinVer, section 21.2); `MAJOR.MINOR.PATCH` is the cluster's app version | Cannot roll from the previous version: blue/green or a full stop (section 21.8) | New capability, rolls over N-1 | Fix, rolls over N-1 |
| Contract package | SemVer 2 of the public .NET API; `AssemblyVersion` is `MAJOR.0.0.0` | A type or member removed or changed | A type or member added | No API change |
| Event or message | `.vN` in the manifest, per type | Never: a new `.vN+1` manifest with an upcaster, the old one read for ever | An optional field (ADR 0015) | — |
| Client contract (`/app/v1`) | The route's `v1`, per API | A new route prefix (`/app/v2`) served beside `/app/v1` while any supported app build uses it | An optional field, a new endpoint, a new rejection code | — |

1. **Services** are tagged `vX.Y.Z` by the release process (section 21.3). A service's MAJOR is
   bumped only when the release cannot roll over the previous one, for example an Akka.NET
   transport change. Everything else is MINOR or PATCH, decided by the commits.
2. **Contract packages** are packed with `EnablePackageValidation` on, so `dotnet pack` compares
   the API with the previous package. Before the first release there is nothing to compare with;
   from the first release on, `PackageValidationBaselineVersion` names the last published
   version, and a breaking change fails the pack unless the MAJOR goes up. Only these four
   projects are packable.
3. **Events and messages** never get a major version. Their version lives in the manifest and
   moves one type at a time (ADR 0007, ADR 0015).
4. **The client API** keeps every field, endpoint and rejection code that a supported app build
   uses. The oldest supported build is a product decision written down per store release; the
   golden files in `Ohmline.ContractTests` fail when a shape changes.

## Consequences

- A release note can say "1.5.0" and mean exactly one thing to an operator: it rolls.
- Package consumers get a compiler-checked promise, at the cost of a baseline to keep up to
  date and an explicit MAJOR bump when an API really must break.
- An event's history never forces a service MAJOR, and a service MAJOR never touches the journal.
- The fourth audience is the slowest: a field the app reads can be removed from the server only
  after the last build that reads it is no longer supported, which may be a year away.
- `PackagingTests` in `Ohmline.ArchitectureTests` checks that exactly these projects are
  packable and that each validates its package.
